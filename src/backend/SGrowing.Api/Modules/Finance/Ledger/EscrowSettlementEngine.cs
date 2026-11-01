using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.Finance.Ledger;

#region Escrow Models & DTOs

public enum EscrowStatus
{
    Created = 1,
    Locked = 2,
    Settled = 3,
    Refunded = 4,
    Disputed = 5
}

public sealed record SettlementSplitBreakdown(
    decimal GrossAmount,
    decimal MerchantNet,
    decimal CourierFee,
    decimal AffiliateCommission,
    decimal PlatformTakeRateFee,
    string Currency
);

public sealed record EscrowHoldRecord
{
    public Guid EscrowId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string MerchantId { get; init; } = string.Empty;
    public string CourierId { get; init; } = string.Empty;
    public string? AffiliateId { get; init; }
    public decimal GrossAmount { get; init; }
    public string Currency { get; init; } = "DZD";
    public EscrowStatus Status { get; init; } = EscrowStatus.Locked;
    public DateTimeOffset LockedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SettledAtUtc { get; init; }
    public DateTimeOffset? RefundedAtUtc { get; init; }
    public Guid LockJournalEntryId { get; init; }
    public Guid? SettlementJournalEntryId { get; init; }
    public Guid? RefundJournalEntryId { get; init; }
    public SettlementSplitBreakdown? SettlementSplit { get; init; }
    public string? Notes { get; init; }
}

public sealed record EscrowLockRequest(
    Guid TenantId,
    string OrderId,
    string CustomerId,
    string MerchantId,
    string CourierId,
    string? AffiliateId,
    decimal GrossAmount,
    string Currency = "DZD",
    string? Notes = null
);

public sealed record ProofOfDeliveryConfirmationRequest(
    Guid TenantId,
    string OrderId,
    string TrackingNumber,
    string ProofSignatureOrPin,
    DateTimeOffset DeliveredAtUtc,
    decimal CourierFee,
    decimal PlatformTakeRatePercentage, // e.g. 0.05 for 5%
    decimal AffiliateCommissionPercentage = 0m, // e.g. 0.07 for 7%
    string? Notes = null
);

public sealed record EscrowSettlementResult(
    bool Success,
    EscrowHoldRecord EscrowRecord,
    JournalEntry SettlementJournalEntry,
    SettlementSplitBreakdown SplitBreakdown
);

public sealed record EscrowRefundRequest(
    Guid TenantId,
    string OrderId,
    string Reason, // "CustomerRefusal", "RTO", "Cancelled"
    decimal ReturnShippingFee = 0m,
    string? Notes = null
);

public sealed record EscrowRefundResult(
    bool Success,
    EscrowHoldRecord EscrowRecord,
    JournalEntry RefundJournalEntry,
    string Reason
);

#endregion

#region Service Interface

public interface IEscrowSettlementEngine
{
    Task<EscrowHoldRecord> LockOnCheckoutAsync(EscrowLockRequest request, CancellationToken ct = default);
    Task<EscrowSettlementResult> ReleaseOnProofOfDeliveryAsync(ProofOfDeliveryConfirmationRequest request, CancellationToken ct = default);
    Task<EscrowRefundResult> RefundOnReturnAsync(EscrowRefundRequest request, CancellationToken ct = default);
    Task<EscrowHoldRecord?> GetEscrowRecordAsync(Guid tenantId, string orderId, CancellationToken ct = default);
    SettlementSplitBreakdown CalculateSettlementSplit(
        decimal grossAmount,
        decimal courierFee,
        decimal platformTakeRatePercentage,
        decimal affiliateCommissionPercentage = 0m,
        string currency = "DZD");
}

#endregion

#region Implementation

public sealed class EscrowSettlementEngine : IEscrowSettlementEngine
{
    private readonly ConcurrentDictionary<(Guid TenantId, string OrderId), EscrowHoldRecord> _escrows = new();
    private readonly IDoubleEntryLedgerService _ledgerService;
    private readonly IVirtualWalletPayoutService _walletService;
    private readonly Lock _syncLock = new();

    public EscrowSettlementEngine(
        IDoubleEntryLedgerService ledgerService,
        IVirtualWalletPayoutService walletService)
    {
        _ledgerService = ledgerService;
        _walletService = walletService;
    }

    /// <summary>
    /// Exact penny/cent/centime precision: guarantees zero rounding leakage.
    /// Merchant Net is calculated as residual: Gross - CourierFee - PlatformFee - AffiliateFee.
    /// Invariant: MerchantNet + CourierFee + AffiliateCommission + PlatformTakeRateFee == GrossAmount.
    /// </summary>
    public SettlementSplitBreakdown CalculateSettlementSplit(
        decimal grossAmount,
        decimal courierFee,
        decimal platformTakeRatePercentage,
        decimal affiliateCommissionPercentage = 0m,
        string currency = "DZD")
    {
        if (grossAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(grossAmount), "Gross order amount must be positive.");
        }

        if (courierFee < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(courierFee), "Courier delivery fee cannot be negative.");
        }

        // Exact rounded components to 2 decimal places (AwayFromZero)
        decimal platformFee = Math.Round(grossAmount * platformTakeRatePercentage, 2, MidpointRounding.AwayFromZero);
        decimal affiliateFee = Math.Round(grossAmount * affiliateCommissionPercentage, 2, MidpointRounding.AwayFromZero);

        // Residual merchant net ensures ZERO penny leakage
        decimal merchantNet = grossAmount - (courierFee + platformFee + affiliateFee);

        // Invariant check: exactly equal to grossAmount
        decimal sum = merchantNet + courierFee + platformFee + affiliateFee;
        if (sum != grossAmount)
        {
            throw new InvalidOperationException($"Rounding leakage detected in settlement split! Expected {grossAmount:F2}, got sum {sum:F2}.");
        }

        return new SettlementSplitBreakdown(
            GrossAmount: grossAmount,
            MerchantNet: merchantNet,
            CourierFee: courierFee,
            AffiliateCommission: affiliateFee,
            PlatformTakeRateFee: platformFee,
            Currency: currency
        );
    }

    /// <summary>
    /// Stage 1: LockOnCheckout -> Holds total order amount in 1100 Customer Escrow Held.
    /// Balanced double-entry:
    /// Debit 1000 Cash &amp; Courier Float (GrossAmount)
    /// Credit 1100 Customer Escrow Held (GrossAmount)
    /// Also records merchant virtual wallet escrow locked balance.
    /// </summary>
    public async Task<EscrowHoldRecord> LockOnCheckoutAsync(EscrowLockRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GrossAmount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Gross amount must be positive.");
        }

        var key = (request.TenantId, request.OrderId);
        lock (_syncLock)
        {
            if (_escrows.TryGetValue(key, out var existing))
            {
                return existing;
            }
        }

        // 1. Post balanced double-entry: Debit Cash Float (1000), Credit Escrow Held (1100)
        var journalLines = new List<JournalLineRequest>
        {
            new(
                AccountCode: StandardAccountCodes.CashAndCourierFloat,
                Debit: request.GrossAmount,
                Credit: 0m,
                Description: $"COD / Float collection for Order #{request.OrderId}",
                EntityId: request.CourierId
            ),
            new(
                AccountCode: StandardAccountCodes.CustomerEscrowHeld,
                Debit: 0m,
                Credit: request.GrossAmount,
                Description: $"Customer escrow held custody for Order #{request.OrderId}",
                EntityId: request.CustomerId
            )
        };

        var postReq = new PostJournalEntryRequest(
            TenantId: request.TenantId,
            ReferenceType: "EscrowLockOnCheckout",
            ReferenceId: request.OrderId,
            Description: $"Checkout escrow lock for Order #{request.OrderId}",
            Lines: journalLines,
            Currency: request.Currency
        );

        var journalEntry = await _ledgerService.PostEntryAsync(postReq, ct);

        // 2. Lock merchant's virtual wallet escrow balance
        var merchantWallet = await _walletService.GetOrCreateWalletAsync(
            request.TenantId,
            request.MerchantId,
            WalletOwnerType.Merchant,
            request.Currency,
            ct
        );

        await _walletService.LockEscrowBalanceAsync(merchantWallet.WalletId, request.GrossAmount, request.OrderId, ct);

        // 3. Create Escrow Record
        var record = new EscrowHoldRecord
        {
            EscrowId = Guid.NewGuid(),
            TenantId = request.TenantId,
            OrderId = request.OrderId,
            CustomerId = request.CustomerId,
            MerchantId = request.MerchantId,
            CourierId = request.CourierId,
            AffiliateId = request.AffiliateId,
            GrossAmount = request.GrossAmount,
            Currency = request.Currency,
            Status = EscrowStatus.Locked,
            LockedAtUtc = DateTimeOffset.UtcNow,
            LockJournalEntryId = journalEntry.EntryId,
            Notes = request.Notes
        };

        lock (_syncLock)
        {
            _escrows[key] = record;
        }

        return record;
    }

    /// <summary>
    /// Stage 2: ReleaseOnProofOfDelivery (POD) -> Ingests courier POD confirmation, automatically posts balanced double-entry split:
    /// - Debit 1100 Customer Escrow Held (full order amount)
    /// - Credit 2000 Merchant Payable (Merchant Net Amount)
    /// - Credit 2100 Courier Remittance Payable (Carrier Delivery Fee)
    /// - Credit 2200 Affiliate Payable (Affiliate Commission)
    /// - Credit 4000 Platform Revenue (Platform Commission Take Rate)
    /// </summary>
    public async Task<EscrowSettlementResult> ReleaseOnProofOfDeliveryAsync(ProofOfDeliveryConfirmationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = (request.TenantId, request.OrderId);
        EscrowHoldRecord escrow;

        lock (_syncLock)
        {
            if (!_escrows.TryGetValue(key, out var found))
            {
                throw new KeyNotFoundException($"Escrow hold for Order '{request.OrderId}' was not found.");
            }

            if (found.Status != EscrowStatus.Locked)
            {
                throw new InvalidOperationException($"Cannot release escrow for Order '{request.OrderId}': current status is '{found.Status}' (expected '{EscrowStatus.Locked}').");
            }

            escrow = found;
        }

        // 1. Calculate balanced split with exact penny/centime precision
        var split = CalculateSettlementSplit(
            grossAmount: escrow.GrossAmount,
            courierFee: request.CourierFee,
            platformTakeRatePercentage: request.PlatformTakeRatePercentage,
            affiliateCommissionPercentage: request.AffiliateCommissionPercentage,
            currency: escrow.Currency
        );

        // 2. Prepare 4-way / 5-line balanced double-entry:
        // Debit: Customer Escrow Held (1100) -> Full Gross Amount
        // Credit: Merchant Payable (2000) -> MerchantNet
        // Credit: Courier Remittance Payable (2100) -> CourierFee
        // Credit: Affiliate Payable (2200) -> AffiliateCommission (if > 0)
        // Credit: Platform Revenue (4000) -> PlatformTakeRateFee
        var lines = new List<JournalLineRequest>
        {
            new(
                AccountCode: StandardAccountCodes.CustomerEscrowHeld,
                Debit: split.GrossAmount,
                Credit: 0m,
                Description: $"POD release of escrow custody for Order #{request.OrderId} (Tracking #{request.TrackingNumber})",
                EntityId: escrow.CustomerId
            ),
            new(
                AccountCode: StandardAccountCodes.MerchantAccountsPayable,
                Debit: 0m,
                Credit: split.MerchantNet,
                Description: $"Net settlement to Merchant for Order #{request.OrderId}",
                EntityId: escrow.MerchantId
            ),
            new(
                AccountCode: StandardAccountCodes.CourierRemittancePayable,
                Debit: 0m,
                Credit: split.CourierFee,
                Description: $"Delivery fee payable to Courier for Order #{request.OrderId}",
                EntityId: escrow.CourierId
            )
        };

        if (split.AffiliateCommission > 0m && !string.IsNullOrWhiteSpace(escrow.AffiliateId))
        {
            lines.Add(new JournalLineRequest(
                AccountCode: StandardAccountCodes.AffiliateCommissionPayable,
                Debit: 0m,
                Credit: split.AffiliateCommission,
                Description: $"Affiliate commission earned on Order #{request.OrderId}",
                EntityId: escrow.AffiliateId
            ));
        }
        else if (split.AffiliateCommission > 0m)
        {
            // If affiliate commission was specified but no affiliate id, roll it into platform or merchant
            lines.Add(new JournalLineRequest(
                AccountCode: StandardAccountCodes.AffiliateCommissionPayable,
                Debit: 0m,
                Credit: split.AffiliateCommission,
                Description: $"Affiliate commission reserve for Order #{request.OrderId}",
                EntityId: "AFFILIATE_UNASSIGNED"
            ));
        }

        lines.Add(new JournalLineRequest(
            AccountCode: StandardAccountCodes.PlatformSaaSTakeRateRevenue,
            Debit: 0m,
            Credit: split.PlatformTakeRateFee,
            Description: $"Platform SaaS take rate revenue on Order #{request.OrderId}"
        ));

        var postReq = new PostJournalEntryRequest(
            TenantId: request.TenantId,
            ReferenceType: "ProofOfDeliverySettlement",
            ReferenceId: request.OrderId,
            Description: $"POD 4-way settlement for Order #{request.OrderId} (Carrier: {request.TrackingNumber})",
            Lines: lines,
            Currency: escrow.Currency
        );

        var settlementEntry = await _ledgerService.PostEntryAsync(postReq, ct);

        // 3. Update Virtual Wallets
        // Merchant Wallet: unlock full gross amount, credit net merchant amount to available balance
        var merchantWallet = await _walletService.GetOrCreateWalletAsync(escrow.TenantId, escrow.MerchantId, WalletOwnerType.Merchant, escrow.Currency, ct);
        await _walletService.ReleaseEscrowToAvailableAsync(merchantWallet.WalletId, escrow.GrossAmount, split.MerchantNet, request.OrderId, ct);

        // Courier Wallet: credit delivery fee
        var courierWallet = await _walletService.GetOrCreateWalletAsync(escrow.TenantId, escrow.CourierId, WalletOwnerType.Courier, escrow.Currency, ct);
        await _walletService.CreditAvailableBalanceAsync(courierWallet.WalletId, split.CourierFee, "CourierFeePOD", request.OrderId, $"Delivery fee for Order #{request.OrderId}", ct);

        // Affiliate Wallet (if applicable)
        if (split.AffiliateCommission > 0m && !string.IsNullOrWhiteSpace(escrow.AffiliateId))
        {
            var affiliateWallet = await _walletService.GetOrCreateWalletAsync(escrow.TenantId, escrow.AffiliateId, WalletOwnerType.Affiliate, escrow.Currency, ct);
            await _walletService.CreditAvailableBalanceAsync(affiliateWallet.WalletId, split.AffiliateCommission, "AffiliateCommissionPOD", request.OrderId, $"Affiliate commission for Order #{request.OrderId}", ct);
        }

        // 4. Update Escrow Record status
        var updatedRecord = escrow with
        {
            Status = EscrowStatus.Settled,
            SettledAtUtc = DateTimeOffset.UtcNow,
            SettlementJournalEntryId = settlementEntry.EntryId,
            SettlementSplit = split,
            Notes = request.Notes ?? escrow.Notes
        };

        lock (_syncLock)
        {
            _escrows[key] = updatedRecord;
        }

        return new EscrowSettlementResult(
            Success: true,
            EscrowRecord: updatedRecord,
            SettlementJournalEntry: settlementEntry,
            SplitBreakdown: split
        );
    }

    /// <summary>
    /// Stage 3: RefundOnReturn -> If package is refused/returned, automatically issues reversal journal entries:
    /// Debit 1100 Customer Escrow Held (Gross Amount)
    /// Credit 1000 Cash &amp; Courier Float (Gross Amount)
    /// Optional return shipping expense:
    /// Debit 5000 Shipping &amp; Freight Expense (ReturnShippingFee)
    /// Credit 2100 Courier Remittance Payable (ReturnShippingFee)
    /// Unlocks merchant virtual wallet escrow locked balance.
    /// </summary>
    public async Task<EscrowRefundResult> RefundOnReturnAsync(EscrowRefundRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = (request.TenantId, request.OrderId);
        EscrowHoldRecord escrow;

        lock (_syncLock)
        {
            if (!_escrows.TryGetValue(key, out var found))
            {
                throw new KeyNotFoundException($"Escrow hold for Order '{request.OrderId}' was not found.");
            }

            if (found.Status == EscrowStatus.Refunded)
            {
                throw new InvalidOperationException($"Order '{request.OrderId}' is already refunded.");
            }

            escrow = found;
        }

        JournalEntry refundEntry;

        if (escrow.Status == EscrowStatus.Settled && escrow.SettlementJournalEntryId.HasValue)
        {
            // Case A: Post-delivery return / dispute reversal:
            // Reverse the stage 2 settlement entry
            refundEntry = await _ledgerService.ReverseEntryAsync(
                escrow.SettlementJournalEntryId.Value,
                $"Refund on return: {request.Reason}",
                ct
            );
        }
        else
        {
            // Case B: Pre-delivery refusal / RTO (Return to Origin):
            // Reverse Stage 1 lockup: Debit Escrow Held (1100), Credit Cash Float (1000)
            var lines = new List<JournalLineRequest>
            {
                new(
                    AccountCode: StandardAccountCodes.CustomerEscrowHeld,
                    Debit: escrow.GrossAmount,
                    Credit: 0m,
                    Description: $"Escrow cancellation for Order #{request.OrderId} ({request.Reason})",
                    EntityId: escrow.CustomerId
                ),
                new(
                    AccountCode: StandardAccountCodes.CashAndCourierFloat,
                    Debit: 0m,
                    Credit: escrow.GrossAmount,
                    Description: $"Return of COD float / funds for Order #{request.OrderId}",
                    EntityId: escrow.CourierId
                )
            };

            // If there is return shipping fee charged by courier:
            if (request.ReturnShippingFee > 0m)
            {
                lines.Add(new JournalLineRequest(
                    AccountCode: StandardAccountCodes.ShippingAndFreightExpense,
                    Debit: request.ReturnShippingFee,
                    Credit: 0m,
                    Description: $"Return freight expense for refused package #{request.OrderId}"
                ));

                lines.Add(new JournalLineRequest(
                    AccountCode: StandardAccountCodes.CourierRemittancePayable,
                    Debit: 0m,
                    Credit: request.ReturnShippingFee,
                    Description: $"Return freight remittance owed to courier #{escrow.CourierId}",
                    EntityId: escrow.CourierId
                ));
            }

            var postReq = new PostJournalEntryRequest(
                TenantId: request.TenantId,
                ReferenceType: "RefundOnReturn",
                ReferenceId: request.OrderId,
                Description: $"Refund on return for Order #{request.OrderId} ({request.Reason})",
                Lines: lines,
                Currency: escrow.Currency
            );

            refundEntry = await _ledgerService.PostEntryAsync(postReq, ct);

            // Unlock merchant's locked escrow balance
            var merchantWallet = await _walletService.FindWalletByEntityAsync(escrow.TenantId, escrow.MerchantId, escrow.Currency, ct);
            if (merchantWallet != null)
            {
                await _walletService.UnlockEscrowBalanceAsync(merchantWallet.WalletId, escrow.GrossAmount, request.OrderId, ct);
            }

            // Credit courier return fee if applicable
            if (request.ReturnShippingFee > 0m)
            {
                var courierWallet = await _walletService.GetOrCreateWalletAsync(escrow.TenantId, escrow.CourierId, WalletOwnerType.Courier, escrow.Currency, ct);
                await _walletService.CreditAvailableBalanceAsync(courierWallet.WalletId, request.ReturnShippingFee, "ReturnFreightFee", request.OrderId, $"Return freight fee for Order #{request.OrderId}", ct);
            }
        }

        var updatedRecord = escrow with
        {
            Status = EscrowStatus.Refunded,
            RefundedAtUtc = DateTimeOffset.UtcNow,
            RefundJournalEntryId = refundEntry.EntryId,
            Notes = $"{escrow.Notes}; Refunded: {request.Reason}"
        };

        lock (_syncLock)
        {
            _escrows[key] = updatedRecord;
        }

        return new EscrowRefundResult(
            Success: true,
            EscrowRecord: updatedRecord,
            RefundJournalEntry: refundEntry,
            Reason: request.Reason
        );
    }

    public Task<EscrowHoldRecord?> GetEscrowRecordAsync(Guid tenantId, string orderId, CancellationToken ct = default)
    {
        _escrows.TryGetValue((tenantId, orderId), out var record);
        return Task.FromResult(record);
    }
}

#endregion
