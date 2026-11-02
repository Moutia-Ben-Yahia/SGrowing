using System;
using SGrowing.Api.Modules.Quantum.CavityOptomechanicsPhononSqueezing;
using Xunit;

namespace SGrowing.Tests.Quantum;

public sealed class CavityOptomechanicsPhononSqueezingTests
{
    private readonly CavityOptomechanicsPhononSqueezingEngine _engine = new();

    [Fact]
    public void AnalyzePhononSqueezing_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzePhononSqueezing(null!));
    }

    [Fact]
    public void AnalyzePhononSqueezing_EmptyResonatorId_ThrowsArgumentException()
    {
        var telemetry = new CavityOptomechanicsTelemetry(
            ResonatorId: "",
            MechanicalFrequencyMegahertz: 5.0,
            MechanicalQualityFactor: 1.2e7,
            CavityLinewidthKilohertz: 250.0,
            LaserDetuningMegahertz: -5.0,
            OptomechanicalCouplingRateG0Hertz: 200.0,
            IntracavityPhotonCount: 500_000,
            EnvironmentalBathTemperatureKelvin: 0.05,
            SqueezingDriveModulationDepthPercent: 20.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzePhononSqueezing(telemetry));
    }

    [Fact]
    public void AnalyzePhononSqueezing_DeepGroundState_ReturnsSubSqlSqueezedRegime()
    {
        var telemetry = new CavityOptomechanicsTelemetry(
            ResonatorId: "OPTOMECH-MEMBRANE-001",
            MechanicalFrequencyMegahertz: 10.0,
            MechanicalQualityFactor: 2.0e7,
            CavityLinewidthKilohertz: 200.0,
            LaserDetuningMegahertz: -10.0,
            OptomechanicalCouplingRateG0Hertz: 300.0,
            IntracavityPhotonCount: 1_200_000,
            EnvironmentalBathTemperatureKelvin: 0.02, // 20 mK
            SqueezingDriveModulationDepthPercent: 35.0
        );

        var result = _engine.AnalyzePhononSqueezing(telemetry);

        Assert.NotNull(result);
        Assert.Equal("OPTOMECH-MEMBRANE-001", result.ResonatorId);
        Assert.True(result.EffectiveCooledPhononOccupancy < 0.1);
        Assert.True(result.SqueezingBelowSqlDecibels >= 3.0);
        Assert.True(result.IsSubSqlSqueezingAchieved);
        Assert.Equal("DEEP_GROUND_STATE_SUB_SQL_SQUEEZED_REGIME", result.OptomechanicalRegime);
    }

    [Fact]
    public void AnalyzePhononSqueezing_BlueDetuning_ReturnsUnstableAmplificationWarning()
    {
        var telemetry = new CavityOptomechanicsTelemetry(
            ResonatorId: "OPTOMECH-BLUE-002",
            MechanicalFrequencyMegahertz: 5.0,
            MechanicalQualityFactor: 1.0e6,
            CavityLinewidthKilohertz: 300.0,
            LaserDetuningMegahertz: 5.0, // Blue detuned!
            OptomechanicalCouplingRateG0Hertz: 200.0,
            IntracavityPhotonCount: 200_000,
            EnvironmentalBathTemperatureKelvin: 0.1,
            SqueezingDriveModulationDepthPercent: 10.0
        );

        var result = _engine.AnalyzePhononSqueezing(telemetry);

        Assert.NotNull(result);
        Assert.Equal("UNSTABLE_BLUE_DETUNED_AMPLIFICATION_WARNING", result.OptomechanicalRegime);
        Assert.False(result.IsSubSqlSqueezingAchieved);
        Assert.Contains("blue-shifted", result.ControlGuidance);
    }

    [Fact]
    public void AnalyzePhononSqueezing_LowPowerInsufficientCooling_ReturnsThermalNoiseDominated()
    {
        var telemetry = new CavityOptomechanicsTelemetry(
            ResonatorId: "OPTOMECH-LOW-003",
            MechanicalFrequencyMegahertz: 2.0,
            MechanicalQualityFactor: 1.0e4,
            CavityLinewidthKilohertz: 1000.0,
            LaserDetuningMegahertz: -2.0,
            OptomechanicalCouplingRateG0Hertz: 50.0,
            IntracavityPhotonCount: 100, // tiny power
            EnvironmentalBathTemperatureKelvin: 4.0, // 4 K
            SqueezingDriveModulationDepthPercent: 5.0
        );

        var result = _engine.AnalyzePhononSqueezing(telemetry);

        Assert.NotNull(result);
        Assert.True(result.OptomechanicalCooperativity < 1.0);
        Assert.Equal("THERMAL_NOISE_DOMINATED_INSUFFICIENT_COOLING", result.OptomechanicalRegime);
        Assert.False(result.IsSubSqlSqueezingAchieved);
    }
}
