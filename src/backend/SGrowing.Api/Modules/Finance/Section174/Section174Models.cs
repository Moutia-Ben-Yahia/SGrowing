namespace SGrowing.Api.Modules.Finance.Section174;

public enum JurisdictionResearchType
{
    UnitedStatesDomestic5Year,
    ForeignResearch15Year
}

public sealed record Section174Schedule(
    string ScheduleId,
    int TaxYear,
    JurisdictionResearchType Jurisdiction,
    double TotalCapitalizedExpenditureUsd,
    double Year1DeductionUsd, // Mid-year convention (10% domestic, 3.33% foreign)
    double AnnualSubsequentDeductionUsd,
    double CumulativeTaxShieldGeneratedUsd,
    DateTime CreatedAtUtc
);

public sealed record EngineeringLaborAllocation(
    string AllocationId,
    string ProjectSprintId,
    string EngineeringSquadName,
    double TotalPayrollExpenditureUsd,
    double QualifyingSection174Ratio,
    JurisdictionResearchType Jurisdiction,
    double QualifyingCapitalizedAmountUsd
);

public sealed record Section174Overview(
    int TotalActiveSchedulesCount,
    double TotalDomesticCapitalizedUsd,
    double TotalForeignCapitalizedUsd,
    double AnnualSection174TaxDeductionUsd,
    double EstimatedCashTaxSavingsUsd,
    IReadOnlyList<Section174Schedule> ActiveSchedules,
    IReadOnlyList<EngineeringLaborAllocation> RecentAllocations
);
