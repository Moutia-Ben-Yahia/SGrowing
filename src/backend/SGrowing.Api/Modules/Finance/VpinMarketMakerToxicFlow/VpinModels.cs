namespace SGrowing.Api.Modules.Finance.VpinMarketMakerToxicFlow;

public sealed record VpinTelemetry(
    string Symbol,
    double BuyerInitiatedVolume,
    double SellerInitiatedVolume,
    double TotalVolumeBucketSize,
    int NumberOfBucketsSampled,
    double CurrentInventorySkewFraction,
    double BaselineBidAskSpreadBasisPoints,
    double HistoricalVpinPercentileRank
);

public sealed record VpinAnalysisResult(
    string Symbol,
    double VpinScore,
    double RecommendedSpreadMultiplier,
    double AdjustedBidAskSpreadBasisPoints,
    string MarketMakerRiskRegime,
    string QuotingDefenseAction,
    bool IsMarketMakerSafe
);

public interface IVpinMarketMakerToxicFlowEngine
{
    VpinAnalysisResult AnalyzeOrderFlowToxicity(VpinTelemetry telemetry);
}
