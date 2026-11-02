namespace SGrowing.Api.Modules.Finance.TipsRealYieldBreakevenInflation;

public record TipsRealYieldTelemetry
{
    public required string SecurityCusipOrPortfolioId { get; init; }
    public double MaturityTenorYears { get; init; } = 10.0; // 5, 10, 30
    public double NominalTreasuryYieldPercent { get; init; } = 4.35; // e.g. 4.35%
    public double TipsRealYieldPercent { get; init; } = 2.05; // e.g. 2.05%
    public double ShortTermBreakevenInflationPercent { get; init; } = 2.45; // 2Y/5Y breakeven
    public double LongTermBreakevenInflationPercent { get; init; } = 2.30; // 10Y/30Y breakeven
    public double EstimatedInflationRiskPremiumBps { get; init; } = 35.0; // 25-50 bps
    public double EstimatedTipsLiquidityDiscountBps { get; init; } = 18.0; // 10-30 bps
    public double IndexationLagMonths { get; init; } = 3.0; // Standard 3-month CPI-U lag
    public double DeflationFloorOptionProbabilityPercent { get; init; } = 4.5; // <= 15%
    public double RealDurationYears { get; init; } = 8.65;
}

public record TipsRealYieldEvaluation
{
    public required string SecurityCusipOrPortfolioId { get; init; }
    public double CalculatedBreakevenInflationPercent { get; init; }
    public double ImpliedExpectedInflationPercent { get; init; }
    public double BreakevenCurveSlopeBps { get; init; }
    public bool IsBreakevenStructureNormal { get; init; }
    public bool IsTipsLiquidityNormal { get; init; }
    public bool IsDeflationFloorRiskContained { get; init; }
    public bool IsInflationHedgingOptimal { get; init; }
    public List<string> ActiveRegimes { get; init; } = new();
    public List<string> RecommendedMitigations { get; init; } = new();
    public DateTime EvaluatedAtUtc { get; init; } = DateTime.UtcNow;
}
