using System;
using SGrowing.Api.Modules.Aerospace.LunarCraterTetherRappelling;
using Xunit;

namespace SGrowing.Tests.Aerospace;

public sealed class LunarCraterTetherRappellingTests
{
    private readonly LunarCraterTetherRappellingEngine _engine = new();

    [Fact]
    public void AnalyzeRappellingDynamics_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeRappellingDynamics(null!));
    }

    [Fact]
    public void AnalyzeRappellingDynamics_NegativeMass_ThrowsArgumentException()
    {
        var telemetry = new LunarCraterTetherTelemetry(
            RoverId: "MOON-RAPTOR-1",
            CraterWallSlopeAngleDegrees: 35.0,
            RoverMassKg: -50.0, // Negative!
            RegolithFrictionCoefficient: 0.40,
            AnchorPullOutCapacityNewtons: 2500.0,
            MeasuredTetherTensionNewtons: 200.0,
            RoverTrackWidthMeters: 1.2,
            CenterOfGravityHeightMeters: 0.6
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeRappellingDynamics(telemetry));
    }

    [Fact]
    public void AnalyzeRappellingDynamics_OptimalRappel_ReturnsOptimalRappelRegime()
    {
        var telemetry = new LunarCraterTetherTelemetry(
            RoverId: "SHACKLETON-PSR-ROVER",
            CraterWallSlopeAngleDegrees: 40.0,
            RoverMassKg: 260.0,
            RegolithFrictionCoefficient: 0.45,
            AnchorPullOutCapacityNewtons: 3500.0,
            MeasuredTetherTensionNewtons: 250.0, // Anchor SF = 3500 / 250 = 14.0 >= 3.0
            RoverTrackWidthMeters: 1.5,
            CenterOfGravityHeightMeters: 0.6
        );

        var result = _engine.AnalyzeRappellingDynamics(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SHACKLETON-PSR-ROVER", result.RoverId);
        Assert.True(result.AnchorSafetyFactor >= 3.0);
        Assert.True(result.IsRappellingDescentSafe);
        Assert.Equal("OPTIMAL_TETHERED_RAPPEL_DESCENT_REGIME", result.RappellingRegime);
        Assert.Contains("Stable descent into PSR crater", result.WinchControlGuidance);
    }

    [Fact]
    public void AnalyzeRappellingDynamics_LowAnchorSafetyFactor_ReturnsAnchorPullOutHazard()
    {
        var telemetry = new LunarCraterTetherTelemetry(
            RoverId: "CRATER-CLIMBER-02",
            CraterWallSlopeAngleDegrees: 50.0,
            RoverMassKg: 300.0,
            RegolithFrictionCoefficient: 0.35,
            AnchorPullOutCapacityNewtons: 400.0, // Low capacity!
            MeasuredTetherTensionNewtons: 350.0, // 400 < 350 * 1.5 = 525 N
            RoverTrackWidthMeters: 1.2,
            CenterOfGravityHeightMeters: 0.7
        );

        var result = _engine.AnalyzeRappellingDynamics(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CRITICAL_ANCHOR_PULL_OUT_DISLODGEMENT_HAZARD", result.RappellingRegime);
        Assert.False(result.IsRappellingDescentSafe);
        Assert.Contains("auxiliary rotary hammer anchor", result.WinchControlGuidance);
    }

    [Fact]
    public void AnalyzeRappellingDynamics_SlackTetherBeyondRollover_ReturnsSlackTetherHazard()
    {
        // w = 1.0, h_cg = 0.8 -> theta_roll = arctan(1.0 / 1.6) = 32.0 deg
        var telemetry = new LunarCraterTetherTelemetry(
            RoverId: "TIPPY-ROVER-03",
            CraterWallSlopeAngleDegrees: 45.0, // Exceeds 32 deg!
            RoverMassKg: 200.0,
            RegolithFrictionCoefficient: 0.40,
            AnchorPullOutCapacityNewtons: 3000.0,
            MeasuredTetherTensionNewtons: 15.0, // Slack tether < 50 N!
            RoverTrackWidthMeters: 1.0,
            CenterOfGravityHeightMeters: 0.8
        );

        var result = _engine.AnalyzeRappellingDynamics(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SLOPE_EXCEEDS_ROLLOVER_THRESHOLD_SLACK_TETHER_HAZARD", result.RappellingRegime);
        Assert.False(result.IsRappellingDescentSafe);
        Assert.Contains("Reel in winch immediately", result.WinchControlGuidance);
    }
}
