using System;
using SGrowing.Api.Modules.BioTech.LentiviralVectorIntegrationOncogenesis;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class LentiviralVectorIntegrationOncogenesisTests
{
    private readonly LentiviralVectorIntegrationOncogenesisEngine _engine = new();

    [Fact]
    public void AnalyzeIntegrationProfile_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeIntegrationProfile(null!));
    }

    [Fact]
    public void AnalyzeIntegrationProfile_EmptyBatchId_ThrowsArgumentException()
    {
        var telemetry = new LentiviralIntegrationTelemetry(
            TherapyBatchId: "",
            AverageVectorCopyNumber: 2.0,
            TotalUniqueIntegrationSites: 10_000,
            IntegrationsWithin50kbOfOncogeneTss: 2,
            DominantCloneAbundancePercent: 3.5,
            ShannonClonalityEntropy: 4.1,
            SelfInactivatingSinArchitecturePresent: true
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeIntegrationProfile(telemetry));
    }

    [Fact]
    public void AnalyzeIntegrationProfile_SafePolyclonal_ReturnsSafePolyclonalRegime()
    {
        var telemetry = new LentiviralIntegrationTelemetry(
            TherapyBatchId: "CAR-T-BATCH-994",
            AverageVectorCopyNumber: 1.8,
            TotalUniqueIntegrationSites: 18_500,
            IntegrationsWithin50kbOfOncogeneTss: 1,
            DominantCloneAbundancePercent: 2.8,
            ShannonClonalityEntropy: 4.4,
            SelfInactivatingSinArchitecturePresent: true
        );

        var result = _engine.AnalyzeIntegrationProfile(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CAR-T-BATCH-994", result.TherapyBatchId);
        Assert.Equal("POLYCLONAL_HEALTHY_DISTRIBUTION", result.ClonalityProfile);
        Assert.True(result.IsBatchSafeForInfusion);
        Assert.Equal("SAFE_POLYCLONAL_INTEGRATION_PROFILE_REGIME", result.OncogenesisRiskRegime);
        Assert.Contains("Certified safe for clinical patient infusion", result.ClinicalReleaseGuidance);
    }

    [Fact]
    public void AnalyzeIntegrationProfile_NonSinVector_ReturnsNonSinHazard()
    {
        var telemetry = new LentiviralIntegrationTelemetry(
            TherapyBatchId: "LEGACY-LTR-BATCH-02",
            AverageVectorCopyNumber: 2.5,
            TotalUniqueIntegrationSites: 5_000,
            IntegrationsWithin50kbOfOncogeneTss: 5,
            DominantCloneAbundancePercent: 5.0,
            ShannonClonalityEntropy: 3.8,
            SelfInactivatingSinArchitecturePresent: false // Non-SIN!
        );

        var result = _engine.AnalyzeIntegrationProfile(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CRITICAL_NON_SIN_LTR_PROMOTER_INSERTIONAL_HAZARD", result.OncogenesisRiskRegime);
        Assert.False(result.IsBatchSafeForInfusion);
        Assert.Contains("lacks Self-Inactivating", result.ClinicalReleaseGuidance);
    }

    [Fact]
    public void AnalyzeIntegrationProfile_OligoclonalDominance_ReturnsOligoclonalHazard()
    {
        var telemetry = new LentiviralIntegrationTelemetry(
            TherapyBatchId: "CAR-T-EXPANDED-03",
            AverageVectorCopyNumber: 3.2,
            TotalUniqueIntegrationSites: 1_200,
            IntegrationsWithin50kbOfOncogeneTss: 8,
            DominantCloneAbundancePercent: 42.0, // > 30% dominance!
            ShannonClonalityEntropy: 1.6, // < 2.0
            SelfInactivatingSinArchitecturePresent: true
        );

        var result = _engine.AnalyzeIntegrationProfile(telemetry);

        Assert.NotNull(result);
        Assert.Equal("OLIGOCLONAL_EXPANSION_PRE_LEUKEMIC_DOMINANCE_HAZARD", result.OncogenesisRiskRegime);
        Assert.False(result.IsBatchSafeForInfusion);
        Assert.Contains("Clonal dominance detected", result.ClinicalReleaseGuidance);
    }
}
