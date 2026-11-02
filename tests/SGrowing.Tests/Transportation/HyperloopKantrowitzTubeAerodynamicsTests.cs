namespace SGrowing.Tests.Transportation;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Transportation.HyperloopKantrowitzTubeAerodynamics;
using Xunit;

public class HyperloopKantrowitzTubeAerodynamicsTests
{
    private readonly HyperloopKantrowitzTubeAerodynamicsEngine _engine = new();

    [Fact]
    public void AnalyzeTubeAerodynamics_SubChokingBypassActive_ReturnsOptimalAerodynamics()
    {
        var telemetry = new HyperloopTubeTelemetry(
            PodId: "HYPER-POD-01",
            PodVelocityMetersPerSecond: 210.0, // Mach ~0.61
            TubeStaticPressurePascals: 150.0,
            TubeTemperatureKelvin: 293.15,
            PodFrontalAreaSquareMeters: 3.5,
            TubeCrossSectionalAreaSquareMeters: 10.0,
            PodDragCoefficient: 0.18,
            CompressorBypassActive: true,
            CompressorMassFlowRateKgPerSec: 8.5
        );

        var result = _engine.AnalyzeTubeAerodynamics(telemetry);

        result.PodId.Should().Be("HYPER-POD-01");
        result.KantrowitzChokingRegime.Should().Be("SUB_CHOKING_OPTIMAL_BYPASS_AERODYNAMICS");
        result.IsAerodynamicallyStable.Should().BeTrue();
        result.PodMachNumber.Should().BeInRange(0.55, 0.65);
        result.BlockageRatio.Should().Be(0.35);
        result.AerodynamicPistonDragKiloNewtons.Should().BeLessThan(25.0);
        result.AerodynamicControlGuidance.Should().ContainEquivalentOf("operating safely below Kantrowitz");
    }

    [Fact]
    public void AnalyzeTubeAerodynamics_KantrowitzChokedNoCompressor_ReturnsPistonShockWave()
    {
        var telemetry = new HyperloopTubeTelemetry(
            PodId: "HYPER-POD-02",
            PodVelocityMetersPerSecond: 285.0, // High transonic ~ Mach 0.83
            TubeStaticPressurePascals: 180.0,
            TubeTemperatureKelvin: 293.15,
            PodFrontalAreaSquareMeters: 4.8, // High blockage 0.48
            TubeCrossSectionalAreaSquareMeters: 10.0,
            PodDragCoefficient: 0.22,
            CompressorBypassActive: false, // Inactive compressor causes choking
            CompressorMassFlowRateKgPerSec: 0.0
        );

        var result = _engine.AnalyzeTubeAerodynamics(telemetry);

        result.KantrowitzChokingRegime.Should().Be("KANTROWITZ_CHOKING_PISTON_SHOCK_WAVE");
        result.IsAerodynamicallyStable.Should().BeFalse();
        result.PodMachNumber.Should().BeGreaterThan(result.KantrowitzMachLimit);
        result.AerodynamicControlGuidance.Should().ContainEquivalentOf("choked at sonic bypass threshold");
    }

    [Fact]
    public void AnalyzeTubeAerodynamics_TubeVacuumDegraded_ReturnsVacuumDegradation()
    {
        var telemetry = new HyperloopTubeTelemetry(
            PodId: "HYPER-POD-03",
            PodVelocityMetersPerSecond: 150.0,
            TubeStaticPressurePascals: 2200.0, // Exceeds 1500 Pa
            TubeTemperatureKelvin: 293.15,
            PodFrontalAreaSquareMeters: 3.5,
            TubeCrossSectionalAreaSquareMeters: 10.0,
            PodDragCoefficient: 0.18,
            CompressorBypassActive: true,
            CompressorMassFlowRateKgPerSec: 5.0
        );

        var result = _engine.AnalyzeTubeAerodynamics(telemetry);

        result.KantrowitzChokingRegime.Should().Be("TUBE_VACUUM_DEPRESSURIZATION_DEGRADATION");
        result.IsAerodynamicallyStable.Should().BeFalse();
        result.AerodynamicControlGuidance.Should().ContainEquivalentOf("turbomolecular vacuum pump");
    }

    [Fact]
    public void AnalyzeTubeAerodynamics_HighPistonDragSurge_ReturnsDragMarginWarning()
    {
        var telemetry = new HyperloopTubeTelemetry(
            PodId: "HYPER-POD-04",
            PodVelocityMetersPerSecond: 320.0,
            TubeStaticPressurePascals: 1200.0, // High density and velocity with compressor on
            TubeTemperatureKelvin: 293.15,
            PodFrontalAreaSquareMeters: 5.5,
            TubeCrossSectionalAreaSquareMeters: 10.0,
            PodDragCoefficient: 0.35,
            CompressorBypassActive: true,
            CompressorMassFlowRateKgPerSec: 2.0 // Minimal bypass relief, causes high drag (>45 kN)
        );

        var result = _engine.AnalyzeTubeAerodynamics(telemetry);

        result.KantrowitzChokingRegime.Should().Be("HIGH_AERODYNAMIC_DRAG_MARGIN_WARNING");
        result.IsAerodynamicallyStable.Should().BeFalse();
        result.AerodynamicPistonDragKiloNewtons.Should().BeGreaterThan(3.0);
        result.AerodynamicControlGuidance.Should().ContainEquivalentOf("linear synchronous motor");
    }

    [Fact]
    public void AnalyzeTubeAerodynamics_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeTubeAerodynamics(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
