namespace SGrowing.Api.Modules.Finance.BondPortfolioDurationConvexityImmunization;

public sealed record BondPortfolioImmunizationTelemetry
{
    public required string PortfolioId { get; init; }
    public string MandateName { get; init; } = "Pension-Liability-LDI-Index";
    public double PortfolioPresentValueUsd { get; init; } = 125000000.0; // Assets PV
    public double LiabilityPresentValueUsd { get; init; } = 120000000.0; // Liabilities PV (Surplus ~ $5M)
    public double PortfolioMacaulayDurationYears { get; init; } = 10.45; // D_assets (years)
    public double LiabilityMacaulayDurationYears { get; init; } = 10.50; // D_liabilities (years)
    public double PortfolioConvexityYears2 { get; init; } = 145.0; // C_assets (years^2)
    public double LiabilityConvexityYears2 { get; init; } = 128.0; // C_liabilities (years^2)
    public double CurrentYieldCurveRate { get; init; } = 0.046; // y (4.6%)
    public double SimulatedParallelRateShiftBps { get; init; } = 150.0; // Delta y (+150 bps)
}

public sealed record BondPortfolioImmunizationAnalysisResult
{
    public required string PortfolioId { get; init; }
    public required double PresentValueFundingRatioPercent { get; init; }
    public required double DurationMismatchGapYears { get; init; }
    public required double ConvexitySurplusYears2 { get; init; }
    public required double SimulatedAssetValueChangeUsd { get; init; }
    public required double SimulatedLiabilityValueChangeUsd { get; init; }
    public required double SimulatedNetSurplusChangeUsd { get; init; }
    public required bool IsFundingSurplusPresent { get; init; }
    public required bool IsDurationMatched { get; init; }
    public required bool IsConvexityDominant { get; init; }
    public required bool IsRedingtonImmunizationAchieved { get; init; }
    public required string ImmunizationStatusRegime { get; init; }
    public required string PortfolioRebalancingGuidance { get; init; }
}
