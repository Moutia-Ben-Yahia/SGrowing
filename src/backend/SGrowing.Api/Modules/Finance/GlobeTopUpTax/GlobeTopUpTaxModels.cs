namespace SGrowing.Api.Modules.Finance.GlobeTopUpTax;

public enum SafeHarborStatus
{
    EligibleDeMinimis,
    EligibleSimplifiedEtr,
    EligibleRoutineProfits,
    IneligibleFailsAllTests
}

public record JurisdictionTaxProfile(
    string JurisdictionCode, // e.g. "IE", "BM", "NL", "SG", "US"
    string JurisdictionName,
    decimal ConsolidatedRevenueEur,
    decimal GlobeNetIncomeEur,
    decimal AdjustedCoveredTaxesEur,
    decimal EligiblePayrollCostsEur,
    decimal EligibleTangibleAssetsEur,
    decimal StatutoryRatePercentage,
    DateTime FiscalYearEndUtc
);

public record SafeHarborEvaluation(
    string JurisdictionCode,
    bool PassesDeMinimisTest, // Revenue < 10M & Profit < 1M
    bool PassesSimplifiedEtrTest, // Simplified ETR >= 15%
    bool PassesRoutineProfitsTest, // Profit <= Substance Based Carveout
    SafeHarborStatus OverallStatus,
    string SafeHarborRationale,
    DateTime EvaluatedAtUtc
);

public record TopUpTaxCalculation(
    string CalculationId,
    string JurisdictionCode,
    decimal EffectiveTaxRatePercentage,
    decimal MinimumRatePercentage, // 15.0%
    decimal TopUpTaxPercentage,
    decimal SubstanceBasedCarveoutEur, // SBIE
    decimal ExcessProfitEur,
    decimal JurisdictionalTopUpTaxEur,
    decimal AllocatedQdmttLiabilityEur,
    decimal AllocatedIirLiabilityEur,
    DateTime CalculatedAtUtc
);

public record GlobeTaxLedgerSummary(
    int TotalJurisdictionsEvaluated,
    int SafeHarborExemptJurisdictions,
    int JurisdictionsSubjectToTopUp,
    decimal TotalGlobalTopUpTaxLiabilityEur,
    decimal TotalCoveredTaxesPaidEur,
    DateTime GeneratedAtUtc
);

public record AssessJurisdictionTaxRequest(
    string JurisdictionCode,
    string JurisdictionName,
    decimal ConsolidatedRevenueEur,
    decimal GlobeNetIncomeEur,
    decimal AdjustedCoveredTaxesEur,
    decimal EligiblePayrollCostsEur,
    decimal EligibleTangibleAssetsEur,
    decimal StatutoryRatePercentage
);
