namespace SGrowing.Api.Modules.Finance.DynamicMarginCollateralVar;

public sealed record DynamicMarginVarTelemetry(
    string AccountPortfolioId,
    double TotalCollateralValueUsd,
    double TotalGrossNotionalPositionUsd,
    double PortfolioLeverageRatio,
    double HistoricalVolatilityAnnualized,
    double EvtTailIndexShapeParameterXi, // Pareto tail shape (xi > 0 indicates heavy tail)
    double ConcentratedLiquidityImpermanentLossPercent,
    double WeightedCollateralHaircutPercent,
    double DailyPositionFundingRateBasisPoints
);

public sealed record DynamicMarginVarAnalysisResult(
    string AccountPortfolioId,
    double ValueAtRisk999Usd,
    double ExpectedShortfallUsd,
    double RequiredDynamicMaintenanceMarginUsd,
    double MarginBufferUtilizationRatio,
    string MarginRiskRegime,
    string CollateralRebalancingGuidance,
    bool IsMarginCompliant
);

public interface IDynamicMarginCollateralVarEngine
{
    DynamicMarginVarAnalysisResult AnalyzeMarginRisk(DynamicMarginVarTelemetry telemetry);
}
