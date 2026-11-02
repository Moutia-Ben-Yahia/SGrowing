using System;

namespace SGrowing.Api.Modules.Aerospace.LunarCraterTetherRappelling;

public sealed class LunarCraterTetherRappellingEngine : ILunarCraterTetherRappellingEngine
{
    private const double LunarGravity = 1.62; // m/s^2

    public LunarCraterTetherAnalysisResult AnalyzeRappellingDynamics(LunarCraterTetherTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.RoverId))
        {
            throw new ArgumentException("Rover ID must be provided.", nameof(telemetry));
        }

        if (telemetry.RoverMassKg <= 0 || telemetry.AnchorPullOutCapacityNewtons <= 0 ||
            telemetry.RoverTrackWidthMeters <= 0 || telemetry.CenterOfGravityHeightMeters <= 0)
        {
            throw new ArgumentException("Rover dimensions, mass, and anchor capacity must be strictly positive.", nameof(telemetry));
        }

        var thetaRad = (telemetry.CraterWallSlopeAngleDegrees * Math.PI) / 180.0;
        var fDownslope = telemetry.RoverMassKg * LunarGravity * Math.Sin(thetaRad);
        var fFriction = telemetry.RegolithFrictionCoefficient * telemetry.RoverMassKg * LunarGravity * Math.Cos(thetaRad);

        var theoreticalTension = Math.Max(20.0, fDownslope - fFriction);
        var measuredTension = Math.Max(10.0, telemetry.MeasuredTetherTensionNewtons);
        var anchorSafetyFactor = telemetry.AnchorPullOutCapacityNewtons / measuredTension;

        // Static rollover threshold without tether: theta_roll = arctan(w / (2 * h_cg))
        var rolloverThresholdDegrees = Math.Atan(telemetry.RoverTrackWidthMeters / (2.0 * telemetry.CenterOfGravityHeightMeters)) * (180.0 / Math.PI);

        string regime;
        string guidance;
        bool isSafe = anchorSafetyFactor >= 2.0 &&
                      telemetry.MeasuredTetherTensionNewtons >= 50.0 &&
                      telemetry.CraterWallSlopeAngleDegrees <= 60.0;

        if (telemetry.AnchorPullOutCapacityNewtons < telemetry.MeasuredTetherTensionNewtons * 1.5)
        {
            regime = "CRITICAL_ANCHOR_PULL_OUT_DISLODGEMENT_HAZARD";
            guidance = $"Anchor safety factor ({anchorSafetyFactor:F2} < 1.5) is dangerously low. Risk of regolith anchor dislodgement; halt winch unspooling and drive auxiliary rotary hammer anchor.";
            isSafe = false;
        }
        else if (telemetry.CraterWallSlopeAngleDegrees >= rolloverThresholdDegrees && telemetry.MeasuredTetherTensionNewtons < 50.0)
        {
            regime = "SLOPE_EXCEEDS_ROLLOVER_THRESHOLD_SLACK_TETHER_HAZARD";
            guidance = $"Slope angle ({telemetry.CraterWallSlopeAngleDegrees:F1} deg) exceeds unassisted rollover limit ({rolloverThresholdDegrees:F1} deg) while tether is slack ({telemetry.MeasuredTetherTensionNewtons:F1} N). Reel in winch immediately to avoid tumble.";
            isSafe = false;
        }
        else if (telemetry.CraterWallSlopeAngleDegrees > 65.0)
        {
            regime = "OVERHANG_VERTICAL_CLIFF_ABSOLUTE_FALL_RISK";
            guidance = $"Slope angle ({telemetry.CraterWallSlopeAngleDegrees:F1} deg) represents a shear overhang. Wheel traction completely lost; transition to pure suspended ascender mode.";
            isSafe = false;
        }
        else if (anchorSafetyFactor >= 3.0 && telemetry.CraterWallSlopeAngleDegrees <= 55.0)
        {
            regime = "OPTIMAL_TETHERED_RAPPEL_DESCENT_REGIME";
            guidance = $"Stable descent into PSR crater. Line tension ({telemetry.MeasuredTetherTensionNewtons:F0} N) and anchor SF ({anchorSafetyFactor:F1}x) within safety envelopes.";
        }
        else
        {
            regime = "CONTROLLED_TENSION_RAPPELING_REGIME";
            guidance = $"Controlled rappelling active. Theoretical tension demand is {theoreticalTension:F0} N. Maintain continuous capstan motor current telemetry.";
        }

        return new LunarCraterTetherAnalysisResult(
            telemetry.RoverId,
            Math.Round(theoreticalTension, 1),
            Math.Round(anchorSafetyFactor, 2),
            Math.Round(rolloverThresholdDegrees, 1),
            regime,
            guidance,
            isSafe
        );
    }
}
