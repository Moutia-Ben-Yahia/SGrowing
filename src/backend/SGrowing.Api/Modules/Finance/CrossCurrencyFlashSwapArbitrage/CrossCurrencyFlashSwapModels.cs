namespace SGrowing.Api.Modules.Finance.CrossCurrencyFlashSwapArbitrage;

public sealed record CrossCurrencyFlashSwapTelemetry(
    string ArbitragePathId,
    string TriangularRouteDescription, // e.g. "USDC -> WETH -> WBTC -> USDC"
    double ProposedBorrowAmountUsd, // Flash borrow amount: e.g. 250_000 USD
    double Leg1PoolFeeTierPercent, // e.g. 0.05%
    double Leg2PoolFeeTierPercent, // e.g. 0.30%
    double Leg3PoolFeeTierPercent, // e.g. 0.05%
    double ProductOfExchangeRates, // Cross-rate product R_12 * R_23 * R_31
    double EstimatedTotalPriceImpactBasisPoints, // Slippage bps
    double PriorityBuilderTipFeeGwei, // MEV bundle tip
    double EthereumBaseGasFeeGwei,
    bool IsPrivateMevBuilderRelayUsed // Flashbots / MEV-Share
);

public sealed record CrossCurrencyFlashSwapAnalysisResult(
    string ArbitragePathId,
    double OptimalArbitrageTradeSizeUsd,
    double GrossTriangularProfitUsd,
    double TotalExecutionCostGasAndTipsUsd,
    double NetRiskAdjustedArbitrageProfitUsd,
    string ArbitrageExecutionRegime,
    string MevBundleSubmissionGuidance,
    bool IsArbitrageProfitable
);

public interface ICrossCurrencyFlashSwapArbitrageEngine
{
    CrossCurrencyFlashSwapAnalysisResult AnalyzeTriangularArbitrage(CrossCurrencyFlashSwapTelemetry telemetry);
}
