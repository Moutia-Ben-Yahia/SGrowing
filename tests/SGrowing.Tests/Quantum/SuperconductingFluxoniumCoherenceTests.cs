using System;
using SGrowing.Api.Modules.Quantum.SuperconductingFluxoniumCoherence;
using Xunit;

namespace SGrowing.Tests.Quantum;

public sealed class SuperconductingFluxoniumCoherenceTests
{
    private readonly SuperconductingFluxoniumCoherenceEngine _engine = new();

    [Fact]
    public void AnalyzeFluxoniumCoherence_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeFluxoniumCoherence(null!));
    }

    [Fact]
    public void AnalyzeFluxoniumCoherence_EmptyQubitId_ThrowsArgumentException()
    {
        var telemetry = new SuperconductingFluxoniumTelemetry(
            QubitId: "",
            ChargingEnergyGigahertz: 1.0,
            InductiveEnergyGigahertz: 0.5,
            JosephsonEnergyGigahertz: 4.5,
            ExternalFluxBiasPhi0: 0.50,
            ReadoutResonatorFrequencyGigahertz: 7.2,
            ResonatorCouplingRateMegahertz: 60.0,
            MeasuredT1RelaxationMicroseconds: 1200.0,
            MeasuredT2DephasingMicroseconds: 850.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeFluxoniumCoherence(telemetry));
    }

    [Fact]
    public void AnalyzeFluxoniumCoherence_SweetSpotLongCoherence_ReturnsSweetSpotRegime()
    {
        var telemetry = new SuperconductingFluxoniumTelemetry(
            QubitId: "FLUXONIUM-Q1-MILLISEC",
            ChargingEnergyGigahertz: 0.95,
            InductiveEnergyGigahertz: 0.48,
            JosephsonEnergyGigahertz: 4.2,
            ExternalFluxBiasPhi0: 0.50, // Perfect sweet spot
            ReadoutResonatorFrequencyGigahertz: 7.1,
            ResonatorCouplingRateMegahertz: 65.0,
            MeasuredT1RelaxationMicroseconds: 1450.0, // > 1 ms
            MeasuredT2DephasingMicroseconds: 920.0
        );

        var result = _engine.AnalyzeFluxoniumCoherence(telemetry);

        Assert.NotNull(result);
        Assert.Equal("FLUXONIUM-Q1-MILLISEC", result.QubitId);
        Assert.True(result.IsFaultTolerantCoherenceAchieved);
        Assert.Equal("SWEET_SPOT_PROTECTED_ULTRA_LONG_COHERENCE_FLUXONIUM", result.QubitCoherenceRegime);
        Assert.Contains("millisecond-scale T1", result.ControlGuidance);
    }

    [Fact]
    public void AnalyzeFluxoniumCoherence_OffSweetSpot_ReturnsDephasingHazard()
    {
        var telemetry = new SuperconductingFluxoniumTelemetry(
            QubitId: "FLUXONIUM-OFF-SWEET",
            ChargingEnergyGigahertz: 1.0,
            InductiveEnergyGigahertz: 0.5,
            JosephsonEnergyGigahertz: 4.5,
            ExternalFluxBiasPhi0: 0.42, // Deviation > 0.05
            ReadoutResonatorFrequencyGigahertz: 7.2,
            ResonatorCouplingRateMegahertz: 60.0,
            MeasuredT1RelaxationMicroseconds: 800.0,
            MeasuredT2DephasingMicroseconds: 50.0
        );

        var result = _engine.AnalyzeFluxoniumCoherence(telemetry);

        Assert.NotNull(result);
        Assert.Equal("OFF_SWEET_SPOT_FLUX_NOISE_DEPHASING_HAZARD", result.QubitCoherenceRegime);
        Assert.False(result.IsFaultTolerantCoherenceAchieved);
        Assert.Contains("deviates significantly", result.ControlGuidance);
    }

    [Fact]
    public void AnalyzeFluxoniumCoherence_DielectricLoss_ReturnsDielectricLossDominated()
    {
        var telemetry = new SuperconductingFluxoniumTelemetry(
            QubitId: "FLUXONIUM-LOSSY-03",
            ChargingEnergyGigahertz: 1.0,
            InductiveEnergyGigahertz: 0.5,
            JosephsonEnergyGigahertz: 4.5,
            ExternalFluxBiasPhi0: 0.50,
            ReadoutResonatorFrequencyGigahertz: 7.2,
            ResonatorCouplingRateMegahertz: 60.0,
            MeasuredT1RelaxationMicroseconds: 45.0, // < 100 us
            MeasuredT2DephasingMicroseconds: 30.0
        );

        var result = _engine.AnalyzeFluxoniumCoherence(telemetry);

        Assert.NotNull(result);
        Assert.Equal("DIELECTRIC_OR_QUASIPARTICLE_LOSS_DOMINATED", result.QubitCoherenceRegime);
        Assert.False(result.IsFaultTolerantCoherenceAchieved);
    }
}
