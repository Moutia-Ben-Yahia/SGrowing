namespace SGrowing.Api.Modules.Finance.AmmDynamicImpermanentLossHedge;

public sealed record AmmDynamicHedgeTelemetry(
    string PoolPositionId,
    string PoolPairSymbol, // e.g. "ETH/USDC"
    double InitialEntryPriceRatio, // P_0
    double CurrentMarketPriceRatio, // P_t
    double TotalLiquidityPoolValueUsd, // V_0
    double CumulativeTradingFeesEarnedUsd,
    double PerpetualFuturesFundingRateBasisPoints, // daily funding cost bps
    double DynamicDeltaHedgeRebalanceThresholdPercent, // e.g. 5.0%
    double ImpliedVolatilityAnnualized
);

public sealed record AmmDynamicHedgeAnalysisResult(
    string PoolPositionId,
    double PriceDivergenceRatioK,
    double UnhedgedImpermanentLossPercent,
    double UnhedgedImpermanentLossUsd,
    double OptimalPerpetualHedgeDeltaUnits,
    double NetHedgedPortfolioReturnUsd,
    string ImpermanentLossHedgeRegime,
    string DeltaRebalancingGuidance,
    bool IsHedgeOptimal
);

public interface IAmmDynamicImpermanentLossHedgeEngine
{
    AmmDynamicHedgeAnalysisResult AnalyzeHedge(AmmDynamicHedgeTelemetry telemetry);
}
