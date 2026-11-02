namespace SGrowing.Api.Modules.Finance.MertonJumpDiffusionOptionPricing;

public sealed record MertonJumpDiffusionTelemetry
{
    public required string OptionSymbol { get; init; }
    public double UnderlyingPrice { get; init; } = 250.0; // S
    public double StrikePrice { get; init; } = 250.0; // K
    public double TimeToExpiryYears { get; init; } = 0.25; // T (3 months)
    public double RiskFreeRate { get; init; } = 0.045; // r (4.5%)
    public double ContinuousVolatility { get; init; } = 0.22; // sigma (22%)
    public double JumpIntensityLambda { get; init; } = 0.85; // lambda_J (expected jumps per year)
    public double MeanJumpSizeLogMuJ { get; init; } = -0.08; // mu_J (-8% average jump return)
    public double JumpVolatilitySigmaJ { get; init; } = 0.15; // sigma_J (15% jump dispersion)
    public bool IsCallOption { get; init; } = true; // true = Call, false = Put
}

public sealed record MertonJumpDiffusionAnalysisResult
{
    public required string OptionSymbol { get; init; }
    public required double MertonTheoreticalPrice { get; init; }
    public required double BlackScholesContinuousPrice { get; init; }
    public required double JumpRiskPremiumSpread { get; init; }
    public required double JumpVarianceFractionOfTotal { get; init; }
    public required double TotalEffectiveVolatility { get; init; }
    public required bool IsJumpRiskDominant { get; init; }
    public required bool IsSignificantPreEarningsSmileSkew { get; init; }
    public required string OptionValuationRegime { get; init; }
    public required string HedgingAndVolatilityGuidance { get; init; }
}
