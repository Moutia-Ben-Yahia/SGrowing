namespace SGrowing.Api.Modules.Finance.AlmgrenChrissOptimalLiquidation;

public record AlmgrenChrissLiquidationTelemetry
{
    public required string OrderExecutionId { get; init; }
    public string AssetTicker { get; init; } = "SPY";
    public double TotalOrderQuantityShares { get; init; } = 150000.0;
    public double ExecutionHorizonHours { get; init; } = 4.0; // T
    public int NumberOfTradingIntervals { get; init; } = 16; // N slices
    public double DailyAssetVolatilityPercent { get; init; } = 1.45; // sigma
    public double TemporaryPriceImpactEta { get; init; } = 2.5e-6; // eta
    public double PermanentPriceImpactGamma { get; init; } = 1.2e-7; // gamma
    public double RiskAversionParameterLambda { get; init; } = 1.5e-5; // lambda
    public double RealizedArrivalPriceUsd { get; init; } = 512.40;
    public double RealizedAverageExecutionPriceUsd { get; init; } = 511.95;
    public double MarketParticipationRatePercent { get; init; } = 8.5; // <= 15.0%
}

public record AlmgrenChrissLiquidationEvaluation
{
    public required string OrderExecutionId { get; init; }
    public double CalculatedUrgencyParameterKappa { get; init; }
    public double ExpectedImplementationShortfallBps { get; init; }
    public double TotalEstimatedCostUsd { get; init; }
    public double RealizedSlippageBps { get; init; }
    public bool IsParticipationRateSafe { get; init; }
    public bool IsSlippageWithinTolerance { get; init; }
    public bool IsTrajectoryRiskOptimal { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
