namespace SGrowing.Api.Modules.Finance.BepsPillarTwo;

public record JurisdictionTaxData(
    string JurisdictionCode,
    string CountryName,
    decimal TotalRevenueEur,
    decimal GloBeNetIncomeEur,
    decimal CoveredTaxesEur,
    decimal EligiblePayrollCostsEur,
    decimal EligibleTangibleAssetBookValueEur);

public record BepsPillarTwoCalculationRequest(
    string FiscalYear,
    decimal MinimumTaxRate = 0.15m,
    decimal PayrollCarveOutRate = 0.05m,
    decimal TangibleAssetCarveOutRate = 0.05m,
    List<JurisdictionTaxData>? Jurisdictions = null);

public record JurisdictionAssessment(
    string JurisdictionCode,
    string CountryName,
    decimal EffectiveTaxRate,
    decimal TopUpTaxPercentage,
    decimal SbieCarveOutEur,
    decimal ExcessProfitEur,
    decimal TopUpTaxPayableEur,
    bool IsDeMinimisExcluded,
    string StatusReason);

public record BepsPillarTwoCalculationResult(
    string FiscalYear,
    int TotalJurisdictionsEvaluated,
    decimal TotalGloBeNetIncomeEur,
    decimal TotalCoveredTaxesEur,
    decimal GlobalEffectiveTaxRate,
    decimal TotalTopUpTaxPayableEur,
    List<JurisdictionAssessment> Assessments,
    string ComplianceVerdict);
