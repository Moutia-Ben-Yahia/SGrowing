namespace SGrowing.Tests.Aerospace;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.SolarSailRadiationPressure;
using Xunit;

public class SolarSailRadiationPressureTests
{
    private readonly SolarSailRadiationPressureEngine _engine = new();

    [Fact]
    public void AnalyzeSolarSail_NominalInterplanetaryFlight_ReturnsOptimalPropulsion()
    {
        var telemetry = new SolarSailTelemetry(
            SpacecraftId: "SAIL-DEEP-01",
            HeliocentricDistanceAstronomicalUnits: 1.0, // 1 AU
            TotalSailAreaSquareMeters: 1200.0,
            TotalSpacecraftMassKg: 32.0,
            SailSpecularReflectivityCoefficient: 0.91,
            SailConePitchAngleDegrees: 15.0,
            MembraneFilmThicknessMicrometers: 2.5,
            SailMembraneTemperatureKelvin: 340.0
        );

        var result = _engine.AnalyzeSolarSail(telemetry);

        result.SpacecraftId.Should().Be("SAIL-DEEP-01");
        result.SailNavigationRegime.Should().Be("OPTIMAL_PHOTONIC_RADIATION_PRESSURE_PROPULSION");
        result.IsTrajectoryNominal.Should().BeTrue();
        result.SolarRadiationPressureMicroPascals.Should().BeApproximately(4.563, 0.01);
        result.GeneratedPhotonThrustForceMilliNewtons.Should().BeGreaterThan(9.0);
        result.CharacteristicAccelerationMmPerSec2.Should().BeGreaterThan(0.25);
        result.SolarLightnessFactorBeta.Should().BeGreaterThan(0.04);
        result.AttitudeControlGuidance.Should().ContainEquivalentOf("Continuous propellantless photon momentum transfer");
    }

    [Fact]
    public void AnalyzeSolarSail_HighThermalFlux_ReturnsOverheatingHazard()
    {
        var telemetry = new SolarSailTelemetry(
            SpacecraftId: "SAIL-DEEP-02",
            HeliocentricDistanceAstronomicalUnits: 0.35, // Close perihelion
            TotalSailAreaSquareMeters: 800.0,
            TotalSpacecraftMassKg: 25.0,
            SailSpecularReflectivityCoefficient: 0.88,
            SailConePitchAngleDegrees: 10.0,
            MembraneFilmThicknessMicrometers: 2.5,
            SailMembraneTemperatureKelvin: 545.0 // Exceeds 520 K
        );

        var result = _engine.AnalyzeSolarSail(telemetry);

        result.SailNavigationRegime.Should().Be("MEMBRANE_OVERHEATING_THERMAL_DEGRADATION_HAZARD");
        result.IsTrajectoryNominal.Should().BeFalse();
        result.AttitudeControlGuidance.Should().ContainEquivalentOf("thermal degradation limit");
    }

    [Fact]
    public void AnalyzeSolarSail_HighPitchAngle_ReturnsThrustCollapseWarning()
    {
        var telemetry = new SolarSailTelemetry(
            SpacecraftId: "SAIL-DEEP-03",
            HeliocentricDistanceAstronomicalUnits: 1.2,
            TotalSailAreaSquareMeters: 1000.0,
            TotalSpacecraftMassKg: 30.0,
            SailSpecularReflectivityCoefficient: 0.90,
            SailConePitchAngleDegrees: 68.0, // Exceeds 60 deg
            MembraneFilmThicknessMicrometers: 2.5,
            SailMembraneTemperatureKelvin: 310.0
        );

        var result = _engine.AnalyzeSolarSail(telemetry);

        result.SailNavigationRegime.Should().Be("HIGH_PITCH_THRUST_COLLAPSE_WARNING");
        result.IsTrajectoryNominal.Should().BeFalse();
        result.AttitudeControlGuidance.Should().ContainEquivalentOf("severe photonic thrust collapse");
    }

    [Fact]
    public void AnalyzeSolarSail_CoatingDegradation_ReturnsCoatingDeficit()
    {
        var telemetry = new SolarSailTelemetry(
            SpacecraftId: "SAIL-DEEP-04",
            HeliocentricDistanceAstronomicalUnits: 1.5,
            TotalSailAreaSquareMeters: 1000.0,
            TotalSpacecraftMassKg: 30.0,
            SailSpecularReflectivityCoefficient: 0.65, // Below 0.75
            SailConePitchAngleDegrees: 20.0,
            MembraneFilmThicknessMicrometers: 2.5,
            SailMembraneTemperatureKelvin: 280.0
        );

        var result = _engine.AnalyzeSolarSail(telemetry);

        result.SailNavigationRegime.Should().Be("REFLECTIVE_COATING_DEGRADATION_DEFICIT");
        result.IsTrajectoryNominal.Should().BeFalse();
        result.AttitudeControlGuidance.Should().ContainEquivalentOf("recalibrate heliocentric transfer orbit");
    }

    [Fact]
    public void AnalyzeSolarSail_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeSolarSail(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
