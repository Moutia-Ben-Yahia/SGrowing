using System;

namespace SGrowing.Api.Modules.Environmental.QclAtmosphericPlumeInversion;

public sealed class QclAtmosphericPlumeInversionEngine : IQclAtmosphericPlumeInversionEngine
{
    public QclPlumeAnalysisResult AnalyzeGreenhousePlume(QclPlumeTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var pathLength = Math.Max(10.0, telemetry.OpticalPathLengthMeters);
        var pathAvgPpm = Math.Round(telemetry.MeasuredPathIntegratedPpmMeters / pathLength, 3);

        // Baseline ambient background ~ 1.90 ppm for methane
        const double ambientBackgroundPpm = 1.90;
        var backgroundIntegratedPpmMeters = ambientBackgroundPpm * pathLength;
        var excessPlumePpmMeters = Math.Max(0.0, telemetry.MeasuredPathIntegratedPpmMeters - backgroundIntegratedPpmMeters);

        // Gaussian crosswind flux inversion Q = Delta(PPM-m) * factor * u * cos(theta) (kg/hr)
        var u = Math.Max(0.2, telemetry.CrosswindSpeedMetersPerSecond);
        var windAngleRad = Math.Abs(telemetry.WindDirectionRelativeDegrees) * (Math.PI / 180.0);
        var cosWind = Math.Max(0.1, Math.Cos(windAngleRad));
        var inferredFluxKgPerHour = Math.Round(excessPlumePpmMeters * 0.038 * u * cosWind, 2);

        // Scintillation optical signal attenuation from Rytov variance
        var rytov = Math.Max(0.0, telemetry.BeamScintillationRytovVariance);
        var scintillationAttenuationPercent = Math.Round(Math.Clamp(rytov * 35.0, 0.0, 90.0), 1);

        string regime;
        string action;
        bool isValid;

        if (rytov > 0.40 || !telemetry.RetroreflectorAlignmentTrackingLocked)
        {
            regime = "OPTICAL_SCINTILLATION_BEAM_WANDER_HAZARD";
            action = "Atmospheric refractive index turbulence causing beam wander. Activate piezo fast-steering mirror (FSM) tracking.";
            isValid = false;
        }
        else if (Math.Abs(telemetry.WindDirectionRelativeDegrees) > 75.0)
        {
            regime = "TRANSVERSE_WIND_DISPERSION_DECOUPLED";
            action = "Wind direction nearly parallel to optical beam (angle > 75 deg). Reorient sensor link or await wind shift for valid cross-plume transect.";
            isValid = false;
        }
        else if (inferredFluxKgPerHour > 25.0 || pathAvgPpm > 4.5)
        {
            regime = "FUGITIVE_EMISSION_SUPER_EMITTER_ALERT";
            action = "Fugitive super-emitter detected (>25 kg/hr). Dispatch autonomous drone inspection team to pinpoint upstream flaring or valve leak.";
            isValid = true;
        }
        else
        {
            regime = "ATMOSPHERIC_BACKGROUND_NOMINAL";
            action = "Mid-IR absorption matches global clean baseline. Zero industrial fugitive emission anomalies detected.";
            isValid = true;
        }

        return new QclPlumeAnalysisResult(
            SensorPathId: telemetry.SensorPathId,
            PathAveragedMixingRatioPpm: pathAvgPpm,
            InferredPlumeEmissionFluxKgPerHour: inferredFluxKgPerHour,
            AtmosphericTurbulenceAttenuationPercent: scintillationAttenuationPercent,
            EmissionDetectionRegime: regime,
            PlumeLocalizationAction: action,
            IsMeasurementValid: isValid
        );
    }
}
