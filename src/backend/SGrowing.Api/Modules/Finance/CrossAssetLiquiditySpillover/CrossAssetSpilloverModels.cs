namespace SGrowing.Api.Modules.Finance.CrossAssetLiquiditySpillover;

public sealed record CrossAssetSpilloverTelemetry(
    string PrimaryAssetSymbol,
    string CorrelatedAssetSymbol,
    double PrimaryAssetOwnImpactLambda,
    double CrossAssetImpactLambda,
    double PrimaryAssetOrderFlowVolume,
    double CorrelatedAssetOrderFlowVolume,
    double CrossAssetCovarianceEigenvalueRatio,
    double BaselineBidAskSpreadBasisPoints
);

public sealed record CrossAssetSpilloverAnalysisResult(
    string PrimaryAssetSymbol,
    double CrossAssetLiquiditySpilloverRatio,
    double ProjectedTotalCrossMarketImpactBasisPoints,
    double CrossMarketSlippageSpreadBasisPoints,
    string SpilloverRegime,
    string ExecutionRiskGuidance,
    bool IsSpilloverContained
);

public interface ICrossAssetLiquiditySpilloverEngine
{
    CrossAssetSpilloverAnalysisResult AnalyzeCrossAssetSpillover(CrossAssetSpilloverTelemetry telemetry);
}
