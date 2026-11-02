namespace SGrowing.Tests.Aerospace;

using System;
using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.AerogravityAssistTrajectory;
using Xunit;

public class AerogravityAssistTrajectoryTests
{
    private readonly AerogravityAssistTrajectoryEngine _engine = new();

    [Fact]
    public void AnalyzeAerogravityAssist_NominalMarsFlyby_ReturnsOptimalFlyby()
    {
        var telemetry = new AerogravityAssistTelemetry(
            SpacecraftId: "WAVERIDER-AGA-01",
            TargetPlanet: "Mars",
            AtmosphericEntryVelocityKmPerSec: 12.5,
            PeriapsisAltitudeKilometers: 55.0,
            WaveriderLiftToDragRatio: 3.5,
            InvertedBankAngleDegrees: 180.0,
            DynamicPressureKiloPascals: 45.0,
            StagnationHeatFluxMegawattsPerSquareMeter: 7.2,
            SpacecraftMassKilograms: 1200.0
        );

        var result = _engine.AnalyzeAerogravityAssist(telemetry);

        result.SpacecraftId.Should().Be("WAVERIDER-AGA-01");
        result.FlightRegime.Should().Be("AEROGRAVITY_ASSIST_OPTIMAL_FLYBY");
        result.IsTrajectoryNominal.Should().BeTrue();
        result.CentrifugalForceToGravityRatio.Should().BeGreaterThan(2.0);
        result.TrajectoryTurningAngleDegrees.Should().BeGreaterThan(50.0);
        result.DeltaVHyperbolicEquivalentKmPerSec.Should().BeGreaterThan(10.0);
        result.WaveriderFlightGuidance.Should().ContainEquivalentOf("downward lift securely counterbalancing");
    }

    [Fact]
    public void AnalyzeAerogravityAssist_ExtremeThermalHeating_ReturnsThermalSoakExcursion()
    {
        var telemetry = new AerogravityAssistTelemetry(
            SpacecraftId: "WAVERIDER-AGA-02",
            TargetPlanet: "Venus",
            AtmosphericEntryVelocityKmPerSec: 15.0,
            PeriapsisAltitudeKilometers: 65.0,
            WaveriderLiftToDragRatio: 3.2,
            InvertedBankAngleDegrees: 180.0,
            DynamicPressureKiloPascals: 55.0,
            StagnationHeatFluxMegawattsPerSquareMeter: 13.8, // Exceeds 11.5 MW/m2
            SpacecraftMassKilograms: 1200.0
        );

        var result = _engine.AnalyzeAerogravityAssist(telemetry);

        result.FlightRegime.Should().Be("HYPERSONIC_THERMAL_SOAK_EXCURSION");
        result.IsTrajectoryNominal.Should().BeFalse();
        result.WaveriderFlightGuidance.Should().ContainEquivalentOf("waverider TPS limit");
    }

    [Fact]
    public void AnalyzeAerogravityAssist_AeroelasticOverpressure_ReturnsStructuralOverpressure()
    {
        var telemetry = new AerogravityAssistTelemetry(
            SpacecraftId: "WAVERIDER-AGA-03",
            TargetPlanet: "Mars",
            AtmosphericEntryVelocityKmPerSec: 13.0,
            PeriapsisAltitudeKilometers: 42.0,
            WaveriderLiftToDragRatio: 3.0,
            InvertedBankAngleDegrees: 180.0,
            DynamicPressureKiloPascals: 88.0, // Exceeds 80 kPa
            StagnationHeatFluxMegawattsPerSquareMeter: 8.5,
            SpacecraftMassKilograms: 1200.0
        );

        var result = _engine.AnalyzeAerogravityAssist(telemetry);

        result.FlightRegime.Should().Be("AERODYNAMIC_STRUCTURAL_OVERPRESSURE");
        result.IsTrajectoryNominal.Should().BeFalse();
        result.WaveriderFlightGuidance.Should().ContainEquivalentOf("aeroelastic limit");
    }

    [Fact]
    public void AnalyzeAerogravityAssist_SuboptimalBankAngle_ReturnsCentrifugalPopOut()
    {
        var telemetry = new AerogravityAssistTelemetry(
            SpacecraftId: "WAVERIDER-AGA-04",
            TargetPlanet: "Mars",
            AtmosphericEntryVelocityKmPerSec: 12.0,
            PeriapsisAltitudeKilometers: 55.0,
            WaveriderLiftToDragRatio: 3.2,
            InvertedBankAngleDegrees: 135.0, // Deviates from 155-205 deg
            DynamicPressureKiloPascals: 45.0,
            StagnationHeatFluxMegawattsPerSquareMeter: 7.0,
            SpacecraftMassKilograms: 1200.0
        );

        var result = _engine.AnalyzeAerogravityAssist(telemetry);

        result.FlightRegime.Should().Be("CENTRIFUGAL_POP_OUT_UNLOCKED");
        result.IsTrajectoryNominal.Should().BeFalse();
        result.WaveriderFlightGuidance.Should().ContainEquivalentOf("downward lift trim");
    }

    [Fact]
    public void AnalyzeAerogravityAssist_NullTelemetry_ThrowsArgumentNullException()
    {
        var act = () => _engine.AnalyzeAerogravityAssist(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
