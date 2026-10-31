using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SGrowing.Api.Modules.Commerce.Affiliates;

#region Enums

public enum AffiliateTierLevel
{
    Level1Direct = 1,
    Level2Parent = 2,
    Level3Master = 3
}

public enum CommissionRuleType
{
    PercentageOfGmv = 1,
    PercentageOfMargin = 2,
    FixedBountyPerOrder = 3,
    RecurringSubscriptionSplit = 4,
    MilestoneBonus = 5
}

public enum CommissionState
{
    PendingEscrow = 1,
    Approved = 2,
    Disputed = 3,
    RefundReversed = 4,
    PaidOut = 5
}

public enum PayoutMethod
{
    Sepa = 1,
    PayPal = 2,
    Wire = 3,
    Crypto = 4
}

public enum AffiliateStatus
{
    Active = 1,
    Suspended = 2,
    Inactive = 3
}

public enum TaxFormType
{
    None = 0,
    W8Ben = 1,
    W9 = 2,
    Vat = 3
}

#endregion

#region Domain Entities & Records

public record CommissionRule
{
    public CommissionRuleType RuleType { get; init; } = CommissionRuleType.PercentageOfGmv;
    public decimal RatePercentage { get; init; } = 15.0m;
    public decimal FixedAmount { get; init; } = 0m;
    public decimal MinimumOrderThreshold { get; init; } = 0m;
    public List<string> ExcludedCategories { get; init; } = new();
    public int MilestoneTargetSales { get; init; } = 50;
    public decimal MilestoneBonusAmount { get; init; } = 500.0m;
    public decimal? Tier2RateOverride { get; init; }
    public decimal? Tier3RateOverride { get; init; }
}

public record PayoutDetails
{
    public PayoutMethod PreferredMethod { get; init; } = PayoutMethod.PayPal;
    public string? AccountHolderName { get; init; }
    public string? Iban { get; init; }
    public string? SwiftBic { get; init; }
    public string? BankName { get; init; }
    public string? PayPalEmail { get; init; }
    public string? CryptoWalletAddress { get; init; }
    public string? CryptoChain { get; init; } = "Ethereum";
    public string Currency { get; init; } = "USD";
}

public record TaxDetails
{
    public TaxFormType FormType { get; init; } = TaxFormType.W8Ben;
    public string? TaxIdOrSsn { get; init; }
    public decimal WithholdingPercentage { get; init; } = 10.0m;
    public bool IsVerified { get; init; } = true;
    public DateTime? VerifiedAtUtc { get; init; } = DateTime.UtcNow;
}

public class AffiliateMetrics
{
    public decimal TotalReferredGmv { get; set; }
    public decimal TotalCommissionsEarned { get; set; }
    public decimal TotalCommissionsPaid { get; set; }
    public decimal PendingEscrowAmount { get; set; }
    public decimal ApprovedAvailableAmount { get; set; }
    public decimal RefundReversalAmount { get; set; }
    public int TotalClicks { get; set; }
    public int TotalConversions { get; set; }
    public int RefundReversalCount { get; set; }
    public int CumulativeSalesCount { get; set; }
    public DateTime? LastConversionAtUtc { get; set; }
}

public class AffiliatePartner
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TenantId { get; init; } = "tenant-default";
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public AffiliateTierLevel TierLevel { get; set; } = AffiliateTierLevel.Level1Direct;
    public Guid? ParentPartnerId { get; set; }
    public AffiliateStatus Status { get; set; } = AffiliateStatus.Active;
    public List<string> ReferralCodes { get; set; } = new();
    public string TrackingSlug { get; set; } = string.Empty;
    public string? CustomLandingDomain { get; set; }
    public int AttributionWindowDays { get; set; } = 30; // 0 = Lifetime
    public int EscrowHoldbackDays { get; set; } = 14;
    public decimal MinimumPayoutThreshold { get; set; } = 50.0m;
    public CommissionRule CommissionRule { get; set; } = new();
    public PayoutDetails PayoutDetails { get; set; } = new();
    public TaxDetails TaxDetails { get; set; } = new();
    public AffiliateMetrics Metrics { get; set; } = new();
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class CommissionTransaction
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TenantId { get; init; } = "tenant-default";
    public string OrderId { get; init; } = string.Empty;
    public Guid ConversionId { get; init; } = Guid.NewGuid();
    public Guid AffiliateId { get; init; }
    public string AffiliateName { get; init; } = string.Empty;
    public AffiliateTierLevel TierLevel { get; init; }
    public CommissionRuleType RuleType { get; init; }
    public decimal OrderGmv { get; init; }
    public decimal OrderMargin { get; init; }
    public decimal CommissionRate { get; init; }
    public decimal CommissionAmount { get; init; }
    public CommissionState State { get; set; } = CommissionState.PendingEscrow;
    public string Currency { get; init; } = "USD";
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public int EscrowHoldbackDays { get; init; } = 14;
    public DateTime MaturesAtUtc { get; init; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? PaidOutAtUtc { get; set; }
    public Guid? SettlementBatchId { get; set; }
    public string? ReversalReason { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    public string? Notes { get; set; }
}

public record AttributionCookieRecord
{
    public string Token { get; init; } = Guid.NewGuid().ToString("N");
    public Guid AffiliateId { get; init; }
    public string ReferralCode { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string? CustomDomain { get; init; }
    public string? CustomerId { get; init; }
    public DateTime ClickedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; init; }
}

public class SettlementBatch
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TenantId { get; init; } = "tenant-default";
    public string BatchNumber { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public decimal GrossTotal { get; set; }
    public decimal TotalTaxWithheld { get; set; }
    public decimal NetTotal { get; set; }
    public int AffiliateCount { get; set; }
    public int CommissionCount { get; set; }
    public string Currency { get; init; } = "USD";
    public PayoutMethod PayoutMethod { get; init; }
    public List<SettlementLineItem> LineItems { get; init; } = new();
    public string? SepaXmlManifest { get; set; }
    public string? PayPalCsvManifest { get; set; }
    public string? WireCryptoLedgerManifest { get; set; }
}

public class SettlementLineItem
{
    public Guid LineItemId { get; init; } = Guid.NewGuid();
    public Guid AffiliateId { get; init; }
    public string AffiliateName { get; init; } = string.Empty;
    public string RecipientEmail { get; init; } = string.Empty;
    public PayoutMethod Method { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal TaxWithheld { get; init; }
    public decimal NetAmount { get; init; }
    public string DestinationReference { get; init; } = string.Empty;
    public string? SwiftBic { get; init; }
    public List<Guid> CommissionIds { get; init; } = new();
}

#endregion

#region Request & Response DTOs

public record RegisterAffiliateRequest(
    string Name,
    string Email,
    AffiliateTierLevel TierLevel,
    Guid? ParentPartnerId = null,
    string? CustomReferralCode = null,
    string? TrackingSlug = null,
    string? CustomLandingDomain = null,
    int AttributionWindowDays = 30,
    int EscrowHoldbackDays = 14,
    decimal MinimumPayoutThreshold = 50.0m,
    CommissionRule? CommissionRule = null,
    PayoutDetails? PayoutDetails = null,
    TaxDetails? TaxDetails = null,
    string TenantId = "tenant-default");

public record UpdateAffiliateRequest(
    string? Name = null,
    string? Email = null,
    AffiliateStatus? Status = null,
    Guid? ParentPartnerId = null,
    CommissionRule? CommissionRule = null,
    PayoutDetails? PayoutDetails = null,
    TaxDetails? TaxDetails = null,
    int? AttributionWindowDays = null,
    int? EscrowHoldbackDays = null,
    decimal? MinimumPayoutThreshold = null);

public record TrackConversionRequest(
    string OrderId,
    decimal OrderGmv,
    decimal CostOfGoodsSold = 0m,
    string? ProductCategory = null,
    string? ReferralCode = null,
    string? TrackingSlug = null,
    string? LandingDomain = null,
    string? AttributionToken = null,
    DateTime? ClickTimestampUtc = null,
    DateTime? OrderTimestampUtc = null,
    string? CustomerId = null,
    string Currency = "USD",
    bool IsRecurringSubscription = false,
    string TenantId = "tenant-default");

public record ConversionResult(
    bool IsAttributed,
    string? Reason,
    Guid? DirectAffiliateId,
    List<CommissionTransaction> CreatedCommissions,
    decimal TotalCommissionsAwarded);

public record ReverseCommissionRequest(
    string? OrderId = null,
    Guid? CommissionId = null,
    string Reason = "Order refund or fraud dispute",
    string TenantId = "tenant-default");

public record GenerateSettlementBatchRequest(
    decimal MinPayoutThreshold = 50.0m,
    PayoutMethod? PayoutMethodFilter = null,
    string Currency = "USD",
    List<Guid>? SpecificAffiliateIds = null,
    string TenantId = "tenant-default");

public record TopAffiliateDto(
    Guid AffiliateId,
    string Name,
    AffiliateTierLevel Tier,
    decimal TotalGmv,
    decimal TotalCommissions,
    int ConversionCount);

public record AffiliateCommissionMetrics(
    decimal TotalReferredGmv,
    decimal TotalCommissionsAccrued,
    decimal PendingEscrowVolume,
    decimal ApprovedAvailableVolume,
    decimal TotalPaidOut,
    decimal RefundReversedVolume,
    int ActiveAffiliatesCount,
    int TotalConversionsCount,
    decimal AverageOrderValue,
    List<TopAffiliateDto> TopAffiliates,
    Dictionary<int, decimal> CommissionsByTier,
    Dictionary<string, int> CommissionsByStatus);

#endregion

#region Service Interface

public interface IAffiliateCommissionService
{
    Task<IReadOnlyList<AffiliatePartner>> GetPartnersAsync(
        string tenantId = "tenant-default",
        AffiliateTierLevel? tier = null,
        AffiliateStatus? status = null,
        string? search = null,
        CancellationToken ct = default);

    Task<AffiliatePartner?> GetPartnerByIdAsync(Guid id, CancellationToken ct = default);

    Task<AffiliatePartner?> ResolvePartnerByCodeOrSlugAsync(string codeOrSlugOrDomain, CancellationToken ct = default);

    Task<AffiliatePartner> RegisterPartnerAsync(RegisterAffiliateRequest request, CancellationToken ct = default);

    Task<AffiliatePartner> UpdatePartnerAsync(Guid id, UpdateAffiliateRequest request, CancellationToken ct = default);

    Task<AttributionCookieRecord> TrackClickAsync(string codeOrSlugOrDomain, string? customerId = null, CancellationToken ct = default);

    Task<ConversionResult> TrackConversionAsync(TrackConversionRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<CommissionTransaction>> ReverseCommissionAsync(ReverseCommissionRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<CommissionTransaction>> GetCommissionsAsync(
        string tenantId = "tenant-default",
        CommissionState? status = null,
        AffiliateTierLevel? tier = null,
        Guid? affiliateId = null,
        string? orderId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);

    Task<CommissionTransaction?> GetCommissionByIdAsync(Guid id, CancellationToken ct = default);

    Task<int> ProcessMaturitySweepAsync(DateTime? asOfUtc = null, CancellationToken ct = default);

    Task<SettlementBatch> GeneratePayoutBatchAsync(GenerateSettlementBatchRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<SettlementBatch>> GetSettlementBatchesAsync(string tenantId = "tenant-default", CancellationToken ct = default);

    Task<SettlementBatch?> GetSettlementBatchByIdAsync(Guid id, CancellationToken ct = default);

    Task<string?> GetBatchManifestAsync(Guid batchId, string? format = null, CancellationToken ct = default);

    Task<AffiliateCommissionMetrics> GetMetricsAsync(string tenantId = "tenant-default", CancellationToken ct = default);

    Task ResetToPreseededDataAsync(CancellationToken ct = default);
}

#endregion

#region Service Implementation

public class AffiliateCommissionService : IAffiliateCommissionService
{
    private static readonly object _syncRoot = new();
    private static readonly object _settlementLock = new();

    private readonly ConcurrentDictionary<Guid, AffiliatePartner> _partners = new();
    private readonly ConcurrentDictionary<Guid, CommissionTransaction> _commissions = new();
    private readonly ConcurrentDictionary<Guid, SettlementBatch> _batches = new();
    private readonly ConcurrentDictionary<string, AttributionCookieRecord> _cookies = new();

    // Lookups
    private readonly ConcurrentDictionary<string, Guid> _codeToPartnerId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Guid> _slugToPartnerId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Guid> _domainToPartnerId = new(StringComparer.OrdinalIgnoreCase);

    public AffiliateCommissionService()
    {
        SeedInitialData();
    }

    #region Partner Management

    public Task<IReadOnlyList<AffiliatePartner>> GetPartnersAsync(
        string tenantId = "tenant-default",
        AffiliateTierLevel? tier = null,
        AffiliateStatus? status = null,
        string? search = null,
        CancellationToken ct = default)
    {
        var query = _partners.Values.Where(p => p.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));

        if (tier.HasValue)
            query = query.Where(p => p.TierLevel == tier.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                                  || p.Email.Contains(s, StringComparison.OrdinalIgnoreCase)
                                  || p.TrackingSlug.Contains(s, StringComparison.OrdinalIgnoreCase)
                                  || p.ReferralCodes.Any(c => c.Contains(s, StringComparison.OrdinalIgnoreCase)));
        }

        return Task.FromResult<IReadOnlyList<AffiliatePartner>>(query.OrderBy(p => p.TierLevel).ThenBy(p => p.Name).ToList());
    }

    public Task<AffiliatePartner?> GetPartnerByIdAsync(Guid id, CancellationToken ct = default)
    {
        _partners.TryGetValue(id, out var partner);
        return Task.FromResult(partner);
    }

    public Task<AffiliatePartner?> ResolvePartnerByCodeOrSlugAsync(string codeOrSlugOrDomain, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codeOrSlugOrDomain))
            return Task.FromResult<AffiliatePartner?>(null);

        var cleaned = codeOrSlugOrDomain.Trim();

        // 1. Check referral code
        if (_codeToPartnerId.TryGetValue(cleaned, out var idByCode) && _partners.TryGetValue(idByCode, out var partnerByCode))
            return Task.FromResult<AffiliatePartner?>(partnerByCode);

        // 2. Check tracking slug
        if (_slugToPartnerId.TryGetValue(cleaned, out var idBySlug) && _partners.TryGetValue(idBySlug, out var partnerBySlug))
            return Task.FromResult<AffiliatePartner?>(partnerBySlug);

        // 3. Check custom landing domain
        if (_domainToPartnerId.TryGetValue(cleaned, out var idByDomain) && _partners.TryGetValue(idByDomain, out var partnerByDomain))
            return Task.FromResult<AffiliatePartner?>(partnerByDomain);

        return Task.FromResult<AffiliatePartner?>(null);
    }

    public Task<AffiliatePartner> RegisterPartnerAsync(RegisterAffiliateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Partner name is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Partner email is required.", nameof(request));

        lock (_syncRoot)
        {
            var partnerId = Guid.NewGuid();

            // Generate referral code if not provided
            var referralCode = !string.IsNullOrWhiteSpace(request.CustomReferralCode)
                ? SanitizeCode(request.CustomReferralCode)
                : GenerateReferralCode(request.Name);

            // Ensure unique code
            int counter = 1;
            var baseCode = referralCode;
            while (_codeToPartnerId.ContainsKey(referralCode))
            {
                referralCode = $"{baseCode}{counter++}";
            }

            var slug = !string.IsNullOrWhiteSpace(request.TrackingSlug)
                ? SanitizeSlug(request.TrackingSlug)
                : GenerateSlug(request.Name);

            var baseSlug = slug;
            counter = 1;
            while (_slugToPartnerId.ContainsKey(slug))
            {
                slug = $"{baseSlug}-{counter++}";
            }

            var defaultRule = request.CommissionRule ?? new CommissionRule
            {
                RuleType = CommissionRuleType.PercentageOfGmv,
                RatePercentage = request.TierLevel switch
                {
                    AffiliateTierLevel.Level1Direct => 15.0m,
                    AffiliateTierLevel.Level2Parent => 5.0m,
                    AffiliateTierLevel.Level3Master => 2.0m,
                    _ => 10.0m
                }
            };

            var partner = new AffiliatePartner
            {
                Id = partnerId,
                TenantId = request.TenantId,
                Name = request.Name.Trim(),
                Email = request.Email.Trim().ToLowerInvariant(),
                TierLevel = request.TierLevel,
                ParentPartnerId = request.ParentPartnerId,
                Status = AffiliateStatus.Active,
                ReferralCodes = new List<string> { referralCode },
                TrackingSlug = slug,
                CustomLandingDomain = request.CustomLandingDomain?.Trim().ToLowerInvariant(),
                AttributionWindowDays = request.AttributionWindowDays,
                EscrowHoldbackDays = request.EscrowHoldbackDays,
                MinimumPayoutThreshold = request.MinimumPayoutThreshold,
                CommissionRule = defaultRule,
                PayoutDetails = request.PayoutDetails ?? new PayoutDetails { PreferredMethod = PayoutMethod.PayPal, PayPalEmail = request.Email },
                TaxDetails = request.TaxDetails ?? new TaxDetails(),
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _partners[partnerId] = partner;
            _codeToPartnerId[referralCode] = partnerId;
            _slugToPartnerId[slug] = partnerId;
            if (!string.IsNullOrWhiteSpace(partner.CustomLandingDomain))
                _domainToPartnerId[partner.CustomLandingDomain] = partnerId;

            return Task.FromResult(partner);
        }
    }

    public Task<AffiliatePartner> UpdatePartnerAsync(Guid id, UpdateAffiliateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_partners.TryGetValue(id, out var partner))
            throw new KeyNotFoundException($"Affiliate partner '{id}' not found.");

        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(request.Name))
                partner.Name = request.Name.Trim();

            if (!string.IsNullOrWhiteSpace(request.Email))
                partner.Email = request.Email.Trim().ToLowerInvariant();

            if (request.Status.HasValue)
                partner.Status = request.Status.Value;

            if (request.ParentPartnerId.HasValue)
                partner.ParentPartnerId = request.ParentPartnerId.Value;

            if (request.CommissionRule != null)
                partner.CommissionRule = request.CommissionRule;

            if (request.PayoutDetails != null)
                partner.PayoutDetails = request.PayoutDetails;

            if (request.TaxDetails != null)
                partner.TaxDetails = request.TaxDetails;

            if (request.AttributionWindowDays.HasValue)
                partner.AttributionWindowDays = request.AttributionWindowDays.Value;

            if (request.EscrowHoldbackDays.HasValue)
                partner.EscrowHoldbackDays = request.EscrowHoldbackDays.Value;

            if (request.MinimumPayoutThreshold.HasValue)
                partner.MinimumPayoutThreshold = request.MinimumPayoutThreshold.Value;

            partner.UpdatedAtUtc = DateTime.UtcNow;
            return Task.FromResult(partner);
        }
    }

    #endregion

    #region Click Tracking & Attribution

    public Task<AttributionCookieRecord> TrackClickAsync(string codeOrSlugOrDomain, string? customerId = null, CancellationToken ct = default)
    {
        var partner = ResolvePartnerByCodeOrSlugAsync(codeOrSlugOrDomain, ct).GetAwaiter().GetResult();
        if (partner == null)
            throw new ArgumentException($"Cannot resolve affiliate partner for tracking input '{codeOrSlugOrDomain}'.");

        var now = DateTime.UtcNow;
        DateTime? expiresAt = partner.AttributionWindowDays > 0 ? now.AddDays(partner.AttributionWindowDays) : null;

        var token = "aff_" + Guid.NewGuid().ToString("N");
        var cookie = new AttributionCookieRecord
        {
            Token = token,
            AffiliateId = partner.Id,
            ReferralCode = partner.ReferralCodes.FirstOrDefault() ?? partner.TrackingSlug,
            Slug = partner.TrackingSlug,
            CustomDomain = partner.CustomLandingDomain,
            CustomerId = customerId,
            ClickedAtUtc = now,
            ExpiresAtUtc = expiresAt
        };

        _cookies[token] = cookie;
        lock (partner.Metrics)
        {
            partner.Metrics.TotalClicks++;
        }

        return Task.FromResult(cookie);
    }

    #endregion

    #region Conversion Tracking & Multi-Tier Cascade

    public Task<ConversionResult> TrackConversionAsync(TrackConversionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.OrderId))
            throw new ArgumentException("OrderId is required.", nameof(request));

        lock (_syncRoot)
        {
            // 1. Resolve Level 1 Direct Partner
            AffiliatePartner? directPartner = null;
            DateTime? clickTime = request.ClickTimestampUtc;

            if (!string.IsNullOrWhiteSpace(request.AttributionToken) && _cookies.TryGetValue(request.AttributionToken, out var cookie))
            {
                clickTime = cookie.ClickedAtUtc;
                _partners.TryGetValue(cookie.AffiliateId, out directPartner);

                // Verify cookie expiration
                var orderTime = request.OrderTimestampUtc ?? DateTime.UtcNow;
                if (cookie.ExpiresAtUtc.HasValue && orderTime > cookie.ExpiresAtUtc.Value)
                {
                    return Task.FromResult(new ConversionResult(
                        IsAttributed: false,
                        Reason: "Attribution cookie expired",
                        DirectAffiliateId: null,
                        CreatedCommissions: new List<CommissionTransaction>(),
                        TotalCommissionsAwarded: 0m));
                }
            }

            if (directPartner == null)
            {
                var refKey = request.ReferralCode ?? request.TrackingSlug ?? request.LandingDomain;
                if (!string.IsNullOrWhiteSpace(refKey))
                {
                    directPartner = ResolvePartnerByCodeOrSlugAsync(refKey, ct).GetAwaiter().GetResult();
                }
            }

            if (directPartner == null)
            {
                return Task.FromResult(new ConversionResult(
                    IsAttributed: false,
                    Reason: "No matching affiliate found for referral code or tracking slug",
                    DirectAffiliateId: null,
                    CreatedCommissions: new List<CommissionTransaction>(),
                    TotalCommissionsAwarded: 0m));
            }

            if (directPartner.Status != AffiliateStatus.Active)
            {
                return Task.FromResult(new ConversionResult(
                    IsAttributed: false,
                    Reason: $"Affiliate partner is {directPartner.Status}",
                    DirectAffiliateId: directPartner.Id,
                    CreatedCommissions: new List<CommissionTransaction>(),
                    TotalCommissionsAwarded: 0m));
            }

            // Verify attribution window if click timestamp provided
            var effectiveOrderTime = request.OrderTimestampUtc ?? DateTime.UtcNow;
            if (clickTime.HasValue && directPartner.AttributionWindowDays > 0)
            {
                var expirationTime = clickTime.Value.AddDays(directPartner.AttributionWindowDays);
                if (effectiveOrderTime > expirationTime)
                {
                    return Task.FromResult(new ConversionResult(
                        IsAttributed: false,
                        Reason: $"Conversion occurred outside attribution window of {directPartner.AttributionWindowDays} days",
                        DirectAffiliateId: directPartner.Id,
                        CreatedCommissions: new List<CommissionTransaction>(),
                        TotalCommissionsAwarded: 0m));
                }
            }

            // Check Minimum Order Threshold
            if (directPartner.CommissionRule.MinimumOrderThreshold > 0 &&
                request.OrderGmv < directPartner.CommissionRule.MinimumOrderThreshold)
            {
                return Task.FromResult(new ConversionResult(
                    IsAttributed: false,
                    Reason: $"Order GMV ${request.OrderGmv:F2} is below minimum threshold ${directPartner.CommissionRule.MinimumOrderThreshold:F2}",
                    DirectAffiliateId: directPartner.Id,
                    CreatedCommissions: new List<CommissionTransaction>(),
                    TotalCommissionsAwarded: 0m));
            }

            // Check Product Category Exclusions
            if (!string.IsNullOrWhiteSpace(request.ProductCategory) &&
                directPartner.CommissionRule.ExcludedCategories.Contains(request.ProductCategory, StringComparer.OrdinalIgnoreCase))
            {
                return Task.FromResult(new ConversionResult(
                    IsAttributed: false,
                    Reason: $"Product category '{request.ProductCategory}' is excluded from commissions",
                    DirectAffiliateId: directPartner.Id,
                    CreatedCommissions: new List<CommissionTransaction>(),
                    TotalCommissionsAwarded: 0m));
            }

            var conversionId = Guid.NewGuid();
            var createdCommissions = new List<CommissionTransaction>();
            decimal orderMargin = Math.Max(0m, request.OrderGmv - request.CostOfGoodsSold);

            // Level 1 Commission
            decimal l1Amount = CalculateCommissionAmount(directPartner.CommissionRule, request.OrderGmv, orderMargin, request.IsRecurringSubscription);

            // Check Milestone Bonus for Direct Partner
            lock (directPartner.Metrics)
            {
                directPartner.Metrics.CumulativeSalesCount++;
                if (directPartner.CommissionRule.RuleType == CommissionRuleType.MilestoneBonus &&
                    directPartner.CommissionRule.MilestoneTargetSales > 0 &&
                    directPartner.Metrics.CumulativeSalesCount % directPartner.CommissionRule.MilestoneTargetSales == 0)
                {
                    l1Amount += directPartner.CommissionRule.MilestoneBonusAmount;
                }
            }

            var l1Tx = CreateCommissionTx(
                tenantId: request.TenantId,
                orderId: request.OrderId,
                conversionId: conversionId,
                affiliate: directPartner,
                tier: AffiliateTierLevel.Level1Direct,
                ruleType: directPartner.CommissionRule.RuleType,
                gmv: request.OrderGmv,
                margin: orderMargin,
                rate: directPartner.CommissionRule.RatePercentage,
                amount: l1Amount,
                holdbackDays: directPartner.EscrowHoldbackDays,
                createdAt: effectiveOrderTime,
                currency: request.Currency,
                notes: "Level 1 direct referral commission");

            _commissions[l1Tx.Id] = l1Tx;
            createdCommissions.Add(l1Tx);
            UpdatePartnerConversionMetrics(directPartner, request.OrderGmv, l1Amount, effectiveOrderTime);

            // Level 2 Parent Partner (if present)
            AffiliatePartner? l2Partner = null;
            if (directPartner.ParentPartnerId.HasValue &&
                _partners.TryGetValue(directPartner.ParentPartnerId.Value, out l2Partner) &&
                l2Partner.Status == AffiliateStatus.Active)
            {
                decimal l2Rate = directPartner.CommissionRule.Tier2RateOverride
                                 ?? l2Partner.CommissionRule.RatePercentage;
                if (l2Rate <= 0) l2Rate = 5.0m; // standard default L2 override

                decimal l2Amount = Math.Round(request.OrderGmv * (l2Rate / 100m), 2);

                var l2Tx = CreateCommissionTx(
                    tenantId: request.TenantId,
                    orderId: request.OrderId,
                    conversionId: conversionId,
                    affiliate: l2Partner,
                    tier: AffiliateTierLevel.Level2Parent,
                    ruleType: CommissionRuleType.PercentageOfGmv,
                    gmv: request.OrderGmv,
                    margin: orderMargin,
                    rate: l2Rate,
                    amount: l2Amount,
                    holdbackDays: l2Partner.EscrowHoldbackDays,
                    createdAt: effectiveOrderTime,
                    currency: request.Currency,
                    notes: $"Level 2 parent override from direct referrer '{directPartner.Name}'");

                _commissions[l2Tx.Id] = l2Tx;
                createdCommissions.Add(l2Tx);
                UpdatePartnerConversionMetrics(l2Partner, request.OrderGmv, l2Amount, effectiveOrderTime);

                // Level 3 Master Distributor (if present)
                if (l2Partner.ParentPartnerId.HasValue &&
                    _partners.TryGetValue(l2Partner.ParentPartnerId.Value, out var l3Partner) &&
                    l3Partner.Status == AffiliateStatus.Active)
                {
                    decimal l3Rate = directPartner.CommissionRule.Tier3RateOverride
                                     ?? l2Partner.CommissionRule.Tier3RateOverride
                                     ?? l3Partner.CommissionRule.RatePercentage;
                    if (l3Rate <= 0) l3Rate = 2.0m; // standard default L3 override

                    decimal l3Amount = Math.Round(request.OrderGmv * (l3Rate / 100m), 2);

                    var l3Tx = CreateCommissionTx(
                        tenantId: request.TenantId,
                        orderId: request.OrderId,
                        conversionId: conversionId,
                        affiliate: l3Partner,
                        tier: AffiliateTierLevel.Level3Master,
                        ruleType: CommissionRuleType.PercentageOfGmv,
                        gmv: request.OrderGmv,
                        margin: orderMargin,
                        rate: l3Rate,
                        amount: l3Amount,
                        holdbackDays: l3Partner.EscrowHoldbackDays,
                        createdAt: effectiveOrderTime,
                        currency: request.Currency,
                        notes: $"Level 3 master distributor override from agency '{l2Partner.Name}'");

                    _commissions[l3Tx.Id] = l3Tx;
                    createdCommissions.Add(l3Tx);
                    UpdatePartnerConversionMetrics(l3Partner, request.OrderGmv, l3Amount, effectiveOrderTime);
                }
            }

            decimal totalCommission = createdCommissions.Sum(c => c.CommissionAmount);

            return Task.FromResult(new ConversionResult(
                IsAttributed: true,
                Reason: "Conversion successfully attributed across affiliate hierarchy",
                DirectAffiliateId: directPartner.Id,
                CreatedCommissions: createdCommissions,
                TotalCommissionsAwarded: totalCommission));
        }
    }

    private static decimal CalculateCommissionAmount(CommissionRule rule, decimal gmv, decimal margin, bool isRecurring)
    {
        return rule.RuleType switch
        {
            CommissionRuleType.PercentageOfGmv => Math.Round(gmv * (rule.RatePercentage / 100m), 2),
            CommissionRuleType.PercentageOfMargin => Math.Round(Math.Max(0m, margin) * (rule.RatePercentage / 100m), 2),
            CommissionRuleType.FixedBountyPerOrder => Math.Round(rule.FixedAmount, 2),
            CommissionRuleType.RecurringSubscriptionSplit => Math.Round(gmv * (rule.RatePercentage / 100m), 2),
            CommissionRuleType.MilestoneBonus => Math.Round(gmv * (rule.RatePercentage / 100m), 2),
            _ => Math.Round(gmv * (rule.RatePercentage / 100m), 2)
        };
    }

    private static CommissionTransaction CreateCommissionTx(
        string tenantId,
        string orderId,
        Guid conversionId,
        AffiliatePartner affiliate,
        AffiliateTierLevel tier,
        CommissionRuleType ruleType,
        decimal gmv,
        decimal margin,
        decimal rate,
        decimal amount,
        int holdbackDays,
        DateTime createdAt,
        string currency,
        string notes)
    {
        return new CommissionTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            ConversionId = conversionId,
            AffiliateId = affiliate.Id,
            AffiliateName = affiliate.Name,
            TierLevel = tier,
            RuleType = ruleType,
            OrderGmv = gmv,
            OrderMargin = margin,
            CommissionRate = rate,
            CommissionAmount = amount,
            State = CommissionState.PendingEscrow,
            Currency = currency,
            CreatedAtUtc = createdAt,
            EscrowHoldbackDays = holdbackDays,
            MaturesAtUtc = createdAt.AddDays(holdbackDays),
            Notes = notes
        };
    }

    private static void UpdatePartnerConversionMetrics(AffiliatePartner partner, decimal gmv, decimal commission, DateTime orderTime)
    {
        lock (partner.Metrics)
        {
            partner.Metrics.TotalReferredGmv += gmv;
            partner.Metrics.TotalCommissionsEarned += commission;
            partner.Metrics.PendingEscrowAmount += commission;
            partner.Metrics.TotalConversions++;
            partner.Metrics.LastConversionAtUtc = orderTime;
        }
    }

    #endregion

    #region Anti-Fraud Escrow, Maturation & Clawback

    public Task<int> ProcessMaturitySweepAsync(DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var cutoff = asOfUtc ?? DateTime.UtcNow;
        int transitionedCount = 0;

        lock (_syncRoot)
        {
            foreach (var tx in _commissions.Values)
            {
                if (tx.State == CommissionState.PendingEscrow && tx.MaturesAtUtc <= cutoff)
                {
                    tx.State = CommissionState.Approved;
                    tx.ApprovedAtUtc = cutoff;
                    transitionedCount++;

                    // Update partner metrics: move from Pending to Approved
                    if (_partners.TryGetValue(tx.AffiliateId, out var partner))
                    {
                        lock (partner.Metrics)
                        {
                            partner.Metrics.PendingEscrowAmount = Math.Max(0m, partner.Metrics.PendingEscrowAmount - tx.CommissionAmount);
                            partner.Metrics.ApprovedAvailableAmount += tx.CommissionAmount;
                        }
                    }
                }
            }
        }

        return Task.FromResult(transitionedCount);
    }

    public Task<IReadOnlyList<CommissionTransaction>> ReverseCommissionAsync(ReverseCommissionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.OrderId) && !request.CommissionId.HasValue)
            throw new ArgumentException("Either OrderId or CommissionId must be specified for reversal.", nameof(request));

        var reversedList = new List<CommissionTransaction>();
        var now = DateTime.UtcNow;

        lock (_syncRoot)
        {
            var targets = _commissions.Values.Where(tx =>
                (request.CommissionId.HasValue && tx.Id == request.CommissionId.Value) ||
                (!string.IsNullOrWhiteSpace(request.OrderId) && tx.OrderId.Equals(request.OrderId, StringComparison.OrdinalIgnoreCase))
            ).ToList();

            foreach (var tx in targets)
            {
                // Can reverse if in PendingEscrow or Approved (clawback before final payout)
                if (tx.State == CommissionState.PendingEscrow || tx.State == CommissionState.Approved)
                {
                    var previousState = tx.State;
                    tx.State = CommissionState.RefundReversed;
                    tx.ReversalReason = request.Reason;
                    tx.ReversedAtUtc = now;
                    reversedList.Add(tx);

                    // Revert partner metrics
                    if (_partners.TryGetValue(tx.AffiliateId, out var partner))
                    {
                        lock (partner.Metrics)
                        {
                            if (previousState == CommissionState.PendingEscrow)
                                partner.Metrics.PendingEscrowAmount = Math.Max(0m, partner.Metrics.PendingEscrowAmount - tx.CommissionAmount);
                            else if (previousState == CommissionState.Approved)
                                partner.Metrics.ApprovedAvailableAmount = Math.Max(0m, partner.Metrics.ApprovedAvailableAmount - tx.CommissionAmount);

                            partner.Metrics.RefundReversalCount++;
                            partner.Metrics.RefundReversalAmount += tx.CommissionAmount;
                        }
                    }
                }
            }
        }

        return Task.FromResult<IReadOnlyList<CommissionTransaction>>(reversedList);
    }

    public Task<IReadOnlyList<CommissionTransaction>> GetCommissionsAsync(
        string tenantId = "tenant-default",
        CommissionState? status = null,
        AffiliateTierLevel? tier = null,
        Guid? affiliateId = null,
        string? orderId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var query = _commissions.Values.Where(c => c.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));

        if (status.HasValue)
            query = query.Where(c => c.State == status.Value);

        if (tier.HasValue)
            query = query.Where(c => c.TierLevel == tier.Value);

        if (affiliateId.HasValue)
            query = query.Where(c => c.AffiliateId == affiliateId.Value);

        if (!string.IsNullOrWhiteSpace(orderId))
            query = query.Where(c => c.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase));

        if (fromDate.HasValue)
            query = query.Where(c => c.CreatedAtUtc >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(c => c.CreatedAtUtc <= toDate.Value);

        return Task.FromResult<IReadOnlyList<CommissionTransaction>>(
            query.OrderByDescending(c => c.CreatedAtUtc).ToList());
    }

    public Task<CommissionTransaction?> GetCommissionByIdAsync(Guid id, CancellationToken ct = default)
    {
        _commissions.TryGetValue(id, out var tx);
        return Task.FromResult(tx);
    }

    #endregion

    #region Batch Payout Settlement Generator

    public Task<SettlementBatch> GeneratePayoutBatchAsync(GenerateSettlementBatchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_settlementLock)
        {
            // First run maturity sweep to ensure all due commissions are in Approved status
            ProcessMaturitySweepAsync(DateTime.UtcNow, ct).GetAwaiter().GetResult();

            // Find all Approved commissions matching tenant and optional affiliate filter
            var eligibleCommissions = _commissions.Values
                .Where(c => c.TenantId.Equals(request.TenantId, StringComparison.OrdinalIgnoreCase) &&
                            c.State == CommissionState.Approved &&
                            !c.SettlementBatchId.HasValue)
                .ToList();

            if (request.SpecificAffiliateIds != null && request.SpecificAffiliateIds.Count > 0)
            {
                eligibleCommissions = eligibleCommissions
                    .Where(c => request.SpecificAffiliateIds.Contains(c.AffiliateId))
                    .ToList();
            }

            // Group by Affiliate
            var groupedByAffiliate = eligibleCommissions
                .GroupBy(c => c.AffiliateId)
                .ToList();

            var lineItems = new List<SettlementLineItem>();
            var includedCommissions = new List<CommissionTransaction>();
            decimal grossBatchTotal = 0m;
            decimal totalTaxWithheld = 0m;
            decimal netBatchTotal = 0m;

            var batchId = Guid.NewGuid();
            var batchNumber = $"SETTLE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            var now = DateTime.UtcNow;

            foreach (var group in groupedByAffiliate)
            {
                if (!_partners.TryGetValue(group.Key, out var partner))
                    continue;

                // Check payout method filter
                if (request.PayoutMethodFilter.HasValue &&
                    partner.PayoutDetails.PreferredMethod != request.PayoutMethodFilter.Value)
                    continue;

                decimal grossAmount = group.Sum(c => c.CommissionAmount);

                // Threshold check: Must meet requested threshold or partner's threshold
                decimal effectiveThreshold = Math.Max(request.MinPayoutThreshold, partner.MinimumPayoutThreshold);
                if (grossAmount < effectiveThreshold)
                    continue;

                // Tax withholding calculation
                decimal withholdingRate = partner.TaxDetails.WithholdingPercentage;
                if (partner.TaxDetails.FormType == TaxFormType.W9 && partner.TaxDetails.IsVerified)
                {
                    withholdingRate = 0m; // US resident with verified W-9 has 0% backup withholding
                }
                else if (partner.TaxDetails.FormType == TaxFormType.W8Ben && withholdingRate <= 0)
                {
                    withholdingRate = 10.0m; // Default treaty rate for W-8BEN
                }

                decimal taxWithheld = Math.Round(grossAmount * (withholdingRate / 100m), 2);
                decimal netAmount = grossAmount - taxWithheld;

                string destRef = partner.PayoutDetails.PreferredMethod switch
                {
                    PayoutMethod.Sepa => partner.PayoutDetails.Iban ?? "IBAN_NOT_SET",
                    PayoutMethod.PayPal => partner.PayoutDetails.PayPalEmail ?? partner.Email,
                    PayoutMethod.Crypto => $"{partner.PayoutDetails.CryptoChain}:{partner.PayoutDetails.CryptoWalletAddress}",
                    _ => partner.PayoutDetails.AccountHolderName ?? partner.Name
                };

                var itemCommissionIds = group.Select(c => c.Id).ToList();

                var lineItem = new SettlementLineItem
                {
                    LineItemId = Guid.NewGuid(),
                    AffiliateId = partner.Id,
                    AffiliateName = partner.Name,
                    RecipientEmail = partner.Email,
                    Method = partner.PayoutDetails.PreferredMethod,
                    GrossAmount = grossAmount,
                    TaxWithheld = taxWithheld,
                    NetAmount = netAmount,
                    DestinationReference = destRef,
                    SwiftBic = partner.PayoutDetails.SwiftBic,
                    CommissionIds = itemCommissionIds
                };

                lineItems.Add(lineItem);
                grossBatchTotal += grossAmount;
                totalTaxWithheld += taxWithheld;
                netBatchTotal += netAmount;

                // Mark included commissions as PaidOut
                foreach (var tx in group)
                {
                    tx.State = CommissionState.PaidOut;
                    tx.SettlementBatchId = batchId;
                    tx.PaidOutAtUtc = now;
                    includedCommissions.Add(tx);
                }

                // Update partner metrics
                lock (partner.Metrics)
                {
                    partner.Metrics.ApprovedAvailableAmount = Math.Max(0m, partner.Metrics.ApprovedAvailableAmount - grossAmount);
                    partner.Metrics.TotalCommissionsPaid += netAmount;
                }
            }

            var primaryMethod = request.PayoutMethodFilter ??
                                (lineItems.Count > 0 ? lineItems[0].Method : PayoutMethod.Sepa);

            var batch = new SettlementBatch
            {
                Id = batchId,
                TenantId = request.TenantId,
                BatchNumber = batchNumber,
                CreatedAtUtc = now,
                GrossTotal = grossBatchTotal,
                TotalTaxWithheld = totalTaxWithheld,
                NetTotal = netBatchTotal,
                AffiliateCount = lineItems.Count,
                CommissionCount = includedCommissions.Count,
                Currency = request.Currency,
                PayoutMethod = primaryMethod,
                LineItems = lineItems
            };

            // Generate Manifests
            batch.SepaXmlManifest = GenerateSepaXmlManifest(batch, lineItems.Where(l => l.Method == PayoutMethod.Sepa).ToList());
            batch.PayPalCsvManifest = GeneratePayPalCsvManifest(batch, lineItems.Where(l => l.Method == PayoutMethod.PayPal).ToList());
            batch.WireCryptoLedgerManifest = GenerateWireCryptoLedgerManifest(batch, lineItems.Where(l => l.Method == PayoutMethod.Wire || l.Method == PayoutMethod.Crypto).ToList());

            _batches[batchId] = batch;
            return Task.FromResult(batch);
        }
    }

    public Task<IReadOnlyList<SettlementBatch>> GetSettlementBatchesAsync(string tenantId = "tenant-default", CancellationToken ct = default)
    {
        var list = _batches.Values
            .Where(b => b.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<SettlementBatch>>(list);
    }

    public Task<SettlementBatch?> GetSettlementBatchByIdAsync(Guid id, CancellationToken ct = default)
    {
        _batches.TryGetValue(id, out var batch);
        return Task.FromResult(batch);
    }

    public Task<string?> GetBatchManifestAsync(Guid batchId, string? format = null, CancellationToken ct = default)
    {
        if (!_batches.TryGetValue(batchId, out var batch))
            return Task.FromResult<string?>(null);

        var fmt = (format ?? batch.PayoutMethod.ToString()).ToLowerInvariant();

        if (fmt.Contains("sepa") || fmt.Contains("xml"))
            return Task.FromResult(batch.SepaXmlManifest);

        if (fmt.Contains("paypal") || fmt.Contains("csv"))
            return Task.FromResult(batch.PayPalCsvManifest);

        return Task.FromResult(batch.WireCryptoLedgerManifest ?? batch.SepaXmlManifest ?? batch.PayPalCsvManifest);
    }

    #endregion

    #region Manifest Generation (SEPA XML, PayPal CSV, Ledger)

    public static string GenerateSepaXmlManifest(SettlementBatch batch, List<SettlementLineItem> sepaItems)
    {
        XNamespace ns = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.03";
        var creationDt = batch.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var executionDt = batch.CreatedAtUtc.ToString("yyyy-MM-dd");
        decimal sum = sepaItems.Sum(i => i.NetAmount);

        var cdtTrfTxInfElements = sepaItems.Select(item =>
            new XElement(ns + "CdtTrfTxInf",
                new XElement(ns + "PmtId",
                    new XElement(ns + "EndToEndId", $"SETTLE-{item.LineItemId.ToString("N")[..12].ToUpperInvariant()}")
                ),
                new XElement(ns + "Amt",
                    new XElement(ns + "InstdAmt",
                        new XAttribute("Ccy", batch.Currency == "USD" ? "EUR" : batch.Currency),
                        item.NetAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))
                ),
                new XElement(ns + "CdtrAgt",
                    new XElement(ns + "FinInstnId",
                        new XElement(ns + "BIC", string.IsNullOrWhiteSpace(item.SwiftBic) ? "DBEUMM21XXX" : item.SwiftBic))
                ),
                new XElement(ns + "Cdtr",
                    new XElement(ns + "Nm", item.AffiliateName)
                ),
                new XElement(ns + "CdtrAcct",
                    new XElement(ns + "Id",
                        new XElement(ns + "IBAN", item.DestinationReference.Replace(" ", "").ToUpperInvariant()))
                ),
                new XElement(ns + "RmtInf",
                    new XElement(ns + "Ustrd", $"B2B Commission Settlement {batch.BatchNumber}")
                )
            )
        );

        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement(ns + "Document",
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                new XElement(ns + "CstmrCdtTrfInitn",
                    new XElement(ns + "GrpHdr",
                        new XElement(ns + "MsgId", $"MSG-{batch.BatchNumber}"),
                        new XElement(ns + "CreDtTm", creationDt),
                        new XElement(ns + "NbOfTxs", sepaItems.Count),
                        new XElement(ns + "CtrlSum", sum.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(ns + "InitgPty",
                            new XElement(ns + "Nm", "SGrowing B2B Settlement Engine")
                        )
                    ),
                    new XElement(ns + "PmtInf",
                        new XElement(ns + "PmtInfId", $"PMT-INF-{batch.BatchNumber}"),
                        new XElement(ns + "PmtMtd", "TRF"),
                        new XElement(ns + "BtchBookg", "true"),
                        new XElement(ns + "NbOfTxs", sepaItems.Count),
                        new XElement(ns + "CtrlSum", sum.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
                        new XElement(ns + "PmtTpInf",
                            new XElement(ns + "SvcLvl",
                                new XElement(ns + "Cd", "SEPA")
                            )
                        ),
                        new XElement(ns + "ReqdExctnDt", executionDt),
                        new XElement(ns + "Dbtr",
                            new XElement(ns + "Nm", "SGrowing Global Commerce Treasury")
                        ),
                        new XElement(ns + "DbtrAcct",
                            new XElement(ns + "Id",
                                new XElement(ns + "IBAN", "DE89370400440532013000")
                            )
                        ),
                        new XElement(ns + "DbtrAgt",
                            new XElement(ns + "FinInstnId",
                                new XElement(ns + "BIC", "DBEUMM21XXX")
                            )
                        ),
                        new XElement(ns + "ChrgBr", "SLEV"),
                        cdtTrfTxInfElements
                    )
                )
            )
        );

        return doc.Declaration + Environment.NewLine + doc.ToString();
    }

    public static string GeneratePayPalCsvManifest(SettlementBatch batch, List<SettlementLineItem> payPalItems)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RecipientEmail,Amount,Currency,Note,CustomId");

        foreach (var item in payPalItems)
        {
            var email = item.DestinationReference.Contains('@') ? item.DestinationReference : item.RecipientEmail;
            var amountStr = item.NetAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            var note = $"B2B Commission Payout - {batch.BatchNumber}";
            var customId = item.AffiliateId.ToString();
            sb.AppendLine($"\"{email}\",{amountStr},{batch.Currency},\"{note}\",\"{customId}\"");
        }

        return sb.ToString();
    }

    public static string GenerateWireCryptoLedgerManifest(SettlementBatch batch, List<SettlementLineItem> ledgerItems)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LineItemId,AffiliateId,AffiliateName,PayoutMethod,GrossAmount,TaxWithheld,NetAmount,Currency,DestinationAddress,SwiftBic,BatchNumber");

        foreach (var item in ledgerItems)
        {
            sb.AppendLine(string.Join(",",
                item.LineItemId,
                item.AffiliateId,
                $"\"{item.AffiliateName}\"",
                item.Method,
                item.GrossAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                item.TaxWithheld.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                item.NetAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                batch.Currency,
                $"\"{item.DestinationReference}\"",
                $"\"{item.SwiftBic ?? "N/A"}\"",
                batch.BatchNumber));
        }

        return sb.ToString();
    }

    #endregion

    #region Performance Analytics

    public Task<AffiliateCommissionMetrics> GetMetricsAsync(string tenantId = "tenant-default", CancellationToken ct = default)
    {
        var partners = _partners.Values.Where(p => p.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)).ToList();
        var commissions = _commissions.Values.Where(c => c.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)).ToList();

        decimal totalReferredGmv = commissions.Where(c => c.TierLevel == AffiliateTierLevel.Level1Direct).Sum(c => c.OrderGmv);
        decimal totalCommissionsAccrued = commissions.Sum(c => c.CommissionAmount);
        decimal pendingEscrowVolume = commissions.Where(c => c.State == CommissionState.PendingEscrow).Sum(c => c.CommissionAmount);
        decimal approvedAvailableVolume = commissions.Where(c => c.State == CommissionState.Approved).Sum(c => c.CommissionAmount);
        decimal totalPaidOut = commissions.Where(c => c.State == CommissionState.PaidOut).Sum(c => c.CommissionAmount);
        decimal refundReversedVolume = commissions.Where(c => c.State == CommissionState.RefundReversed).Sum(c => c.CommissionAmount);

        int activeAffiliatesCount = partners.Count(p => p.Status == AffiliateStatus.Active);
        int totalConversionsCount = commissions.Select(c => c.ConversionId).Distinct().Count();
        decimal avgOrderValue = totalConversionsCount > 0 ? Math.Round(totalReferredGmv / totalConversionsCount, 2) : 0m;

        var topAffiliates = partners
            .OrderByDescending(p => p.Metrics.TotalReferredGmv)
            .Take(5)
            .Select(p => new TopAffiliateDto(
                p.Id,
                p.Name,
                p.TierLevel,
                p.Metrics.TotalReferredGmv,
                p.Metrics.TotalCommissionsEarned,
                p.Metrics.TotalConversions))
            .ToList();

        var commissionsByTier = commissions
            .GroupBy(c => (int)c.TierLevel)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.CommissionAmount));

        var commissionsByStatus = commissions
            .GroupBy(c => c.State.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var metrics = new AffiliateCommissionMetrics(
            TotalReferredGmv: totalReferredGmv,
            TotalCommissionsAccrued: totalCommissionsAccrued,
            PendingEscrowVolume: pendingEscrowVolume,
            ApprovedAvailableVolume: approvedAvailableVolume,
            TotalPaidOut: totalPaidOut,
            RefundReversedVolume: refundReversedVolume,
            ActiveAffiliatesCount: activeAffiliatesCount,
            TotalConversionsCount: totalConversionsCount,
            AverageOrderValue: avgOrderValue,
            TopAffiliates: topAffiliates,
            CommissionsByTier: commissionsByTier,
            CommissionsByStatus: commissionsByStatus);

        return Task.FromResult(metrics);
    }

    #endregion

    #region Pre-seeded Data & Reset

    public Task ResetToPreseededDataAsync(CancellationToken ct = default)
    {
        lock (_syncRoot)
        {
            _partners.Clear();
            _commissions.Clear();
            _batches.Clear();
            _cookies.Clear();
            _codeToPartnerId.Clear();
            _slugToPartnerId.Clear();
            _domainToPartnerId.Clear();

            SeedInitialData();
        }

        return Task.CompletedTask;
    }

    private void SeedInitialData()
    {
        // 1. Level 3 Master Partner
        var l3Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var l3 = new AffiliatePartner
        {
            Id = l3Id,
            TenantId = "tenant-default",
            Name = "Global Scale Distribution Hub",
            Email = "finance@globalscalehub.eu",
            TierLevel = AffiliateTierLevel.Level3Master,
            ParentPartnerId = null,
            Status = AffiliateStatus.Active,
            ReferralCodes = new List<string> { "GLOBALHUB", "DISTMASTER" },
            TrackingSlug = "global-dist",
            CustomLandingDomain = "partners.globalscalehub.eu",
            AttributionWindowDays = 90,
            EscrowHoldbackDays = 14,
            MinimumPayoutThreshold = 100.0m,
            CommissionRule = new CommissionRule
            {
                RuleType = CommissionRuleType.PercentageOfGmv,
                RatePercentage = 2.0m
            },
            PayoutDetails = new PayoutDetails
            {
                PreferredMethod = PayoutMethod.Sepa,
                AccountHolderName = "Global Scale Distribution GmbH",
                Iban = "DE89370400440532013000",
                SwiftBic = "DBEUMM21XXX",
                BankName = "Deutsche Bank AG",
                Currency = "EUR"
            },
            TaxDetails = new TaxDetails
            {
                FormType = TaxFormType.W8Ben,
                TaxIdOrSsn = "DE-994827104",
                WithholdingPercentage = 10.0m,
                IsVerified = true
            },
            CreatedAtUtc = DateTime.UtcNow.AddDays(-60),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-60)
        };

        // 2. Level 2 Parent Agency
        var l2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var l2 = new AffiliatePartner
        {
            Id = l2Id,
            TenantId = "tenant-default",
            Name = "Nordic Growth Media Group",
            Email = "affiliates@nordicgrowthmedia.se",
            TierLevel = AffiliateTierLevel.Level2Parent,
            ParentPartnerId = l3Id,
            Status = AffiliateStatus.Active,
            ReferralCodes = new List<string> { "NORDIC5", "AGENCYHUB" },
            TrackingSlug = "nordic-media",
            CustomLandingDomain = "go.nordicgrowthmedia.se",
            AttributionWindowDays = 60,
            EscrowHoldbackDays = 14,
            MinimumPayoutThreshold = 50.0m,
            CommissionRule = new CommissionRule
            {
                RuleType = CommissionRuleType.PercentageOfGmv,
                RatePercentage = 5.0m
            },
            PayoutDetails = new PayoutDetails
            {
                PreferredMethod = PayoutMethod.Sepa,
                AccountHolderName = "Nordic Growth Media AB",
                Iban = "SE4550000000058398257461",
                SwiftBic = "ESSESTMMXXX",
                BankName = "Skandinaviska Enskilda Banken",
                Currency = "EUR"
            },
            TaxDetails = new TaxDetails
            {
                FormType = TaxFormType.W8Ben,
                TaxIdOrSsn = "SE-5561234567",
                WithholdingPercentage = 10.0m,
                IsVerified = true
            },
            CreatedAtUtc = DateTime.UtcNow.AddDays(-50),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-50)
        };

        // 3. Level 1 Direct Affiliate 1: Apex SaaS Reviews
        var l1ApexId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var l1Apex = new AffiliatePartner
        {
            Id = l1ApexId,
            TenantId = "tenant-default",
            Name = "Apex SaaS Reviews",
            Email = "partners@apexsaasreviews.com",
            TierLevel = AffiliateTierLevel.Level1Direct,
            ParentPartnerId = l2Id,
            Status = AffiliateStatus.Active,
            ReferralCodes = new List<string> { "APEX15", "APEXDEALS" },
            TrackingSlug = "apex-saas",
            CustomLandingDomain = "deals.apexsaasreviews.com",
            AttributionWindowDays = 30,
            EscrowHoldbackDays = 14,
            MinimumPayoutThreshold = 50.0m,
            CommissionRule = new CommissionRule
            {
                RuleType = CommissionRuleType.PercentageOfGmv,
                RatePercentage = 15.0m,
                MinimumOrderThreshold = 25.0m,
                ExcludedCategories = new List<string> { "GiftCards", "HardwareClearance" }
            },
            PayoutDetails = new PayoutDetails
            {
                PreferredMethod = PayoutMethod.PayPal,
                PayPalEmail = "billing@apexsaasreviews.com",
                Currency = "USD"
            },
            TaxDetails = new TaxDetails
            {
                FormType = TaxFormType.W9,
                TaxIdOrSsn = "98-7654321",
                WithholdingPercentage = 0.0m,
                IsVerified = true
            },
            CreatedAtUtc = DateTime.UtcNow.AddDays(-40),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-40)
        };

        // 4. Level 1 Direct Affiliate 2: Venture Tech Media
        var l1VentureId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var l1Venture = new AffiliatePartner
        {
            Id = l1VentureId,
            TenantId = "tenant-default",
            Name = "Venture Tech Media",
            Email = "payouts@venturetechmedia.io",
            TierLevel = AffiliateTierLevel.Level1Direct,
            ParentPartnerId = l2Id,
            Status = AffiliateStatus.Active,
            ReferralCodes = new List<string> { "VENTURE20", "B2BDEALS" },
            TrackingSlug = "venture-tech",
            CustomLandingDomain = "offers.venturetechmedia.io",
            AttributionWindowDays = 45,
            EscrowHoldbackDays = 14,
            MinimumPayoutThreshold = 75.0m,
            CommissionRule = new CommissionRule
            {
                RuleType = CommissionRuleType.PercentageOfMargin,
                RatePercentage = 20.0m
            },
            PayoutDetails = new PayoutDetails
            {
                PreferredMethod = PayoutMethod.Wire,
                AccountHolderName = "Venture Tech Media LLC",
                BankName = "JPMorgan Chase Bank, N.A.",
                Currency = "USD"
            },
            TaxDetails = new TaxDetails
            {
                FormType = TaxFormType.W9,
                TaxIdOrSsn = "12-3456789",
                WithholdingPercentage = 0.0m,
                IsVerified = true
            },
            CreatedAtUtc = DateTime.UtcNow.AddDays(-35),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-35)
        };

        // 5. Level 1 Direct Independent Affiliate: Cloud Stack Creator
        var l1CloudId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var l1Cloud = new AffiliatePartner
        {
            Id = l1CloudId,
            TenantId = "tenant-default",
            Name = "Cloud Stack Creator",
            Email = "alex@cloudstackcreator.com",
            TierLevel = AffiliateTierLevel.Level1Direct,
            ParentPartnerId = null, // Independent
            Status = AffiliateStatus.Active,
            ReferralCodes = new List<string> { "CLOUDSTACK", "ALEX100" },
            TrackingSlug = "cloud-stack",
            AttributionWindowDays = 0, // Lifetime attribution
            EscrowHoldbackDays = 14,
            MinimumPayoutThreshold = 50.0m,
            CommissionRule = new CommissionRule
            {
                RuleType = CommissionRuleType.FixedBountyPerOrder,
                FixedAmount = 100.0m
            },
            PayoutDetails = new PayoutDetails
            {
                PreferredMethod = PayoutMethod.Crypto,
                CryptoChain = "Ethereum",
                CryptoWalletAddress = "0x71C8366420A0927156652b1f02c78568F8E24b84",
                Currency = "USD"
            },
            TaxDetails = new TaxDetails
            {
                FormType = TaxFormType.W8Ben,
                TaxIdOrSsn = "CA-88291047",
                WithholdingPercentage = 10.0m,
                IsVerified = true
            },
            CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-30)
        };

        AddPartnerInternal(l3);
        AddPartnerInternal(l2);
        AddPartnerInternal(l1Apex);
        AddPartnerInternal(l1Venture);
        AddPartnerInternal(l1Cloud);

        // Pre-seed Historical Commissions across states
        SeedHistoricalTransactions(l1Apex, l2, l3, l1Venture, l1Cloud);
    }

    private void AddPartnerInternal(AffiliatePartner partner)
    {
        _partners[partner.Id] = partner;
        foreach (var code in partner.ReferralCodes)
            _codeToPartnerId[code] = partner.Id;
        if (!string.IsNullOrWhiteSpace(partner.TrackingSlug))
            _slugToPartnerId[partner.TrackingSlug] = partner.Id;
        if (!string.IsNullOrWhiteSpace(partner.CustomLandingDomain))
            _domainToPartnerId[partner.CustomLandingDomain] = partner.Id;
    }

    private void SeedHistoricalTransactions(
        AffiliatePartner l1Apex,
        AffiliatePartner l2,
        AffiliatePartner l3,
        AffiliatePartner l1Venture,
        AffiliatePartner l1Cloud)
    {
        var now = DateTime.UtcNow;

        // 1. Settled Historical Order (30 days ago) -> Status: PaidOut
        var conv1Id = Guid.NewGuid();
        var t1Apex = new CommissionTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-2026-9011",
            ConversionId = conv1Id,
            AffiliateId = l1Apex.Id,
            AffiliateName = l1Apex.Name,
            TierLevel = AffiliateTierLevel.Level1Direct,
            RuleType = CommissionRuleType.PercentageOfGmv,
            OrderGmv = 1000.0m,
            OrderMargin = 400.0m,
            CommissionRate = 15.0m,
            CommissionAmount = 150.0m,
            State = CommissionState.PaidOut,
            CreatedAtUtc = now.AddDays(-30),
            EscrowHoldbackDays = 14,
            MaturesAtUtc = now.AddDays(-16),
            ApprovedAtUtc = now.AddDays(-16),
            PaidOutAtUtc = now.AddDays(-10),
            Notes = "Settled in Batch SETTLE-202610-001"
        };
        var t1L2 = new CommissionTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-2026-9011",
            ConversionId = conv1Id,
            AffiliateId = l2.Id,
            AffiliateName = l2.Name,
            TierLevel = AffiliateTierLevel.Level2Parent,
            RuleType = CommissionRuleType.PercentageOfGmv,
            OrderGmv = 1000.0m,
            OrderMargin = 400.0m,
            CommissionRate = 5.0m,
            CommissionAmount = 50.0m,
            State = CommissionState.PaidOut,
            CreatedAtUtc = now.AddDays(-30),
            EscrowHoldbackDays = 14,
            MaturesAtUtc = now.AddDays(-16),
            ApprovedAtUtc = now.AddDays(-16),
            PaidOutAtUtc = now.AddDays(-10),
            Notes = "Settled in Batch SETTLE-202610-001"
        };
        var t1L3 = new CommissionTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-2026-9011",
            ConversionId = conv1Id,
            AffiliateId = l3.Id,
            AffiliateName = l3.Name,
            TierLevel = AffiliateTierLevel.Level3Master,
            RuleType = CommissionRuleType.PercentageOfGmv,
            OrderGmv = 1000.0m,
            OrderMargin = 400.0m,
            CommissionRate = 2.0m,
            CommissionAmount = 20.0m,
            State = CommissionState.PaidOut,
            CreatedAtUtc = now.AddDays(-30),
            EscrowHoldbackDays = 14,
            MaturesAtUtc = now.AddDays(-16),
            ApprovedAtUtc = now.AddDays(-16),
            PaidOutAtUtc = now.AddDays(-10),
            Notes = "Settled in Batch SETTLE-202610-001"
        };

        _commissions[t1Apex.Id] = t1Apex;
        _commissions[t1L2.Id] = t1L2;
        _commissions[t1L3.Id] = t1L3;

        // Archive a historical settlement batch for this
        var histBatch = new SettlementBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = "SETTLE-202610-001",
            CreatedAtUtc = now.AddDays(-10),
            GrossTotal = 220.0m,
            TotalTaxWithheld = 7.0m,
            NetTotal = 213.0m,
            AffiliateCount = 3,
            CommissionCount = 3,
            Currency = "USD",
            PayoutMethod = PayoutMethod.PayPal,
            LineItems = new List<SettlementLineItem>
            {
                new() { LineItemId = Guid.NewGuid(), AffiliateId = l1Apex.Id, AffiliateName = l1Apex.Name, RecipientEmail = l1Apex.Email, Method = PayoutMethod.PayPal, GrossAmount = 150.0m, TaxWithheld = 0m, NetAmount = 150.0m, DestinationReference = l1Apex.PayoutDetails.PayPalEmail!, CommissionIds = new() { t1Apex.Id } },
                new() { LineItemId = Guid.NewGuid(), AffiliateId = l2.Id, AffiliateName = l2.Name, RecipientEmail = l2.Email, Method = PayoutMethod.Sepa, GrossAmount = 50.0m, TaxWithheld = 5.0m, NetAmount = 45.0m, DestinationReference = l2.PayoutDetails.Iban!, CommissionIds = new() { t1L2.Id } },
                new() { LineItemId = Guid.NewGuid(), AffiliateId = l3.Id, AffiliateName = l3.Name, RecipientEmail = l3.Email, Method = PayoutMethod.Sepa, GrossAmount = 20.0m, TaxWithheld = 2.0m, NetAmount = 18.0m, DestinationReference = l3.PayoutDetails.Iban!, CommissionIds = new() { t1L3.Id } }
            }
        };
        histBatch.SepaXmlManifest = GenerateSepaXmlManifest(histBatch, histBatch.LineItems.Where(i => i.Method == PayoutMethod.Sepa).ToList());
        histBatch.PayPalCsvManifest = GeneratePayPalCsvManifest(histBatch, histBatch.LineItems.Where(i => i.Method == PayoutMethod.PayPal).ToList());
        _batches[histBatch.Id] = histBatch;

        // Update partner metrics for settled
        l1Apex.Metrics.TotalReferredGmv += 1000m;
        l1Apex.Metrics.TotalCommissionsEarned += 150m;
        l1Apex.Metrics.TotalCommissionsPaid += 150m;
        l1Apex.Metrics.TotalConversions++;

        l2.Metrics.TotalReferredGmv += 1000m;
        l2.Metrics.TotalCommissionsEarned += 50m;
        l2.Metrics.TotalCommissionsPaid += 45m;
        l2.Metrics.TotalConversions++;

        l3.Metrics.TotalReferredGmv += 1000m;
        l3.Metrics.TotalCommissionsEarned += 20m;
        l3.Metrics.TotalCommissionsPaid += 18m;
        l3.Metrics.TotalConversions++;

        // 2. Matured Order (20 days ago) -> Status: Approved (Matured 6 days ago, ready for next payout batch)
        var conv2Id = Guid.NewGuid();
        var t2Apex = new CommissionTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-2026-9042",
            ConversionId = conv2Id,
            AffiliateId = l1Apex.Id,
            AffiliateName = l1Apex.Name,
            TierLevel = AffiliateTierLevel.Level1Direct,
            RuleType = CommissionRuleType.PercentageOfGmv,
            OrderGmv = 800.0m,
            OrderMargin = 320.0m,
            CommissionRate = 15.0m,
            CommissionAmount = 120.0m,
            State = CommissionState.Approved,
            CreatedAtUtc = now.AddDays(-20),
            EscrowHoldbackDays = 14,
            MaturesAtUtc = now.AddDays(-6),
            ApprovedAtUtc = now.AddDays(-6),
            Notes = "Matured from holdback, approved for settlement"
        };
        var t2L2 = new CommissionTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-2026-9042",
            ConversionId = conv2Id,
            AffiliateId = l2.Id,
            AffiliateName = l2.Name,
            TierLevel = AffiliateTierLevel.Level2Parent,
            RuleType = CommissionRuleType.PercentageOfGmv,
            OrderGmv = 800.0m,
            OrderMargin = 320.0m,
            CommissionRate = 5.0m,
            CommissionAmount = 40.0m,
            State = CommissionState.Approved,
            CreatedAtUtc = now.AddDays(-20),
            EscrowHoldbackDays = 14,
            MaturesAtUtc = now.AddDays(-6),
            ApprovedAtUtc = now.AddDays(-6)
        };
        _commissions[t2Apex.Id] = t2Apex;
        _commissions[t2L2.Id] = t2L2;

        l1Apex.Metrics.TotalReferredGmv += 800m;
        l1Apex.Metrics.TotalCommissionsEarned += 120m;
        l1Apex.Metrics.ApprovedAvailableAmount += 120m;
        l1Apex.Metrics.TotalConversions++;

        l2.Metrics.TotalReferredGmv += 800m;
        l2.Metrics.TotalCommissionsEarned += 40m;
        l2.Metrics.ApprovedAvailableAmount += 40m;
        l2.Metrics.TotalConversions++;

        // 3. Recent Order (3 days ago) -> Status: PendingEscrow (holding back for fraud/refunds)
        var conv3Id = Guid.NewGuid();
        var t3Venture = new CommissionTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-2026-9088",
            ConversionId = conv3Id,
            AffiliateId = l1Venture.Id,
            AffiliateName = l1Venture.Name,
            TierLevel = AffiliateTierLevel.Level1Direct,
            RuleType = CommissionRuleType.PercentageOfMargin,
            OrderGmv = 1500.0m,
            OrderMargin = 750.0m,
            CommissionRate = 20.0m,
            CommissionAmount = 150.0m,
            State = CommissionState.PendingEscrow,
            CreatedAtUtc = now.AddDays(-3),
            EscrowHoldbackDays = 14,
            MaturesAtUtc = now.AddDays(11),
            Notes = "Anti-fraud holdback active (11 days remaining)"
        };
        _commissions[t3Venture.Id] = t3Venture;
        l1Venture.Metrics.TotalReferredGmv += 1500m;
        l1Venture.Metrics.TotalCommissionsEarned += 150m;
        l1Venture.Metrics.PendingEscrowAmount += 150m;
        l1Venture.Metrics.TotalConversions++;

        // 4. Cancelled Order (8 days ago) -> Status: RefundReversed (clawed back)
        var conv4Id = Guid.NewGuid();
        var t4Cloud = new CommissionTransaction
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-2026-9030",
            ConversionId = conv4Id,
            AffiliateId = l1Cloud.Id,
            AffiliateName = l1Cloud.Name,
            TierLevel = AffiliateTierLevel.Level1Direct,
            RuleType = CommissionRuleType.FixedBountyPerOrder,
            OrderGmv = 350.0m,
            OrderMargin = 150.0m,
            CommissionRate = 100.0m,
            CommissionAmount = 100.0m,
            State = CommissionState.RefundReversed,
            CreatedAtUtc = now.AddDays(-8),
            EscrowHoldbackDays = 14,
            MaturesAtUtc = now.AddDays(6),
            ReversalReason = "Customer returned hardware items within 14-day warranty window",
            ReversedAtUtc = now.AddDays(-5),
            Notes = "Clawback reversed automatically on refund webhook"
        };
        _commissions[t4Cloud.Id] = t4Cloud;
        l1Cloud.Metrics.TotalReferredGmv += 350m;
        l1Cloud.Metrics.TotalCommissionsEarned += 100m;
        l1Cloud.Metrics.RefundReversalCount++;
        l1Cloud.Metrics.RefundReversalAmount += 100m;
        l1Cloud.Metrics.TotalConversions++;
    }

    private static string GenerateReferralCode(string name)
    {
        var cleaned = Regex.Replace(name, @"[^A-Za-z0-9]", "").ToUpperInvariant();
        if (cleaned.Length > 8)
            cleaned = cleaned[..8];
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "PARTNER";

        var suffix = RandomNumberGenerator.GetInt32(10, 99);
        return $"{cleaned}{suffix}";
    }

    private static string SanitizeCode(string input)
    {
        return Regex.Replace(input.Trim(), @"[^A-Za-z0-9_-]", "").ToUpperInvariant();
    }

    private static string GenerateSlug(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        var slug = Regex.Replace(lower, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "affiliate" : slug;
    }

    private static string SanitizeSlug(string input)
    {
        return Regex.Replace(input.Trim().ToLowerInvariant(), @"[^a-z0-9-]+", "-").Trim('-');
    }

    #endregion
}

#endregion
