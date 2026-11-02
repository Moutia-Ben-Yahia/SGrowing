using System;

namespace SGrowing.Api.Modules.Finance.HierarchicalRiskParityClustering;

public sealed class HierarchicalRiskParityClusteringEngine : IHierarchicalRiskParityClusteringEngine
{
    public HierarchicalRiskParityAnalysisResult AnalyzePortfolioRiskParity(HierarchicalRiskParityTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.PortfolioClusterId))
        {
            throw new ArgumentException("Portfolio cluster ID must be provided.", nameof(telemetry));
        }

        if (telemetry.AssetCount <= 0 || telemetry.CovarianceMatrixConditionNumber <= 0 ||
            telemetry.PortfolioHerfindahlIndex <= 0)
        {
            throw new ArgumentException("Asset count, condition number, and HHI must be strictly positive.", nameof(telemetry));
        }

        var effectiveConstituents = 1.0 / Math.Clamp(telemetry.PortfolioHerfindahlIndex, 0.01, 1.0);
        var weightDispersion = Math.Max(0.0, telemetry.MaxSingleAssetHrpWeightPercent - telemetry.MinSingleAssetHrpWeightPercent);

        string regime;
        string guidance;
        bool isRobust = telemetry.CovarianceMatrixConditionNumber < 1000.0 &&
                        telemetry.PortfolioHerfindahlIndex <= 0.22 &&
                        telemetry.MaxSingleAssetHrpWeightPercent < 35.0 &&
                        telemetry.OutOfSampleDiversificationRatio >= 1.5;

        if (telemetry.AssetCount < 3)
        {
            regime = "INSUFFICIENT_ASSET_UNIVERSE_FOR_TREE_CLUSTERING";
            guidance = $"Asset count ({telemetry.AssetCount}) is insufficient for hierarchical linkage tree clustering. Expand asset universe to at least 3 assets.";
            isRobust = false;
        }
        else if (telemetry.CovarianceMatrixConditionNumber >= 1500.0 && telemetry.AveragePairwiseCorrelation >= 0.85)
        {
            regime = "SEVERE_ILL_CONDITIONED_COLLINEARITY_HAZARD";
            guidance = $"Severe multi-collinearity (Condition number = {telemetry.CovarianceMatrixConditionNumber:F0}, avg rho = {telemetry.AveragePairwiseCorrelation:F2}). Tree clustering isolates collinear branches, but asset pruning is recommended.";
            isRobust = false;
        }
        else if (telemetry.MaxSingleAssetHrpWeightPercent >= 45.0 || telemetry.PortfolioHerfindahlIndex >= 0.30)
        {
            regime = "CONCENTRATED_CLUSTER_RISK_PARITY_IMBALANCE";
            guidance = $"Excessive concentration in top cluster (Max weight = {telemetry.MaxSingleAssetHrpWeightPercent:F1}%, HHI = {telemetry.PortfolioHerfindahlIndex:F3}). Apply recursive bisection bounds to cap single-node allocations.";
            isRobust = false;
        }
        else if (telemetry.OutOfSampleDiversificationRatio >= 2.0 && telemetry.PortfolioHerfindahlIndex <= 0.18)
        {
            regime = "OPTIMAL_HIERARCHICAL_RISK_PARITY_DIVERSIFIED_REGIME";
            guidance = $"Robust risk parity achieved: Effective constituents = {effectiveConstituents:F1}, Diversification ratio = {telemetry.OutOfSampleDiversificationRatio:F2}. Immune to quadratic covariance inversion errors.";
        }
        else
        {
            regime = "BALANCED_QUASI_DIAGONALIZED_ALLOCATION_REGIME";
            guidance = $"HRP tree clustering completed. Portfolio exhibits stable quasi-diagonalized variance weights across clusters.";
        }

        return new HierarchicalRiskParityAnalysisResult(
            telemetry.PortfolioClusterId,
            Math.Round(effectiveConstituents, 2),
            Math.Round(weightDispersion, 2),
            regime,
            guidance,
            isRobust
        );
    }
}
