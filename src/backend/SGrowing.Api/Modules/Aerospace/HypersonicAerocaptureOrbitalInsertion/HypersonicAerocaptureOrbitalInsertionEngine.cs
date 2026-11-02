using System;

namespace SGrowing.Api.Modules.Aerospace.HypersonicAerocaptureOrbitalInsertion;

public sealed class HypersonicAerocaptureOrbitalInsertionEngine : IHypersonicAerocaptureOrbitalInsertionEngine
{
    public HypersonicAerocaptureAnalysisResult AnalyzeAerocaptureTrajectory(HypersonicAerocaptureTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.SpacecraftId))
        {
            throw new ArgumentException("Spacecraft ID must be provided.", nameof(telemetry));
        }

        if (telemetry.AtmosphericEntryVelocityKilometersPerSec <= 0 || telemetry.TpsHeatFluxLimitWattsPerSquareCm <= 0 ||
            telemetry.CorridorWidthDegrees <= 0)
        {
            throw new ArgumentException("Velocities, TPS limits, and corridor width must be positive.", nameof(telemetry));
        }

        var corridorHalfWidth = telemetry.CorridorWidthDegrees / 2.0;
        var deviation = Math.Abs(telemetry.EntryFlightPathAngleDegrees - telemetry.NominalCorridorCenterDegrees);

        // Approximate delta-V saved vs all-propulsive orbital insertion
        var deltaVSaved = Math.Max(0.0, (telemetry.AtmosphericEntryVelocityKilometersPerSec * 1000.0) - 3500.0);

        var thermalMarginPercent = Math.Max(0.0,
            ((telemetry.TpsHeatFluxLimitWattsPerSquareCm - telemetry.PeakStagnationHeatFluxWattsPerSquareCm) /
             telemetry.TpsHeatFluxLimitWattsPerSquareCm) * 100.0);

        string regime;
        string guidance;
        bool isSafe = thermalMarginPercent >= 15.0 &&
                      telemetry.PeakDecelerationEarthGs <= 9.0 &&
                      deviation <= corridorHalfWidth;

        if (telemetry.EntryFlightPathAngleDegrees > telemetry.NominalCorridorCenterDegrees + corridorHalfWidth)
        {
            regime = "SHALLOW_ATMOSPHERIC_SKIP_OUT_ESCAPE_HAZARD";
            guidance = $"Flight path angle ({telemetry.EntryFlightPathAngleDegrees:F2} deg) is shallower than the skip-out boundary ({telemetry.NominalCorridorCenterDegrees + corridorHalfWidth:F2} deg). Vehicle will fail orbital capture; bank lift vector down (roll 180 deg).";
            isSafe = false;
        }
        else if (telemetry.EntryFlightPathAngleDegrees < telemetry.NominalCorridorCenterDegrees - corridorHalfWidth)
        {
            regime = "STEEP_TRAJECTORY_THERMAL_AND_STRUCTURAL_G_OVERLOAD";
            guidance = $"Flight path angle ({telemetry.EntryFlightPathAngleDegrees:F2} deg) is steeper than corridor limit ({telemetry.NominalCorridorCenterDegrees - corridorHalfWidth:F2} deg). Peak g-load ({telemetry.PeakDecelerationEarthGs:F1} g) risks structural breakup; bank lift vector up (roll 0 deg).";
            isSafe = false;
        }
        else if (telemetry.PeakStagnationHeatFluxWattsPerSquareCm > telemetry.TpsHeatFluxLimitWattsPerSquareCm)
        {
            regime = "TPS_THERMAL_ABLATION_LIMIT_EXCEEDED";
            guidance = $"Stagnation heat flux ({telemetry.PeakStagnationHeatFluxWattsPerSquareCm:F1} W/cm^2) exceeds thermal tile limit ({telemetry.TpsHeatFluxLimitWattsPerSquareCm:F1} W/cm^2). Severe risk of TPS burn-through.";
            isSafe = false;
        }
        else if (thermalMarginPercent >= 20.0 && telemetry.PeakDecelerationEarthGs <= 8.0)
        {
            regime = "OPTIMAL_CONTROLLED_HYPERSONIC_AEROCAPTURE_REGIME";
            guidance = $"Trajectory is centrally positioned within the aerocapture corridor (deviation {deviation:F2} deg). Clean orbital insertion achieved with {deltaVSaved:F0} m/s delta-V propellant savings.";
        }
        else
        {
            regime = "MARGINAL_CORRIDOR_TRACKING_HIGH_DYNAMIC_PRESSURE";
            guidance = $"Inside corridor but close to aerodynamic limits. Maintain active RCS / flap roll-rate steering to stabilize exit apoapsis.";
        }

        return new HypersonicAerocaptureAnalysisResult(
            telemetry.SpacecraftId,
            Math.Round(deviation, 3),
            Math.Round(deltaVSaved, 1),
            Math.Round(thermalMarginPercent, 1),
            regime,
            guidance,
            isSafe
        );
    }
}
