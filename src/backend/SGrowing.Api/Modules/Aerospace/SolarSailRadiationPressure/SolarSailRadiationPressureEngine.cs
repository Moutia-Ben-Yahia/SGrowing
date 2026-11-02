using System;

namespace SGrowing.Api.Modules.Aerospace.SolarSailRadiationPressure;

public sealed class SolarSailRadiationPressureEngine : ISolarSailRadiationPressureEngine
{
    private const double P0SolarPressureAt1AuMicroPa = 4.563; // 4.563 uPa at 1 AU (S0 / c)
    private const double GSunAt1AuMmPerSec2 = 5.930; // Solar gravity acceleration at 1 AU

    public SolarSailAnalysisResult AnalyzeSolarSail(SolarSailTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var rAu = Math.Max(0.1, telemetry.HeliocentricDistanceAstronomicalUnits);
        var areaM2 = Math.Max(1.0, telemetry.TotalSailAreaSquareMeters);
        var massKg = Math.Max(1.0, telemetry.TotalSpacecraftMassKg);
        var eta = Math.Clamp(telemetry.SailSpecularReflectivityCoefficient, 0.1, 1.0);

        var pitchRad = Math.Clamp(telemetry.SailConePitchAngleDegrees, 0.0, 90.0) * (Math.PI / 180.0);

        // Local solar radiation pressure at distance r: P(r) = P0 / r^2
        var localPressureMicroPa = P0SolarPressureAt1AuMicroPa / Math.Pow(rAu, 2.0);

        // Photonic thrust force: F = P(r) * A * cos^2(alpha) * (1 + eta)
        var thrustForceNewtons = (localPressureMicroPa * 1e-6) * areaM2 * Math.Pow(Math.Cos(pitchRad), 2.0) * (1.0 + eta);
        var thrustMilliNewtons = thrustForceNewtons * 1000.0;

        // Characteristic acceleration a = F / m (in mm/s^2)
        var accelerationMmPerSec2 = (thrustForceNewtons / massKg) * 1000.0;

        // Solar lightness factor beta = a_sail(1 AU, alpha=0) / g_sun(1 AU)
        var a1AuMmPerSec2 = ((P0SolarPressureAt1AuMicroPa * 1e-6 * areaM2 * (1.0 + eta)) / massKg) * 1000.0;
        var lightnessFactorBeta = a1AuMmPerSec2 / GSunAt1AuMmPerSec2;

        string regime;
        string guidance;
        bool isNominal;

        if (telemetry.SailMembraneTemperatureKelvin > 520.0)
        {
            regime = "MEMBRANE_OVERHEATING_THERMAL_DEGRADATION_HAZARD";
            guidance = "Polyimide sail membrane temperature exceeds 520 K thermal degradation limit. Increase pitch angle to reduce projected solar thermal flux.";
            isNominal = false;
        }
        else if (telemetry.SailConePitchAngleDegrees > 60.0)
        {
            regime = "HIGH_PITCH_THRUST_COLLAPSE_WARNING";
            guidance = "Sail cone pitch angle exceeds 60 deg causing severe photonic thrust collapse and membrane fluttering. Adjust tip-vanes to trim attitude.";
            isNominal = false;
        }
        else if (telemetry.SailSpecularReflectivityCoefficient < 0.75)
        {
            regime = "REFLECTIVE_COATING_DEGRADATION_DEFICIT";
            guidance = "Aluminum reflective coating degraded below 75% specular threshold. Photonic momentum transfer reduced; recalibrate heliocentric transfer orbit.";
            isNominal = false;
        }
        else
        {
            regime = "OPTIMAL_PHOTONIC_RADIATION_PRESSURE_PROPULSION";
            guidance = "Continuous propellantless photon momentum transfer active. Lightness factor and attitude control within nominal interplanetary parameters.";
            isNominal = true;
        }

        return new SolarSailAnalysisResult(
            SpacecraftId: telemetry.SpacecraftId,
            SolarRadiationPressureMicroPascals: localPressureMicroPa,
            GeneratedPhotonThrustForceMilliNewtons: thrustMilliNewtons,
            CharacteristicAccelerationMmPerSec2: accelerationMmPerSec2,
            SolarLightnessFactorBeta: lightnessFactorBeta,
            SailNavigationRegime: regime,
            AttitudeControlGuidance: guidance,
            IsTrajectoryNominal: isNominal
        );
    }
}
