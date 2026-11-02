namespace SGrowing.Api.Modules.Finance.OrderFlowAdverseSelectionDrain;

public sealed record OrderFlowAdverseSelectionTelemetry(
    string OrderBookInstrumentId,
    double SignedTradeVolumeShares, // Q_signed: signed volume (positive = aggressive buy, negative = aggressive sell)
    double MidPricePostTradeImpactBps, // Delta P in basis points
    double ImmediatePostFillAdverseMoveBps, // Price move in 50ms post fill
    double DepthReplenishmentRateSharesPerSecond, // R_rep: replenishment rate
    double InboundAggressiveRunVelocitySharesPerSecond, // v_agg: aggressive run rate
    double SpreadWideTightRatio, // spread / baseline spread
    double CancellationToFillRatio // cancellation-to-fill ratio
);

public sealed record OrderFlowAdverseSelectionAnalysisResult(
    string OrderBookInstrumentId,
    double KylePriceImpactLambdaBpsPerThousandShares,
    double NetLiquidityDrainVelocitySharesPerSecond,
    double ToxicityDrainRatio,
    string MicrostructureRegime,
    string LiquidityManagementGuidance,
    bool IsQuoteWideningOrPullbackMandatory
);

public interface IOrderFlowAdverseSelectionDrainEngine
{
    OrderFlowAdverseSelectionAnalysisResult AnalyzeAdverseSelectionDrain(OrderFlowAdverseSelectionTelemetry telemetry);
}
