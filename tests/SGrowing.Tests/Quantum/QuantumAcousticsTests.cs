using SGrowing.Api.Modules.Quantum.SuperconductingQuantumAcoustics;
using Xunit;

namespace SGrowing.Tests.Quantum;

public class QuantumAcousticsTests
{
    private readonly QuantumAcousticsEngine _engine = new();

    [Fact]
    public void AnalyzeAcoustics_NominalPhononFockState_ReturnsSynthesisReady()
    {
        var telemetry = new QuantumAcousticsTelemetry
        {
            ResonatorUnitId = "HBAR-QAR-001",
            PiezoelectricSubstrate = "LithiumNiobate_LiNbO3",
            ResonatorFrequencyGigaHertz = 4.85,
            MechanicalQualityFactor = 6.2e6,
            PhononCoherenceLifetimeMicroseconds = 92.0,
            TransmonAcousticCouplingMhz = 3.2,
            CryostatPlateTemperatureMilliKelvin = 12.0,
            MeasuredWignerOriginValue = -0.52,
            TargetPhononFockNumber = 1,
            SinglePhononGenerationFidelityPercent = 95.8
        };

        var result = _engine.AnalyzeAcoustics(telemetry);

        Assert.NotNull(result);
        Assert.Equal("HBAR-QAR-001", result.ResonatorUnitId);
        Assert.True(result.IsMechanicalQualityOptimal);
        Assert.True(result.IsThermalPhononBathSuppressed);
        Assert.True(result.IsNonClassicalWignerConfirmed);
        Assert.True(result.IsPhononFockStateSynthesisReady);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.TransmonPhononCooperativityC > 1000.0);
        Assert.True(result.AcousticWignerNegativityMargin > 0.40);
    }

    [Fact]
    public void AnalyzeAcoustics_LowMechanicalQuality_TriggersAcousticDecayAlert()
    {
        var telemetry = new QuantumAcousticsTelemetry
        {
            ResonatorUnitId = "HBAR-QAR-002",
            MechanicalQualityFactor = 2.1e6,
            PhononCoherenceLifetimeMicroseconds = 35.0,
            MeasuredWignerOriginValue = -0.48
        };

        var result = _engine.AnalyzeAcoustics(telemetry);

        Assert.False(result.IsMechanicalQualityOptimal);
        Assert.False(result.IsPhononFockStateSynthesisReady);
        Assert.Contains("ACOUSTIC_PHONON_DISSIPATION_DECAY", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeAcoustics_DilutionFridgeHeating_TriggersThermalBathAlert()
    {
        var telemetry = new QuantumAcousticsTelemetry
        {
            ResonatorUnitId = "HBAR-QAR-003",
            CryostatPlateTemperatureMilliKelvin = 35.0,
            MeasuredWignerOriginValue = -0.46
        };

        var result = _engine.AnalyzeAcoustics(telemetry);

        Assert.False(result.IsThermalPhononBathSuppressed);
        Assert.False(result.IsPhononFockStateSynthesisReady);
        Assert.Contains("THERMAL_PHONON_BATH_EXCITATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeAcoustics_DecoheredWignerPurity_TriggersNegativityCollapse()
    {
        var telemetry = new QuantumAcousticsTelemetry
        {
            ResonatorUnitId = "HBAR-QAR-004",
            MeasuredWignerOriginValue = 0.05
        };

        var result = _engine.AnalyzeAcoustics(telemetry);

        Assert.False(result.IsNonClassicalWignerConfirmed);
        Assert.False(result.IsPhononFockStateSynthesisReady);
        Assert.Contains("WIGNER_NEGATIVITY_DECOHERENCE_COLLAPSE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeAcoustics_LowFidelity_BlocksSynthesisReadiness()
    {
        var telemetry = new QuantumAcousticsTelemetry
        {
            ResonatorUnitId = "HBAR-QAR-005",
            SinglePhononGenerationFidelityPercent = 88.5
        };

        var result = _engine.AnalyzeAcoustics(telemetry);

        Assert.True(result.IsMechanicalQualityOptimal);
        Assert.False(result.IsPhononFockStateSynthesisReady);
    }
}
