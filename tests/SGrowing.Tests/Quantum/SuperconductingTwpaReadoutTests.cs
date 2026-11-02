using System;
using SGrowing.Api.Modules.Quantum.SuperconductingTwpaReadout;
using Xunit;

namespace SGrowing.Tests.Quantum;

public sealed class SuperconductingTwpaReadoutTests
{
    private readonly SuperconductingTwpaReadoutEngine _engine = new();

    [Fact]
    public void AnalyzeTwpaPerformance_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeTwpaPerformance(null!));
    }

    [Fact]
    public void AnalyzeTwpaPerformance_EmptyAmplifierId_ThrowsArgumentException()
    {
        var telemetry = new SuperconductingTwpaTelemetry(
            AmplifierId: "",
            CenterFrequencyGigahertz: 6.0,
            UsableBandwidthGigahertz: 3.0,
            AveragePowerGainDecibels: 20.0,
            GainRippleDecibels: 1.5,
            InputOneDbCompressionPowerDbm: -100.0,
            AddedNoisePhotons: 0.6,
            PumpPowerDbm: -55.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeTwpaPerformance(telemetry));
    }

    [Fact]
    public void AnalyzeTwpaPerformance_QuantumLimitedBroadband_ReturnsQuantumLimitedRegime()
    {
        var telemetry = new SuperconductingTwpaTelemetry(
            AmplifierId: "TWPA-JPA-BROADBAND-01",
            CenterFrequencyGigahertz: 6.5,
            UsableBandwidthGigahertz: 4.0, // 4 GHz bandwidth -> 20 qubits
            AveragePowerGainDecibels: 22.0, // >= 20 dB
            GainRippleDecibels: 1.6,
            InputOneDbCompressionPowerDbm: -96.0,
            AddedNoisePhotons: 0.65, // Near SQL 0.5
            PumpPowerDbm: -54.0
        );

        var result = _engine.AnalyzeTwpaPerformance(telemetry);

        Assert.NotNull(result);
        Assert.Equal("TWPA-JPA-BROADBAND-01", result.AmplifierId);
        Assert.True(result.MultiplexedQubitReadoutCapacity >= 15);
        Assert.True(result.IsQuantumLimitedFidelityAchieved);
        Assert.Equal("QUANTUM_LIMITED_BROADBAND_PARAMETRIC_AMPLIFICATION_REGIME", result.TwpaOperatingRegime);
        Assert.Contains("Near-quantum-limited performance confirmed", result.MicrowaveEngineeringGuidance);
    }

    [Fact]
    public void AnalyzeTwpaPerformance_ExcessNoise_ReturnsNoiseHazard()
    {
        var telemetry = new SuperconductingTwpaTelemetry(
            AmplifierId: "TWPA-HOT-PUMP-02",
            CenterFrequencyGigahertz: 6.0,
            UsableBandwidthGigahertz: 2.5,
            AveragePowerGainDecibels: 18.0,
            GainRippleDecibels: 2.0,
            InputOneDbCompressionPowerDbm: -105.0,
            AddedNoisePhotons: 4.2, // > 3.0 photons!
            PumpPowerDbm: -45.0
        );

        var result = _engine.AnalyzeTwpaPerformance(telemetry);

        Assert.NotNull(result);
        Assert.Equal("EXCESS_THERMAL_OR_PUMP_PHASE_NOISE_HAZARD", result.TwpaOperatingRegime);
        Assert.False(result.IsQuantumLimitedFidelityAchieved);
        Assert.Contains("significantly exceeds quantum limits", result.MicrowaveEngineeringGuidance);
    }

    [Fact]
    public void AnalyzeTwpaPerformance_LowGain_ReturnsInsufficientGain()
    {
        var telemetry = new SuperconductingTwpaTelemetry(
            AmplifierId: "TWPA-WEAK-PUMP-03",
            CenterFrequencyGigahertz: 5.5,
            UsableBandwidthGigahertz: 2.0,
            AveragePowerGainDecibels: 11.0, // < 15 dB!
            GainRippleDecibels: 1.0,
            InputOneDbCompressionPowerDbm: -110.0,
            AddedNoisePhotons: 0.8,
            PumpPowerDbm: -70.0
        );

        var result = _engine.AnalyzeTwpaPerformance(telemetry);

        Assert.NotNull(result);
        Assert.Equal("INSUFFICIENT_PARAMETRIC_GAIN_PUMP_STARVATION", result.TwpaOperatingRegime);
        Assert.False(result.IsQuantumLimitedFidelityAchieved);
        Assert.Contains("insufficient for single-shot qubit readout", result.MicrowaveEngineeringGuidance);
    }
}
