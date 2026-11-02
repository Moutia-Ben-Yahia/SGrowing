using System;
using SGrowing.Api.Modules.Aerospace.HypersonicAerocaptureOrbitalInsertion;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class HypersonicAerocaptureOrbitalInsertionTests
{
    private readonly HypersonicAerocaptureOrbitalInsertionEngine _engine = new();

    [Fact]
    public void AnalyzeAerocaptureTrajectory_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeAerocaptureTrajectory(null!));
    }

    [Fact]
    public void AnalyzeAerocaptureTrajectory_NegativeCorridorWidth_ThrowsArgumentException()
    {
        var telemetry = new HypersonicAerocaptureTelemetry(
            SpacecraftId: "MARS-AERO-01",
            TargetBody: "Mars",
            AtmosphericEntryVelocityKilometersPerSec: 6.2,
            EntryFlightPathAngleDegrees: -11.5,
            NominalCorridorCenterDegrees: -11.4,
            CorridorWidthDegrees: -0.5, // Negative!
            HypersonicLiftToDragRatio: 0.30,
            PeakDecelerationEarthGs: 5.5,
            PeakStagnationHeatFluxWattsPerSquareCm: 150.0,
            TpsHeatFluxLimitWattsPerSquareCm: 250.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeAerocaptureTrajectory(telemetry));
    }

    [Fact]
    public void AnalyzeAerocaptureTrajectory_OptimalTrajectory_ReturnsOptimalControlledRegime()
    {
        var telemetry = new HypersonicAerocaptureTelemetry(
            SpacecraftId: "TITAN-EXPLORER-AERO",
            TargetBody: "Titan",
            AtmosphericEntryVelocityKilometersPerSec: 6.5,
            EntryFlightPathAngleDegrees: -12.0,
            NominalCorridorCenterDegrees: -12.0, // Center
            CorridorWidthDegrees: 1.4,
            HypersonicLiftToDragRatio: 0.35,
            PeakDecelerationEarthGs: 5.2,
            PeakStagnationHeatFluxWattsPerSquareCm: 140.0,
            TpsHeatFluxLimitWattsPerSquareCm: 260.0 // 46% margin
        );

        var result = _engine.AnalyzeAerocaptureTrajectory(telemetry);

        Assert.NotNull(result);
        Assert.Equal("TITAN-EXPLORER-AERO", result.SpacecraftId);
        Assert.True(result.TpsThermalMarginPercent >= 20.0);
        Assert.True(result.IsOrbitalInsertionSafe);
        Assert.Equal("OPTIMAL_CONTROLLED_HYPERSONIC_AEROCAPTURE_REGIME", result.AerocaptureRegime);
        Assert.Contains("propellant savings", result.TrajectoryGuidance);
    }

    [Fact]
    public void AnalyzeAerocaptureTrajectory_ShallowSkipOut_ReturnsSkipOutHazard()
    {
        var telemetry = new HypersonicAerocaptureTelemetry(
            SpacecraftId: "MARS-SKIP-02",
            TargetBody: "Mars",
            AtmosphericEntryVelocityKilometersPerSec: 6.3,
            EntryFlightPathAngleDegrees: -10.0, // Shallower than -11.4 + 0.6 = -10.8
            NominalCorridorCenterDegrees: -11.4,
            CorridorWidthDegrees: 1.2,
            HypersonicLiftToDragRatio: 0.30,
            PeakDecelerationEarthGs: 2.1,
            PeakStagnationHeatFluxWattsPerSquareCm: 80.0,
            TpsHeatFluxLimitWattsPerSquareCm: 250.0
        );

        var result = _engine.AnalyzeAerocaptureTrajectory(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SHALLOW_ATMOSPHERIC_SKIP_OUT_ESCAPE_HAZARD", result.AerocaptureRegime);
        Assert.False(result.IsOrbitalInsertionSafe);
        Assert.Contains("skip-out boundary", result.TrajectoryGuidance);
    }

    [Fact]
    public void AnalyzeAerocaptureTrajectory_SteepOverload_ReturnsThermalAndGOverload()
    {
        var telemetry = new HypersonicAerocaptureTelemetry(
            SpacecraftId: "VENUS-OVERLOAD-03",
            TargetBody: "Venus",
            AtmosphericEntryVelocityKilometersPerSec: 11.2,
            EntryFlightPathAngleDegrees: -13.5, // Steeper than -11.4 - 0.6 = -12.0
            NominalCorridorCenterDegrees: -11.4,
            CorridorWidthDegrees: 1.2,
            HypersonicLiftToDragRatio: 0.28,
            PeakDecelerationEarthGs: 18.5, // High g-load
            PeakStagnationHeatFluxWattsPerSquareCm: 450.0,
            TpsHeatFluxLimitWattsPerSquareCm: 350.0
        );

        var result = _engine.AnalyzeAerocaptureTrajectory(telemetry);

        Assert.NotNull(result);
        Assert.Equal("STEEP_TRAJECTORY_THERMAL_AND_STRUCTURAL_G_OVERLOAD", result.AerocaptureRegime);
        Assert.False(result.IsOrbitalInsertionSafe);
    }
}
