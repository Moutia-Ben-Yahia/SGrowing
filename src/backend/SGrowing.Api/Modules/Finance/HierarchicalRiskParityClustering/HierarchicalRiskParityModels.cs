namespace SGrowing.Api.Modules.Finance.HierarchicalRiskParityClustering;

public sealed record HierarchicalRiskParityTelemetry(
    string PortfolioClusterId,
    int AssetCount, // N: e.g. 10 assets
    double CovarianceMatrixConditionNumber, // kappa(Sigma): e.g. 120.0
    double AveragePairwiseCorrelation, // rho_avg: e.g. 0.38
    double MaxSingleAssetHrpWeightPercent, // e.g. 18.5%
    double MinSingleAssetHrpWeightPercent, // e.g. 4.2%
    double PortfolioHerfindahlIndex, // HHI: e.g. 0.125
    double OutOfSampleDiversificationRatio // e.g. 2.45
);

public sealed record HierarchicalRiskParityAnalysisResult(
    string PortfolioClusterId,
    double EffectiveNumberConstituents,
    double WeightDispersionPercent,
    string AllocationRegime,
    string RiskParityGuidance,
    bool IsPortfolioAllocationRobust
);

public interface IHierarchicalRiskParityClusteringEngine
{
    HierarchicalRiskParityAnalysisResult AnalyzePortfolioRiskParity(HierarchicalRiskParityTelemetry telemetry);
}
