namespace SGrowing.Api.Modules.Finance.LsdDepegArbitrageRedemption;

public sealed record LsdArbitrageTelemetry(
    string PoolId,
    string LsdTokenSymbol,
    double SecondaryMarketPrice,
    double CanonicalRedemptionRate,
    double ValidatorExitQueueDelayDays,
    double SecondaryPoolImbalanceRatio,
    double SimulatedTradeSizeEth,
    double EstimatedGasCostEth,
    double EstimatedPriceImpactPercent,
    double AnnualizedCapitalCostHurdleRatePercent
);

public sealed record LsdArbitrageAnalysisResult(
    string PoolId,
    string LsdTokenSymbol,
    double DepegDiscountBasisPoints,
    double NetArbitrageYieldAnnualizedPercent,
    double ExpectedProfitPerTradeEth,
    string ArbitrageRegime,
    string ExecutionStrategy,
    bool IsArbitrageViable
);

public interface ILsdDepegArbitrageRedemptionEngine
{
    LsdArbitrageAnalysisResult AnalyzeLsdDepegArbitrage(LsdArbitrageTelemetry telemetry);
}
