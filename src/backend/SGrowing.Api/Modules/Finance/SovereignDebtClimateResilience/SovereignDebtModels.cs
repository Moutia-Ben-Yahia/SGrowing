namespace SGrowing.Api.Modules.Finance.SovereignDebtClimateResilience;

public sealed record SovereignDebtTelemetry(
    string SovereignCountryCode,
    double DebtToGdpRatioPercent,
    double RealEffectiveInterestRatePercent,
    double RealGdpGrowthRatePercent,
    double PrimaryBalancePercentOfGdp,
    double ClimateVulnerabilityIndex,
    double ProjectedAnnualClimateDamagePercentOfGdp,
    double SovereignBondSpreadBasisPoints,
    double ForeignCurrencyDebtSharePercent
);

public sealed record SovereignDebtAnalysisResult(
    string SovereignCountryCode,
    double SnowballDifferentialPercent,
    double RequiredPrimarySurplusPercentOfGdp,
    double ProjectedFiscalGapPercentOfGdp,
    double ClimateAdjustedDefaultProbabilityPercent,
    string FiscalSustainabilityRegime,
    string PolicyRecommendation,
    bool IsFiscalSustainable
);

public interface ISovereignDebtClimateResilienceEngine
{
    SovereignDebtAnalysisResult AnalyzeDebtSustainability(SovereignDebtTelemetry telemetry);
}
