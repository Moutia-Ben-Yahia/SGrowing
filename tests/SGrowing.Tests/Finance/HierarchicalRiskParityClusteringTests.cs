using System;
using SGrowing.Api.Modules.Finance.HierarchicalRiskParityClustering;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class HierarchicalRiskParityClusteringTests
{
    private readonly HierarchicalRiskParityClusteringEngine _engine = new();

    [Fact]
    public void AnalyzePortfolioRiskParity_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzePortfolioRiskParity(null!));
    }

    [Fact]
    public void AnalyzePortfolioRiskParity_InvalidAssetCount_ThrowsArgumentException()
    {
        var telemetry = new HierarchicalRiskParityTelemetry(
            PortfolioClusterId: "PORTFOLIO-01",
            AssetCount: -5, // Negative!
            CovarianceMatrixConditionNumber: 150.0,
            AveragePairwiseCorrelation: 0.35,
            MaxSingleAssetHrpWeightPercent: 20.0,
            MinSingleAssetHrpWeightPercent: 5.0,
            PortfolioHerfindahlIndex: 0.12,
            OutOfSampleDiversificationRatio: 2.2
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzePortfolioRiskParity(telemetry));
    }

    [Fact]
    public void AnalyzePortfolioRiskParity_OptimalDiversification_ReturnsOptimalRegime()
    {
        var telemetry = new HierarchicalRiskParityTelemetry(
            PortfolioClusterId: "GLOBAL-MULTI-ASSET-HRP",
            AssetCount: 12,
            CovarianceMatrixConditionNumber: 180.0,
            AveragePairwiseCorrelation: 0.40,
            MaxSingleAssetHrpWeightPercent: 16.5,
            MinSingleAssetHrpWeightPercent: 4.8,
            PortfolioHerfindahlIndex: 0.11, // <= 0.18
            OutOfSampleDiversificationRatio: 2.35 // >= 2.0
        );

        var result = _engine.AnalyzePortfolioRiskParity(telemetry);

        Assert.NotNull(result);
        Assert.Equal("GLOBAL-MULTI-ASSET-HRP", result.PortfolioClusterId);
        Assert.True(result.EffectiveNumberConstituents >= 8.0);
        Assert.True(result.IsPortfolioAllocationRobust);
        Assert.Equal("OPTIMAL_HIERARCHICAL_RISK_PARITY_DIVERSIFIED_REGIME", result.AllocationRegime);
        Assert.Contains("Immune to quadratic covariance inversion errors", result.RiskParityGuidance);
    }

    [Fact]
    public void AnalyzePortfolioRiskParity_SevereIllConditioning_ReturnsCollinearityHazard()
    {
        var telemetry = new HierarchicalRiskParityTelemetry(
            PortfolioClusterId: "COLLINEAR-SUB-UNIVERSE",
            AssetCount: 8,
            CovarianceMatrixConditionNumber: 2200.0, // >= 1500
            AveragePairwiseCorrelation: 0.92, // >= 0.85
            MaxSingleAssetHrpWeightPercent: 25.0,
            MinSingleAssetHrpWeightPercent: 3.0,
            PortfolioHerfindahlIndex: 0.15,
            OutOfSampleDiversificationRatio: 1.2
        );

        var result = _engine.AnalyzePortfolioRiskParity(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SEVERE_ILL_CONDITIONED_COLLINEARITY_HAZARD", result.AllocationRegime);
        Assert.False(result.IsPortfolioAllocationRobust);
        Assert.Contains("asset pruning is recommended", result.RiskParityGuidance);
    }

    [Fact]
    public void AnalyzePortfolioRiskParity_ConcentratedCluster_ReturnsImbalanceRegime()
    {
        var telemetry = new HierarchicalRiskParityTelemetry(
            PortfolioClusterId: "SKEWED-CLUSTER-ALLOC",
            AssetCount: 6,
            CovarianceMatrixConditionNumber: 300.0,
            AveragePairwiseCorrelation: 0.50,
            MaxSingleAssetHrpWeightPercent: 52.0, // >= 45%
            MinSingleAssetHrpWeightPercent: 2.0,
            PortfolioHerfindahlIndex: 0.35, // >= 0.30
            OutOfSampleDiversificationRatio: 1.6
        );

        var result = _engine.AnalyzePortfolioRiskParity(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CONCENTRATED_CLUSTER_RISK_PARITY_IMBALANCE", result.AllocationRegime);
        Assert.False(result.IsPortfolioAllocationRobust);
        Assert.Contains("Excessive concentration", result.RiskParityGuidance);
    }
}
