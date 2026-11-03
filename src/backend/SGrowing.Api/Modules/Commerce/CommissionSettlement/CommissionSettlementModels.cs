using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Commerce.CommissionSettlement;

public enum RepTier
{
    Baseline = 0,        // 0 - 80% quota (8%)
    Standard = 1,        // 80.1% - 100% quota (10%)
    Accelerator = 2,     // 100.1% - 120% quota (15%)
    SuperAccelerator = 3 // > 120% quota (20%)
}

public enum PayoutStatus
{
    PendingSigning = 0,
    DisbursedImmediate = 1,
    HeldInClawbackEscrow = 2,
    FullySettled = 3
}

public record CommissionRecord(
    string CommissionId,
    string DealId,
    string AccountName,
    string RepId,
    string RepName,
    decimal DealArr,
    double QuotaAttainmentPct,
    RepTier Tier,
    decimal TotalCommission,
    decimal ImmediateDisbursedAmount, // 80% upfront
    decimal ClawbackEscrowAmount,     // 20% 90-day escrow
    PayoutStatus Status,
    DateTime ClosedAt,
    DateTime EscrowReleaseDate,
    string PayoutHash
);

public record CommissionOverview(
    int TotalDealsCount,
    decimal TotalCommissionsPaid,
    decimal TotalInEscrow,
    double AverageAttainmentPct,
    List<CommissionRecord> Commissions
);

public record CalculateCommissionRequest(
    string DealId,
    string AccountName,
    string RepId,
    string RepName,
    decimal DealArr,
    double QuotaAttainmentPct
);

public record ReleaseEscrowCommand(
    string CommissionId,
    string ApproverId
);

public record ReleaseEscrowResult(
    string CommissionId,
    decimal ReleasedAmount,
    PayoutStatus Status,
    string ReleaseReceipt
);
