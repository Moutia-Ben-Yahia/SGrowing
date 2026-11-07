using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.Marketing.Loyalty;

/// <summary>
/// Service contract for multi-tenant loyalty point ledger management, tier thresholds, and referral attributions.
/// </summary>
public interface ICustomerLoyaltyRewardsService
{
    Task<LoyaltyAccountSummary> GetAccountSummary(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default);
    Task<LoyaltyAccountSummary> EarnPoints(EarnPointsRequest request, CancellationToken cancellationToken = default);
    Task<LoyaltyAccountSummary> RedeemPoints(RedeemPointsRequest request, CancellationToken cancellationToken = default);
    Task<ReferralAttributionRecord> AttributeReferral(AttributeReferralRequest request, CancellationToken cancellationToken = default);
    IReadOnlyList<LoyaltyTierInfo> GetTierCatalog();
    Task<IReadOnlyList<LoyaltyAccountSummary>> GetAllAccounts(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thread-safe multi-tenant implementation of the customer loyalty rewards and referral ledger engine.
/// Enforces double-entry ledger invariant: BalanceAfter = previous balance + Points.
/// </summary>
public class CustomerLoyaltyRewardsService : ICustomerLoyaltyRewardsService
{
    public static readonly Guid FallbackTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid SultanContactId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid NouraContactId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid FahadContactId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // Partitioned stores: TenantId -> (ContactId -> LoyaltyAccountSummary)
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, LoyaltyAccountSummary>> _tenantAccounts = new();

    // Partitioned referral codes: TenantId -> (NormalizedReferralCode -> ContactId)
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, Guid>> _tenantReferralCodes = new();

    // Partitioned referral attributions: TenantId -> ConcurrentBag<ReferralAttributionRecord>
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<ReferralAttributionRecord>> _tenantAttributions = new();

    // Mutex locks for atomic ledger transitions: (TenantId, ContactId) -> object
    private readonly ConcurrentDictionary<string, object> _syncLocks = new();

    public CustomerLoyaltyRewardsService()
    {
        SeedDefaultTenantAccounts();
    }

    public Task<LoyaltyAccountSummary> GetAccountSummary(Guid tenantId, Guid contactId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            tenantId = FallbackTenantId;
        }

        var accounts = _tenantAccounts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, LoyaltyAccountSummary>());

        if (!accounts.TryGetValue(contactId, out var account))
        {
            throw new KeyNotFoundException($"Loyalty account not found for contact '{contactId}' in tenant '{tenantId}'.");
        }

        return Task.FromResult(account);
    }

    public Task<LoyaltyAccountSummary> EarnPoints(EarnPointsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Points <= 0)
        {
            throw new ArgumentException("Points to earn must be strictly positive.", nameof(request.Points));
        }

        if (request.ContactId == Guid.Empty)
        {
            throw new ArgumentException("ContactId cannot be empty.", nameof(request.ContactId));
        }

        var tenantId = request.TenantId == Guid.Empty ? FallbackTenantId : request.TenantId;
        var lockKey = $"{tenantId}:{request.ContactId}";
        var syncLock = _syncLocks.GetOrAdd(lockKey, _ => new object());

        lock (syncLock)
        {
            var accounts = _tenantAccounts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, LoyaltyAccountSummary>());
            var codes = _tenantReferralCodes.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));

            if (!accounts.TryGetValue(request.ContactId, out var account))
            {
                // Auto-provision standard Bronze membership account if not existing
                var shortId = request.ContactId.ToString("N")[..6].ToUpperInvariant();
                var refCode = $"REF-{shortId}";
                account = new LoyaltyAccountSummary(
                    ContactId: request.ContactId,
                    TenantId: tenantId,
                    ContactName: $"Member {shortId}",
                    ContactEmail: $"member.{shortId.ToLowerInvariant()}@customer.sgrowing.io",
                    CurrentPointsBalance: 0,
                    LifetimePointsEarned: 0,
                    CurrentTier: LoyaltyMembershipTier.Bronze,
                    NextTierPointsRemaining: 500,
                    ReferralCode: refCode,
                    ActiveTierPerks: GetPerksForTier(LoyaltyMembershipTier.Bronze),
                    RecentTransactions: new List<LoyaltyPointLedgerEntry>()
                );
                codes[refCode] = request.ContactId;
            }

            var previousBalance = account.CurrentPointsBalance;
            var newBalance = previousBalance + request.Points;
            var newLifetime = account.LifetimePointsEarned + request.Points;
            var newTier = CalculateTier(newLifetime);
            var nextTierRemaining = CalculateNextTierPointsRemaining(newLifetime);
            var activePerks = GetPerksForTier(newTier);

            // Double-entry invariant verification
            var balanceAfterInvariant = previousBalance + request.Points;
            if (newBalance != balanceAfterInvariant)
            {
                throw new InvalidOperationException($"Double-entry invariant failure: {newBalance} != {balanceAfterInvariant}");
            }

            var entry = new LoyaltyPointLedgerEntry(
                EntryId: Guid.NewGuid(),
                TenantId: tenantId,
                ContactId: request.ContactId,
                Type: request.Type,
                Points: request.Points,
                BalanceAfter: newBalance,
                ReferenceId: request.ReferenceId,
                Description: request.Description,
                CreatedAtUtc: DateTime.UtcNow
            );

            var updatedTransactions = new List<LoyaltyPointLedgerEntry>(account.RecentTransactions);
            updatedTransactions.Insert(0, entry);

            var updatedAccount = account with
            {
                CurrentPointsBalance = newBalance,
                LifetimePointsEarned = newLifetime,
                CurrentTier = newTier,
                NextTierPointsRemaining = nextTierRemaining,
                ActiveTierPerks = activePerks,
                RecentTransactions = updatedTransactions
            };

            accounts[request.ContactId] = updatedAccount;
            return Task.FromResult(updatedAccount);
        }
    }

    public Task<LoyaltyAccountSummary> RedeemPoints(RedeemPointsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PointsToRedeem <= 0)
        {
            throw new ArgumentException("Points to redeem must be strictly positive.", nameof(request.PointsToRedeem));
        }

        if (request.ContactId == Guid.Empty)
        {
            throw new ArgumentException("ContactId cannot be empty.", nameof(request.ContactId));
        }

        var tenantId = request.TenantId == Guid.Empty ? FallbackTenantId : request.TenantId;
        var lockKey = $"{tenantId}:{request.ContactId}";
        var syncLock = _syncLocks.GetOrAdd(lockKey, _ => new object());

        lock (syncLock)
        {
            var accounts = _tenantAccounts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, LoyaltyAccountSummary>());

            if (!accounts.TryGetValue(request.ContactId, out var account))
            {
                throw new KeyNotFoundException($"Loyalty account not found for contact '{request.ContactId}' in tenant '{tenantId}'.");
            }

            // Guard against redeeming more points than available
            if (request.PointsToRedeem > account.CurrentPointsBalance)
            {
                throw new InvalidOperationException(
                    $"Insufficient points balance for redemption. Requested: {request.PointsToRedeem}, Available: {account.CurrentPointsBalance}.");
            }

            var previousBalance = account.CurrentPointsBalance;
            var pointsBurn = -request.PointsToRedeem;
            var newBalance = previousBalance + pointsBurn; // Invariant: previous balance + points

            // Double-entry invariant verification
            var balanceAfterInvariant = previousBalance + pointsBurn;
            if (newBalance != balanceAfterInvariant)
            {
                throw new InvalidOperationException($"Double-entry invariant failure: {newBalance} != {balanceAfterInvariant}");
            }

            var entry = new LoyaltyPointLedgerEntry(
                EntryId: Guid.NewGuid(),
                TenantId: tenantId,
                ContactId: request.ContactId,
                Type: LoyaltyTransactionType.BurnRedemption,
                Points: pointsBurn,
                BalanceAfter: newBalance,
                ReferenceId: request.OrderReferenceId,
                Description: $"Redeemed {request.PointsToRedeem} points for order {request.OrderReferenceId}",
                CreatedAtUtc: DateTime.UtcNow
            );

            var updatedTransactions = new List<LoyaltyPointLedgerEntry>(account.RecentTransactions);
            updatedTransactions.Insert(0, entry);

            // Note: LifetimePointsEarned remains untouched upon redemption to preserve earned tier status
            var updatedAccount = account with
            {
                CurrentPointsBalance = newBalance,
                RecentTransactions = updatedTransactions
            };

            accounts[request.ContactId] = updatedAccount;
            return Task.FromResult(updatedAccount);
        }
    }

    public async Task<ReferralAttributionRecord> AttributeReferral(AttributeReferralRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ReferralCode))
        {
            throw new ArgumentException("Referral code is required.", nameof(request.ReferralCode));
        }

        if (request.RefereeContactId == Guid.Empty)
        {
            throw new ArgumentException("RefereeContactId cannot be empty.", nameof(request.RefereeContactId));
        }

        var tenantId = request.TenantId == Guid.Empty ? FallbackTenantId : request.TenantId;
        var normalizedCode = request.ReferralCode.Trim().ToUpperInvariant();

        var codes = _tenantReferralCodes.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
        if (!codes.TryGetValue(normalizedCode, out var referrerContactId))
        {
            throw new KeyNotFoundException($"Referral code '{request.ReferralCode}' not found for tenant '{tenantId}'.");
        }

        // Self-referral guard
        if (referrerContactId == request.RefereeContactId)
        {
            throw new InvalidOperationException("Self-referral is not permitted. Referee contact ID cannot match referrer.");
        }

        // Duplicate referral guard
        var attributions = _tenantAttributions.GetOrAdd(tenantId, _ => new ConcurrentBag<ReferralAttributionRecord>());
        if (attributions.Any(a => a.RefereeContactId == request.RefereeContactId))
        {
            throw new InvalidOperationException($"Referee '{request.RefereeContactId}' has already been attributed to a referral.");
        }

        // Reward calculation: 250 base points + 10% of qualifying order SAR
        var bonusFromSpend = (int)Math.Round(request.QualifyingOrderTotalSar * 0.10m);
        var referrerPointsAwarded = 250 + Math.Max(0, bonusFromSpend);
        const decimal refereeDiscountPercent = 15.0m;

        // Credit points to referrer
        var earnRequest = new EarnPointsRequest(
            TenantId: tenantId,
            ContactId: referrerContactId,
            Type: LoyaltyTransactionType.EarnReferral,
            Points: referrerPointsAwarded,
            ReferenceId: $"REF-ATTR-{request.RefereeContactId.ToString("N")[..8].ToUpperInvariant()}",
            Description: $"Referral award for referee order SAR {request.QualifyingOrderTotalSar:F2} via code {normalizedCode}"
        );

        await EarnPoints(earnRequest, cancellationToken);

        var attribution = new ReferralAttributionRecord(
            ReferralId: Guid.NewGuid(),
            TenantId: tenantId,
            ReferrerContactId: referrerContactId,
            RefereeContactId: request.RefereeContactId,
            ReferralCode: normalizedCode,
            ReferrerPointsAwarded: referrerPointsAwarded,
            RefereeDiscountPercent: refereeDiscountPercent,
            AttributedAtUtc: DateTime.UtcNow
        );

        attributions.Add(attribution);
        return attribution;
    }

    public IReadOnlyList<LoyaltyTierInfo> GetTierCatalog()
    {
        return
        [
            new LoyaltyTierInfo(
                Tier: LoyaltyMembershipTier.Bronze,
                TierName: "Bronze",
                MinPoints: 0,
                MaxPoints: 499,
                PointsMultiplier: 1.0m,
                Perks: GetPerksForTier(LoyaltyMembershipTier.Bronze)
            ),
            new LoyaltyTierInfo(
                Tier: LoyaltyMembershipTier.Silver,
                TierName: "Silver",
                MinPoints: 500,
                MaxPoints: 1999,
                PointsMultiplier: 1.25m,
                Perks: GetPerksForTier(LoyaltyMembershipTier.Silver)
            ),
            new LoyaltyTierInfo(
                Tier: LoyaltyMembershipTier.Gold,
                TierName: "Gold",
                MinPoints: 2000,
                MaxPoints: 4999,
                PointsMultiplier: 1.5m,
                Perks: GetPerksForTier(LoyaltyMembershipTier.Gold)
            ),
            new LoyaltyTierInfo(
                Tier: LoyaltyMembershipTier.VipDiamond,
                TierName: "VIP Diamond",
                MinPoints: 5000,
                MaxPoints: null,
                PointsMultiplier: 2.0m,
                Perks: GetPerksForTier(LoyaltyMembershipTier.VipDiamond)
            )
        ];
    }

    public Task<IReadOnlyList<LoyaltyAccountSummary>> GetAllAccounts(Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            tenantId = FallbackTenantId;
        }

        var accounts = _tenantAccounts.GetOrAdd(tenantId, _ => new ConcurrentDictionary<Guid, LoyaltyAccountSummary>());
        var list = accounts.Values.OrderByDescending(a => a.LifetimePointsEarned).ToList();
        return Task.FromResult<IReadOnlyList<LoyaltyAccountSummary>>(list);
    }

    public static LoyaltyMembershipTier CalculateTier(int lifetimePoints) =>
        lifetimePoints switch
        {
            >= 5000 => LoyaltyMembershipTier.VipDiamond,
            >= 2000 => LoyaltyMembershipTier.Gold,
            >= 500 => LoyaltyMembershipTier.Silver,
            _ => LoyaltyMembershipTier.Bronze
        };

    public static int CalculateNextTierPointsRemaining(int lifetimePoints) =>
        lifetimePoints switch
        {
            >= 5000 => 0,
            >= 2000 => 5000 - lifetimePoints,
            >= 500 => 2000 - lifetimePoints,
            _ => 500 - lifetimePoints
        };

    public static List<string> GetPerksForTier(LoyaltyMembershipTier tier) =>
        tier switch
        {
            LoyaltyMembershipTier.VipDiamond =>
            [
                "2.0x Double Point Earning on every SAR spent",
                "Dedicated VIP Concierge & Personal Account Manager",
                "Zero-Fee Instant Returns & GCC Priority Exchanges",
                "Complimentary White-Glove VIP Express Courier",
                "Annual Invitation to Exclusive SGrowing Diamond Gala"
            ],
            LoyaltyMembershipTier.Gold =>
            [
                "1.5x Points Multiplier on all marketplace orders",
                "Complimentary Express Same-Day Delivery in KSA",
                "Priority WhatsApp Support Hotline (24/7)",
                "Exclusive Seasonal VIP Mystery Gift Box"
            ],
            LoyaltyMembershipTier.Silver =>
            [
                "1.25x Points Multiplier on GCC orders",
                "Free Standard Delivery on orders over SAR 150",
                "Early Access (12h) to Flash Sales and Launches"
            ],
            _ =>
            [
                "Standard 1x Points Earning on all orders (1 pt / SAR)",
                "Birthday Bonus Celebration Points (100 pts)",
                "Members-Only Monthly Newsletter & Secret Deals"
            ]
        };

    private void SeedDefaultTenantAccounts()
    {
        var accounts = _tenantAccounts.GetOrAdd(FallbackTenantId, _ => new ConcurrentDictionary<Guid, LoyaltyAccountSummary>());
        var codes = _tenantReferralCodes.GetOrAdd(FallbackTenantId, _ => new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));

        // Account 1: Sultan Al-Dosari - VIP Diamond, 4850 pts, referral code: "SULTAN-VIP"
        var sultanTransactions = new List<LoyaltyPointLedgerEntry>
        {
            new(Guid.NewGuid(), FallbackTenantId, SultanContactId, LoyaltyTransactionType.BurnRedemption, -500, 4850, "RED-SA-1004", "Redeemed SAR 50 Store Voucher", DateTime.UtcNow.AddDays(-1)),
            new(Guid.NewGuid(), FallbackTenantId, SultanContactId, LoyaltyTransactionType.EarnReferral, 350, 5350, "REF-SA-4421", "Referral bonus for referee Fahad Al-Harbi", DateTime.UtcNow.AddDays(-3)),
            new(Guid.NewGuid(), FallbackTenantId, SultanContactId, LoyaltyTransactionType.EarnPurchase, 3000, 5000, "ORD-SA-9855", "Earned from Premium Executive Package", DateTime.UtcNow.AddDays(-7)),
            new(Guid.NewGuid(), FallbackTenantId, SultanContactId, LoyaltyTransactionType.EarnPurchase, 2000, 2000, "ORD-SA-9801", "Earned from Luxury Oud Collection", DateTime.UtcNow.AddDays(-14))
        };

        var sultan = new LoyaltyAccountSummary(
            ContactId: SultanContactId,
            TenantId: FallbackTenantId,
            ContactName: "Sultan Al-Dosari",
            ContactEmail: "sultan.dosari@growthksa.com",
            CurrentPointsBalance: 4850,
            LifetimePointsEarned: 5350,
            CurrentTier: LoyaltyMembershipTier.VipDiamond,
            NextTierPointsRemaining: 0,
            ReferralCode: "SULTAN-VIP",
            ActiveTierPerks: GetPerksForTier(LoyaltyMembershipTier.VipDiamond),
            RecentTransactions: sultanTransactions
        );
        accounts[SultanContactId] = sultan;
        codes["SULTAN-VIP"] = SultanContactId;

        // Account 2: Noura Al-Otaibi - Silver, 850 pts
        var nouraTransactions = new List<LoyaltyPointLedgerEntry>
        {
            new(Guid.NewGuid(), FallbackTenantId, NouraContactId, LoyaltyTransactionType.EarnPurchase, 850, 850, "ORD-SA-7712", "Ramadan Abaya Collection purchase", DateTime.UtcNow.AddDays(-5))
        };

        var noura = new LoyaltyAccountSummary(
            ContactId: NouraContactId,
            TenantId: FallbackTenantId,
            ContactName: "Noura Al-Otaibi",
            ContactEmail: "noura.otaibi@riyadhretail.sa",
            CurrentPointsBalance: 850,
            LifetimePointsEarned: 850,
            CurrentTier: LoyaltyMembershipTier.Silver,
            NextTierPointsRemaining: 1150,
            ReferralCode: "NOURA-SILVER",
            ActiveTierPerks: GetPerksForTier(LoyaltyMembershipTier.Silver),
            RecentTransactions: nouraTransactions
        );
        accounts[NouraContactId] = noura;
        codes["NOURA-SILVER"] = NouraContactId;

        // Account 3: Fahad Al-Harbi - Gold, 2400 pts
        var fahadTransactions = new List<LoyaltyPointLedgerEntry>
        {
            new(Guid.NewGuid(), FallbackTenantId, FahadContactId, LoyaltyTransactionType.EarnPurchase, 2400, 2400, "ORD-SA-6610", "Perfume Gift Sets order", DateTime.UtcNow.AddDays(-10))
        };

        var fahad = new LoyaltyAccountSummary(
            ContactId: FahadContactId,
            TenantId: FallbackTenantId,
            ContactName: "Fahad Al-Harbi",
            ContactEmail: "fahad.harbi@jeddahventures.com",
            CurrentPointsBalance: 2400,
            LifetimePointsEarned: 2400,
            CurrentTier: LoyaltyMembershipTier.Gold,
            NextTierPointsRemaining: 2600,
            ReferralCode: "FAHAD-GOLD",
            ActiveTierPerks: GetPerksForTier(LoyaltyMembershipTier.Gold),
            RecentTransactions: fahadTransactions
        );
        accounts[FahadContactId] = fahad;
        codes["FAHAD-GOLD"] = FahadContactId;
    }
}
