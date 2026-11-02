using System;

namespace SGrowing.Api.Modules.Aerospace.AsteroidKineticImpactorDeflection;

public sealed class AsteroidKineticImpactorDeflectionEngine : IAsteroidKineticImpactorDeflectionEngine
{
    private const double EarthRadiusKm = 6378.137;
    private const double SecondsPerYear = 365.25 * 86400.0;

    public AsteroidKineticImpactorAnalysisResult AnalyzeKineticImpactor(AsteroidKineticImpactorTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (string.IsNullOrWhiteSpace(telemetry.AsteroidTargetId))
        {
            throw new ArgumentException("Asteroid target ID must be provided.", nameof(telemetry));
        }

        if (telemetry.AsteroidMassKilograms <= 0 || telemetry.SpacecraftMassKilograms <= 0 ||
            telemetry.ImpactVelocityKilometersPerSec <= 0 || telemetry.WarningTimeLeadYears <= 0)
        {
            throw new ArgumentException("Masses, velocities, and lead time must be strictly positive.", nameof(telemetry));
        }

        var thetaRad = (telemetry.ImpactIncidenceAngleDegrees * Math.PI) / 180.0;
        var vImpMps = telemetry.ImpactVelocityKilometersPerSec * 1000.0;

        // Asteroid velocity change: Delta v = beta * (m_sc / M_ast) * v_imp * cos(theta)
        var beta = telemetry.EstimatedMomentumBetaFactor;
        var deltaVMps = beta * (telemetry.SpacecraftMassKilograms / telemetry.AsteroidMassKilograms) * vImpMps * Math.Cos(thetaRad);
        var deltaVMmPerSec = deltaVMps * 1000.0;

        // B-plane miss deflection: Delta b = Delta v * (T_lead * SecondsPerYear) / 1000.0
        var totalLeadSeconds = telemetry.WarningTimeLeadYears * SecondsPerYear;
        var deflectionKm = Math.Max(0.0, (deltaVMps * totalLeadSeconds) / 1000.0);
        var earthRadii = deflectionKm / EarthRadiusKm;

        string regime;
        string guidance;
        bool isSuccessful = deflectionKm >= 20000.0 &&
                            beta >= 1.2 &&
                            deltaVMmPerSec > 0.5;

        if (beta < 1.0)
        {
            regime = "UNPHYSICAL_OR_MASS_ABSORPTIVE_CRATERING_ANOMALY";
            guidance = $"Beta momentum factor ({beta:F2} < 1.0) violates conservation of momentum for kinetic impact with ejecta. Verify target porous bulk density and impact simulation parameters.";
            isSuccessful = false;
        }
        else if (deflectionKm < EarthRadiusKm)
        {
            regime = "INSUFFICIENT_DEFLECTION_IMPACT_CORRIDOR_CROSSING_HAZARD";
            guidance = $"Deflection distance ({deflectionKm:F0} km < 1 Earth radius) fails to clear Earth capture cross-section. Trajectory remains on collision course; multiple impactors or nuclear standoff blast required.";
            isSuccessful = false;
        }
        else if (telemetry.WarningTimeLeadYears < 3.0)
        {
            regime = "CRITICAL_SHORT_WARNING_TIME_DEFICIT";
            guidance = $"Short warning time ({telemetry.WarningTimeLeadYears:F1} years) restricts secular orbital displacement. Deflection margin ({deflectionKm:F0} km) is dangerously narrow.";
            isSuccessful = false;
        }
        else if (deflectionKm >= 25000.0 && deltaVMmPerSec >= 1.0)
        {
            regime = "SUCCESSFUL_PLANETARY_DEFENSE_EARTH_CLEARANCE_REGIME";
            guidance = $"Kinetic impactor imparts {deltaVMmPerSec:F2} mm/s delta-V (ejecta beta = {beta:F2}), shifting B-plane close approach by {deflectionKm:F0} km ({earthRadii:F1} Earth radii). Asteroid completely defused.";
        }
        else
        {
            regime = "MARGINAL_GRAVITATIONAL_KEYHOLE_SHIFT_REGIME";
            guidance = $"Deflection achieved ({deflectionKm:F0} km, {earthRadii:F1} Earth radii). Verify that deflected trajectory does not enter secondary resonant gravitational return keyholes.";
        }

        return new AsteroidKineticImpactorAnalysisResult(
            telemetry.AsteroidTargetId,
            Math.Round(deltaVMmPerSec, 3),
            Math.Round(deflectionKm, 1),
            Math.Round(earthRadii, 2),
            regime,
            guidance,
            isSuccessful
        );
    }
}
