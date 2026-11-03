using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Finance.AssetDepreciation;

public sealed record AssetDepreciationOverview(
    int TotalFixedAssetsCount,
    decimal TotalCostBasisUsd,
    decimal AccumulatedDepreciationUsd,
    decimal NetBookValueUsd,
    decimal CurrentMonthDepreciationExpenseUsd,
    IReadOnlyList<FixedAssetRecord> FixedAssets,
    IReadOnlyList<DepreciationScheduleEntry> RecentSchedules
);

public sealed record FixedAssetRecord(
    string AssetTag,
    string AssetName,
    string AssetClass, // GPU Server Fleet, Edge POP Hardware, Office Facility Leasehold
    decimal CostBasisUsd,
    decimal SalvageValueUsd,
    int UsefulLifeYears,
    string DepreciationConvention, // US_MACRS_5YR, IFRS_STRAIGHT_LINE, BONUS_SEC_179
    DateTime InServiceDate
);

public sealed record DepreciationScheduleEntry(
    string ScheduleId,
    string AssetTag,
    int FiscalYear,
    int FiscalPeriodMonth,
    decimal DepreciationAmountUsd,
    decimal RemainingBookValueUsd,
    string StandardApplied
);

public sealed record CalculateDepreciationRequest(
    string AssetTag,
    decimal CostBasisUsd,
    decimal SalvageValueUsd,
    int UsefulLifeYears,
    string DepreciationMethod, // MACRS_200_DB, STRAIGHT_LINE, SUM_OF_YEARS_DIGITS
    int TargetFiscalYear
);

public sealed record DepreciationCalculationResult(
    string CalculationId,
    string AssetTag,
    string Method,
    decimal AnnualDepreciationExpenseUsd,
    decimal MonthlyDepreciationUsd,
    decimal YearEndBookValueUsd,
    DateTime CalculatedAt
);

public sealed record RecordAssetImpairmentRequest(
    string AssetTag,
    decimal RecoverableAmountUsd,
    string ImpairmentTriggerReason
);

public sealed record ImpairmentJournalReceipt(
    string JournalEntryId,
    string AssetTag,
    decimal PreImpairmentBookValueUsd,
    decimal ImpairmentLossRecognizedUsd,
    decimal PostImpairmentCarryingValueUsd,
    string Status,
    DateTime Timestamp
);
