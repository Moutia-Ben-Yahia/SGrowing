using System;

namespace SGrowing.Api.Modules.Aerospace.AerogravityAssistTrajectory;

public sealed class AerogravityAssistTrajectoryEngine : IAerogravityAssistTrajectoryEngine
{
    public AerogravityAssistAnalysisResult AnalyzeAerogravityAssist(AerogravityAssistTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        // Planetary constants (Mars or Venus)
        double muPlanet;
        double radiusPlanet;

        if (telemetry.TargetPlanet.Equals("Venus", StringComparison.OrdinalIgnoreCase))
        {
            muPlanet = 324_859.0;
            radiusPlanet = 6052.0;
        }
        else // Mars default
        {
            muPlanet = 42_828.0;
            radiusPlanet = 3390.0;
        }

        var vEntry = Math.Max(1.0, telemetry.AtmosphericEntryVelocityKmPerSec);
        var rPeri = radiusPlanet + Math.Max(10.0, telemetry.PeriapsisAltitudeKilometers);

        // Centrifugal vs local gravitational acceleration ratio: (v^2 / r) / (mu / r^2) = v^2 * r / mu
        var centrifugalToGravity = (Math.Pow(vEntry, 2) * rPeri) / muPlanet;

        // Ballistic turning angle (degrees)
        var eccentricity = 1.0 + (rPeri * Math.Pow(vEntry, 2)) / muPlanet;
        var ballisticTurningDeg = 2.0 * Math.Asin(1.0 / eccentricity) * (180.0 / Math.PI);

        // Aerodynamic waverider turning boost
        var ldRatio = Math.Clamp(telemetry.WaveriderLiftToDragRatio, 1.0, 7.0);
        var aeroTurningDeg = ldRatio * 14.5;
        var totalTurningAngleDeg = ballisticTurningDeg + aeroTurningDeg;

        // Hyperbolic equivalent delta-V (km/s)
        var totalTurningRad = totalTurningAngleDeg * (Math.PI / 180.0);
        var deltaVHyp = 2.0 * vEntry * Math.Sin(totalTurningRad / 2.0);

        string regime;
        string guidance;
        bool isNominal;

        if (telemetry.StagnationHeatFluxMegawattsPerSquareMeter > 11.5)
        {
            regime = "HYPERSONIC_THERMAL_SOAK_EXCURSION";
            guidance = "Stagnation point convective and radiative heating exceeds waverider TPS limit (> 11.5 MW/m2). Pitch up to climb to thinner atmospheric strata.";
            isNominal = false;
        }
        else if (telemetry.DynamicPressureKiloPascals > 80.0)
        {
            regime = "AERODYNAMIC_STRUCTURAL_OVERPRESSURE";
            guidance = "Hypersonic dynamic pressure exceeds aeroelastic limit (> 80 kPa). Modulate bank angle to reduce aerodynamic wing loading.";
            isNominal = false;
        }
        else if (telemetry.InvertedBankAngleDegrees < 155.0 || telemetry.InvertedBankAngleDegrees > 205.0)
        {
            regime = "CENTRIFUGAL_POP_OUT_UNLOCKED";
            guidance = "Inverted bank angle deviates from downward lift trim (155-205 deg). Risk of uncontrolled premature skip-out into hyperbolic escape orbit.";
            isNominal = false;
        }
        else
        {
            regime = "AEROGRAVITY_ASSIST_OPTIMAL_FLYBY";
            guidance = "Waverider inverted downward lift securely counterbalancing centrifugal acceleration. Trajectory turning angle maximized with nominal thermal margins.";
            isNominal = true;
        }

        return new AerogravityAssistAnalysisResult(
            SpacecraftId: telemetry.SpacecraftId,
            TrajectoryTurningAngleDegrees: totalTurningAngleDeg,
            DeltaVHyperbolicEquivalentKmPerSec: deltaVHyp,
            CentrifugalForceToGravityRatio: centrifugalToGravity,
            FlightRegime: regime,
            WaveriderFlightGuidance: guidance,
            IsTrajectoryNominal: isNominal
        );
    }
}
