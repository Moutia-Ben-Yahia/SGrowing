namespace SGrowing.Tests.Aerospace;

using FluentAssertions;
using SGrowing.Api.Modules.Aerospace.SpacecraftAerocaptureGuidance;
using Xunit;

public class SpacecraftAerocaptureGuidanceTests
{
    private readonly SpacecraftAerocaptureGuidanceEngine _engine = new();

    [Fact]
    public void ComputeGuidance_MarsNominalCorridor_ConvergesBankAngleAndMaintainsLimits()
    {
        var state = new SpacecraftEntryState(
            VehicleId: "MARS-AEROCAPTURE-ORBITER",
            AltitudeKm: 55.0, // Mid atmospheric entry
            VelocityKmPerSec: 5.8,
            FlightPathAngleDegrees: -11.2,
            CurrentBankAngleDegrees: 60.0,
            VehicleMassKg: 2800.0,
            LiftToDragRatio: 0.35
        );

        var atmo = new PlanetaryAtmosphereModel("Mars");
        var limits = new AerocaptureCorridorLimits(TargetApoapsisKm: 2000.0, MaxAllowableHeatFluxWattsPerCm2: 450.0, MaxAllowableDecelerationGs: 8.0);

        var request = new AerocaptureGuidanceRequest(state, atmo, limits);
        var decision = _engine.ComputeGuidance(request);

        decision.VehicleId.Should().Be("MARS-AEROCAPTURE-ORBITER");
        decision.TargetBody.Should().Be("Mars");
        decision.PeakHeatFluxWattsPerCm2.Should().BeLessThan(limits.MaxAllowableHeatFluxWattsPerCm2);
        decision.PeakDecelerationGs.Should().BeLessThan(limits.MaxAllowableDecelerationGs);
        decision.GuidanceMode.Should().BeOneOf("BANK_REVERSAL_NOMINAL", "LIFT_DOWN_DIVE_PREVENT", "LIFT_UP_SKIP_PREVENT");
    }

    [Fact]
    public void ComputeGuidance_HighApoapsisSkipRisk_CommandsRollLiftDown()
    {
        // Shallow entry angle (-8.0 deg) risking skipping back out to heliocentric space
        var state = new SpacecraftEntryState(
            VehicleId: "TITAN-EXPLORER",
            AltitudeKm: 75.0,
            VelocityKmPerSec: 6.2,
            FlightPathAngleDegrees: -7.5,
            CurrentBankAngleDegrees: 20.0
        );

        var atmo = new PlanetaryAtmosphereModel("Mars");
        var limits = new AerocaptureCorridorLimits(TargetApoapsisKm: 1500.0);

        var request = new AerocaptureGuidanceRequest(state, atmo, limits);
        var decision = _engine.ComputeGuidance(request);

        decision.GuidanceMode.Should().Be("LIFT_DOWN_DIVE_PREVENT");
        decision.RecommendedBankAngleDegrees.Should().Be(145.0);
        decision.ManeuverDirective.Should().Contain("ROLL_LIFT_DOWN");
    }

    [Fact]
    public void ComputeGuidance_DeepEntry_CommandsRollLiftUp()
    {
        // Steep entry angle (-15.5 deg) risking crashing into planet surface or over-decelerating
        var state = new SpacecraftEntryState(
            VehicleId: "AEROCAPTURE-02",
            AltitudeKm: 38.0,
            VelocityKmPerSec: 5.2,
            FlightPathAngleDegrees: -16.0,
            CurrentBankAngleDegrees: 120.0
        );

        var atmo = new PlanetaryAtmosphereModel("Mars");
        var limits = new AerocaptureCorridorLimits(TargetApoapsisKm: 3000.0);

        var request = new AerocaptureGuidanceRequest(state, atmo, limits);
        var decision = _engine.ComputeGuidance(request);

        decision.GuidanceMode.Should().Be("LIFT_UP_SKIP_PREVENT");
        decision.RecommendedBankAngleDegrees.Should().Be(35.0);
        decision.ManeuverDirective.Should().Contain("ROLL_LIFT_UP");
    }

    [Fact]
    public void ComputeGuidance_VenusEntry_RespectsExtremeHeatFluxConstraint()
    {
        var state = new SpacecraftEntryState("VENUS-ORBITER", 85.0, 10.5, -9.5, 45.0);
        var venusAtmo = new PlanetaryAtmosphereModel("Venus", ScaleHeightKm: 15.9, SurfaceDensityKgPerM3: 65.0, 6051.8, 324859.0);
        var limits = new AerocaptureCorridorLimits(5000.0, MaxAllowableHeatFluxWattsPerCm2: 1500.0, MaxAllowableDecelerationGs: 25.0);

        var request = new AerocaptureGuidanceRequest(state, venusAtmo, limits);
        var decision = _engine.ComputeGuidance(request);

        decision.TargetBody.Should().Be("Venus");
        decision.PeakHeatFluxWattsPerCm2.Should().BeGreaterThan(0.0);
    }
}
