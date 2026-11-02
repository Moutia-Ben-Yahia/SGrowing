using SGrowing.Api.Modules.Quantum.SurfaceCodeLatticeSurgery;
using Xunit;

namespace SGrowing.Tests.Quantum;

public class SurfaceCodeLatticeTests
{
    private readonly SurfaceCodeLatticeEngine _engine = new();

    [Fact]
    public void AnalyzeLattice_NominalDistanceFivePatch_ReturnsFaultTolerant()
    {
        var telemetry = new SurfaceCodeLatticeTelemetry
        {
            LogicalQubitPatchUid = "SURF-D5-PATCH-01",
            CodeDistance = 5,
            PhysicalQubitErrorRate = 0.00065,
            DecoderLatencyMicroseconds = 0.78,
            StabilizerSyndromeFidelityPercent = 99.82,
            DefectChainWeightSpanRatio = 0.42,
            LatticeSurgeryMergeFidelityPercent = 99.45,
            LogicalErrorRatePerRound = 1.2e-8,
            IsBoundaryCommutationVerified = true
        };

        var result = _engine.AnalyzeLattice(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SURF-D5-PATCH-01", result.LogicalQubitPatchUid);
        Assert.True(result.IsPhysicalErrorBelowThreshold);
        Assert.True(result.IsDecodingLatencyRealTimeBounded);
        Assert.True(result.IsDefectChainTolerated);
        Assert.True(result.IsLatticeSurgeryFaultTolerant);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.LogicalSuppressionFactorLambda > 1.4);
        Assert.True(result.DecoderHeadroomMicroseconds > 1.5);
    }

    [Fact]
    public void AnalyzeLattice_DefectChainSpanExceedance_TriggersDefectLeakageAlert()
    {
        var telemetry = new SurfaceCodeLatticeTelemetry
        {
            LogicalQubitPatchUid = "SURF-D5-PATCH-02",
            DefectChainWeightSpanRatio = 0.65,
            LogicalErrorRatePerRound = 3.5e-5
        };

        var result = _engine.AnalyzeLattice(telemetry);

        Assert.False(result.IsDefectChainTolerated);
        Assert.False(result.IsLatticeSurgeryFaultTolerant);
        Assert.Contains("SURFACE_CODE_DEFECT_STRING_LEAKAGE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeLattice_HighDecoderLatency_TriggersSyndromeBacklogAlert()
    {
        var telemetry = new SurfaceCodeLatticeTelemetry
        {
            LogicalQubitPatchUid = "SURF-D5-PATCH-03",
            DecoderLatencyMicroseconds = 2.85
        };

        var result = _engine.AnalyzeLattice(telemetry);

        Assert.False(result.IsDecodingLatencyRealTimeBounded);
        Assert.False(result.IsLatticeSurgeryFaultTolerant);
        Assert.Contains("SYNDROME_DECODER_LATENCY_BACKLOG", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeLattice_BoundaryCommutationBreakdown_TriggersDislocationAlert()
    {
        var telemetry = new SurfaceCodeLatticeTelemetry
        {
            LogicalQubitPatchUid = "SURF-D5-PATCH-04",
            IsBoundaryCommutationVerified = false,
            LatticeSurgeryMergeFidelityPercent = 97.4
        };

        var result = _engine.AnalyzeLattice(telemetry);

        Assert.False(result.IsLatticeSurgeryFaultTolerant);
        Assert.Contains("LATTICE_SURGERY_BOUNDARY_MERGE_DISLOCATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeLattice_AboveThresholdPhysicalError_FailsThresholdCheck()
    {
        var telemetry = new SurfaceCodeLatticeTelemetry
        {
            LogicalQubitPatchUid = "SURF-D5-PATCH-05",
            PhysicalQubitErrorRate = 0.0018 // Above 0.1% threshold
        };

        var result = _engine.AnalyzeLattice(telemetry);

        Assert.False(result.IsPhysicalErrorBelowThreshold);
        Assert.False(result.IsLatticeSurgeryFaultTolerant);
    }
}
