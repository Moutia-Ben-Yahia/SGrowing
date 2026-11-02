using System;
using SGrowing.Api.Modules.Quantum.DiamondColorCenterNanophotonics;
using Xunit;

namespace SGrowing.Tests.Quantum;

public sealed class DiamondColorCenterNanophotonicsTests
{
    private readonly DiamondColorCenterNanophotonicsEngine _engine = new();

    [Fact]
    public void AnalyzeNanophotonicInterface_OptimalParameters_ReturnsQuantumRepeaterNodeReady()
    {
        var telemetry = new DiamondColorCenterTelemetry
        {
            CavityId = "DIAMOND-CAV-001",
            DefectType = "SnV-",
            CavityQualityFactorQ = 40000.0,
            ModeVolumeVLambdaOverNCubed = 0.50,
            ZplWavelengthNm = 619.0,
            DetuningGhz = 0.5,
            DipoleOrientationAlignmentFactor = 0.95,
            IntrinsicDebyeWallerFactor = 0.60,
            OperatingTemperatureKelvin = 2.0,
            GroundStateSplittingGhz = 850.0
        };

        var result = _engine.AnalyzeNanophotonicInterface(telemetry);

        Assert.Equal("DIAMOND-CAV-001", result.CavityId);
        Assert.True(result.IsPurcellEnhancementSufficient);
        Assert.True(result.IsGuidedBranchingRatioOptimal);
        Assert.True(result.IsOrbitalDephasingSuppressed);
        Assert.True(result.PurcellFactorFp >= 25.0);
        Assert.True(result.GuidedModeBranchingRatioBetaZpl >= 0.85);
        Assert.Equal("OPTIMAL_QUANTUM_REPEATER_SPIN_PHOTON_NODE", result.OpticalInterfaceRegime);
    }

    [Fact]
    public void AnalyzeNanophotonicInterface_HighDetuning_TriggersSubPurcellDeficit()
    {
        var telemetry = new DiamondColorCenterTelemetry
        {
            CavityId = "DIAMOND-DETUNE-002",
            DefectType = "SnV-",
            CavityQualityFactorQ = 400.0, // Low Q
            ModeVolumeVLambdaOverNCubed = 2.0, // Large V
            ZplWavelengthNm = 619.0,
            DetuningGhz = 150.0, // Large detuning
            DipoleOrientationAlignmentFactor = 0.5,
            IntrinsicDebyeWallerFactor = 0.60,
            OperatingTemperatureKelvin = 2.0,
            GroundStateSplittingGhz = 850.0
        };

        var result = _engine.AnalyzeNanophotonicInterface(telemetry);

        Assert.False(result.IsPurcellEnhancementSufficient);
        Assert.Equal("SUB_PURCELL_RADIATIVE_COUPLING_DEFICIT", result.OpticalInterfaceRegime);
        Assert.Contains("Purcell factor", result.NanophotonicRecommendation);
    }

    [Fact]
    public void AnalyzeNanophotonicInterface_SmallSplittingHighTemp_TriggersDephasingCollapse()
    {
        var telemetry = new DiamondColorCenterTelemetry
        {
            CavityId = "DIAMOND-SIV-DEPHASE-003",
            DefectType = "SiV-",
            CavityQualityFactorQ = 45000.0,
            ModeVolumeVLambdaOverNCubed = 0.50,
            ZplWavelengthNm = 737.0,
            DetuningGhz = 0.2,
            DipoleOrientationAlignmentFactor = 0.95,
            IntrinsicDebyeWallerFactor = 0.05, // SiV has low DW0 ~5%
            OperatingTemperatureKelvin = 10.0, // High temperature for SiV
            GroundStateSplittingGhz = 50.0 // Small ground state splitting 50 GHz
        };

        var result = _engine.AnalyzeNanophotonicInterface(telemetry);

        Assert.False(result.IsOrbitalDephasingSuppressed);
        Assert.Equal("PHONON_INDUCED_ORBITAL_DEPHASING_COLLAPSE", result.OpticalInterfaceRegime);
        Assert.Contains("Orbital dephasing rate", result.NanophotonicRecommendation);
    }

    [Fact]
    public void AnalyzeNanophotonicInterface_LowDebyeWaller_TriggersSidebandLeakRegime()
    {
        var telemetry = new DiamondColorCenterTelemetry
        {
            CavityId = "DIAMOND-SIDEBAND-004",
            DefectType = "SiV-",
            CavityQualityFactorQ = 25000.0,
            ModeVolumeVLambdaOverNCubed = 0.55,
            ZplWavelengthNm = 737.0,
            DetuningGhz = 0.5,
            DipoleOrientationAlignmentFactor = 0.90,
            IntrinsicDebyeWallerFactor = 0.005, // Low DW0 ~ 0.5%
            OperatingTemperatureKelvin = 0.1, // Dilution fridge < 100 mK
            GroundStateSplittingGhz = 50.0
        };

        var result = _engine.AnalyzeNanophotonicInterface(telemetry);

        Assert.True(result.IsPurcellEnhancementSufficient);
        Assert.True(result.IsOrbitalDephasingSuppressed);
        Assert.False(result.IsGuidedBranchingRatioOptimal);
        Assert.Equal("LOW_ZPL_BRANCHING_PHONON_SIDEBAND_LEAK", result.OpticalInterfaceRegime);
    }

    [Fact]
    public void AnalyzeNanophotonicInterface_ZeroOrNegativeQualityFactor_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeNanophotonicInterface(null!));

        var invalidTelemetry = new DiamondColorCenterTelemetry
        {
            CavityId = "DIAMOND-INV",
            CavityQualityFactorQ = 0.0,
            ModeVolumeVLambdaOverNCubed = 0.5
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeNanophotonicInterface(invalidTelemetry));
    }
}
