namespace SGrowing.Tests.Aerospace;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.ElectrodynamicTetherDeorbit;
using Xunit;

public class ElectrodynamicTetherDeorbitTests
{
    private readonly ElectrodynamicTetherDeorbitEngine _engine = new();

    [Fact]
    public void AnalyzeElectrodynamicTether_NominalLeoSweep_ReturnsSteadyStateDeorbiting()
    {
        var telemetry = new ElectrodynamicTetherTelemetry(
            SatelliteId: "SAT-DEORBIT-01",
            OrbitalAltitudeKilometers: 520.0,
            OrbitalVelocityKmPerSec: 7.6,
            GeomagneticFieldMicroTesla: 42.0,
            TetherLengthKilometers: 5.0,
            TetherInducedCurrentAmperes: 1.4,
            IonosphericPlasmaDensityPerCubicCm: 2.5e5,
            TetherLibrationalSwingAngleDegrees: 7.5,
            TetherTensionNewtons: 16.5
        );

        var result = _engine.AnalyzeElectrodynamicTether(telemetry);

        result.SatelliteId.Should().Be("SAT-DEORBIT-01");
        result.DeorbitRegime.Should().Be("STEADY_STATE_LORENTZ_DEORBITING");
        result.IsDeorbitingOptimal.Should().BeTrue();
        result.InducedMotionalEmfVolts.Should().BeApproximately(1596.0, 5.0);
        result.LorentzDecelerationDragNewtons.Should().BeGreaterThan(0.25);
        result.ProjectedAltitudeLossKmPerDay.Should().BeGreaterThan(4.0);
        result.ElectrodynamicControlGuidance.Should().ContainEquivalentOf("Propellantless orbital decay");
    }

    [Fact]
    public void AnalyzeElectrodynamicTether_ExcessiveLibrationSwing_ReturnsLibrationalInstability()
    {
        var telemetry = new ElectrodynamicTetherTelemetry(
            SatelliteId: "SAT-DEORBIT-02",
            OrbitalAltitudeKilometers: 480.0,
            OrbitalVelocityKmPerSec: 7.6,
            GeomagneticFieldMicroTesla: 40.0,
            TetherLengthKilometers: 4.5,
            TetherInducedCurrentAmperes: 1.1,
            IonosphericPlasmaDensityPerCubicCm: 1.8e5,
            TetherLibrationalSwingAngleDegrees: 29.5, // Exceeds 24 deg
            TetherTensionNewtons: 12.0
        );

        var result = _engine.AnalyzeElectrodynamicTether(telemetry);

        result.DeorbitRegime.Should().Be("LIBRATIONAL_RESONANCE_INSTABILITY");
        result.IsDeorbitingOptimal.Should().BeFalse();
        result.ElectrodynamicControlGuidance.Should().ContainEquivalentOf("gravitational pendular oscillations");
    }

    [Fact]
    public void AnalyzeElectrodynamicTether_LowPlasmaDensity_ReturnsCurrentStarvation()
    {
        var telemetry = new ElectrodynamicTetherTelemetry(
            SatelliteId: "SAT-DEORBIT-03",
            OrbitalAltitudeKilometers: 650.0,
            OrbitalVelocityKmPerSec: 7.5,
            GeomagneticFieldMicroTesla: 38.0,
            TetherLengthKilometers: 4.0,
            TetherInducedCurrentAmperes: 0.15, // Starved current < 0.20 A
            IonosphericPlasmaDensityPerCubicCm: 6.0e3, // Below 1e4 /cm3
            TetherLibrationalSwingAngleDegrees: 6.0,
            TetherTensionNewtons: 10.0
        );

        var result = _engine.AnalyzeElectrodynamicTether(telemetry);

        result.DeorbitRegime.Should().Be("IONOSPHERIC_CURRENT_STARVATION");
        result.IsDeorbitingOptimal.Should().BeFalse();
        result.ElectrodynamicControlGuidance.Should().ContainEquivalentOf("hollow cathode gas discharge");
    }

    [Fact]
    public void AnalyzeElectrodynamicTether_SlackTether_ReturnsZeroTensionHazard()
    {
        var telemetry = new ElectrodynamicTetherTelemetry(
            SatelliteId: "SAT-DEORBIT-04",
            OrbitalAltitudeKilometers: 450.0,
            OrbitalVelocityKmPerSec: 7.6,
            GeomagneticFieldMicroTesla: 42.0,
            TetherLengthKilometers: 5.0,
            TetherInducedCurrentAmperes: 1.0,
            IonosphericPlasmaDensityPerCubicCm: 2.0e5,
            TetherLibrationalSwingAngleDegrees: 5.0,
            TetherTensionNewtons: 0.45 // Slack tension < 1.0 N
        );

        var result = _engine.AnalyzeElectrodynamicTether(telemetry);

        result.DeorbitRegime.Should().Be("ZERO_TENSION_SLACK_SEVERANCE_HAZARD");
        result.IsDeorbitingOptimal.Should().BeFalse();
        result.ElectrodynamicControlGuidance.Should().ContainEquivalentOf("recoil snapping");
    }

    [Fact]
    public void AnalyzeElectrodynamicTether_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeElectrodynamicTether(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
