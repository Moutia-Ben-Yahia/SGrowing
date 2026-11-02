using SGrowing.Api.Modules.Quantum.MajoranaZeroModeBraiding;
using Xunit;

namespace SGrowing.Tests.Quantum;

public class MajoranaBraidingTests
{
    private readonly MajoranaBraidingEngine _engine = new();

    [Fact]
    public void AnalyzeBraiding_NominalTopologicalNanowire_ReturnsBraidingVerified()
    {
        var telemetry = new MajoranaBraidingTelemetry
        {
            NanowireDeviceUid = "MZM-INAS-001",
            SemiconductorHeterostructure = "InAs_Al_EpitaxialNanowire",
            InPlaneZeemanFieldTesla = 0.85,
            ZeroBiasConductanceMicroSiemens = 76.8,
            TopologicalSuperconductingGapMicroEv = 42.5,
            QuasiparticlePoisoningRateKiloHertz = 0.85,
            MajoranaBraidingPhaseErrorDegrees = 0.95,
            BraidingOperationFidelityPercent = 99.4,
            NanowireLengthMicrons = 1.8,
            TopologicalProtectionGapRatio = 0.38
        };

        var result = _engine.AnalyzeBraiding(telemetry);

        Assert.NotNull(result);
        Assert.Equal("MZM-INAS-001", result.NanowireDeviceUid);
        Assert.True(result.IsZeroBiasPeakQuantized);
        Assert.True(result.IsTopologicalGapOpen);
        Assert.True(result.IsParityLifetimeProtected);
        Assert.True(result.IsNonAbelianBraidingVerified);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.ParityLifetimeMicroseconds > 1000.0);
        Assert.True(result.ConductanceQuantizationErrorPercent < 2.0);
    }

    [Fact]
    public void AnalyzeBraiding_CollapsedTopologicalGap_TriggersGapCollapseAlert()
    {
        var telemetry = new MajoranaBraidingTelemetry
        {
            NanowireDeviceUid = "MZM-INAS-002",
            TopologicalSuperconductingGapMicroEv = 15.0,
            TopologicalProtectionGapRatio = 0.12
        };

        var result = _engine.AnalyzeBraiding(telemetry);

        Assert.False(result.IsTopologicalGapOpen);
        Assert.False(result.IsNonAbelianBraidingVerified);
        Assert.Contains("MAJORANA_TOPOLOGICAL_GAP_COLLAPSE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeBraiding_HighQuasiparticlePoisoning_TriggersParityFlipAlert()
    {
        var telemetry = new MajoranaBraidingTelemetry
        {
            NanowireDeviceUid = "MZM-INAS-003",
            QuasiparticlePoisoningRateKiloHertz = 4.2
        };

        var result = _engine.AnalyzeBraiding(telemetry);

        Assert.False(result.IsParityLifetimeProtected);
        Assert.False(result.IsNonAbelianBraidingVerified);
        Assert.Contains("QUASIPARTICLE_POISONING_PARITY_FLIP", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeBraiding_ExcessivePhaseError_TriggersBraidingInfidelity()
    {
        var telemetry = new MajoranaBraidingTelemetry
        {
            NanowireDeviceUid = "MZM-INAS-004",
            MajoranaBraidingPhaseErrorDegrees = 3.6,
            BraidingOperationFidelityPercent = 97.2
        };

        var result = _engine.AnalyzeBraiding(telemetry);

        Assert.False(result.IsNonAbelianBraidingVerified);
        Assert.Contains("NON_ABELIAN_BRAIDING_PHASE_INFIDELITY", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeBraiding_OffQuantizedZbp_FailsQuantizationCheck()
    {
        var telemetry = new MajoranaBraidingTelemetry
        {
            NanowireDeviceUid = "MZM-INAS-005",
            ZeroBiasConductanceMicroSiemens = 62.0 // > 5% error from 77.48 uS
        };

        var result = _engine.AnalyzeBraiding(telemetry);

        Assert.False(result.IsZeroBiasPeakQuantized);
        Assert.False(result.IsNonAbelianBraidingVerified);
    }
}
