namespace SGrowing.Api.Modules.Aerospace.OislLaserPointingJitter;

public class OislLaserPointingJitterEngine
{
    public OislLaserAnalysisResult AnalyzeLinkJitter(OislLaserTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.InterSatelliteDistanceKm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Inter-satellite distance must be positive.");
        }

        if (telemetry.LaserWavelengthNm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Laser wavelength must be positive.");
        }

        if (telemetry.TelescopeApertureDiameterMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Aperture diameter must be positive.");
        }

        if (telemetry.PointingJitterRmsMicroRadians < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Pointing jitter cannot be negative.");
        }

        // Beam divergence theta_div = 1.22 * lambda / D [rad] -> converted to micro-radians
        double lambdaM = telemetry.LaserWavelengthNm * 1e-9;
        double apertureM = telemetry.TelescopeApertureDiameterMm * 1e-3;
        double beamDivergenceUrad = (1.22 * lambdaM / apertureM) * 1e6;

        // Jitter ratio r = sigma_theta / theta_div
        double jitterRatio = telemetry.PointingJitterRmsMicroRadians / Math.Max(beamDivergenceUrad, 1e-6);

        // Gaussian far-field coupling efficiency: eta = exp(-2 * r^2)
        // Attenuation in dB: -10 * log10(eta) = -10 * (-2 * r^2) * log10(e) ~ 8.6858896 * r^2
        double jitterAttenuationDb = 8.6858896 * Math.Pow(jitterRatio, 2);

        bool isDegraded = jitterRatio >= 0.35 || jitterAttenuationDb >= 2.0 || telemetry.QpdTrackingErrorMicroRadians >= 5.0;

        string trackingState;
        string fsmCommand;
        string summary;

        if (jitterRatio >= 0.70 || jitterAttenuationDb >= 6.0 || telemetry.QpdTrackingErrorMicroRadians >= 12.0)
        {
            trackingState = "CRITICAL_BURST_FSM_SATURATION_ALERT";
            fsmCommand = "ENGAGE_COARSE_GIMBAL_RESET_AND_REACQUIRE";
            summary = $"CRITICAL PAT ALERT: Jitter ratio {jitterRatio:P1} caused {jitterAttenuationDb:F1} dB optical loss, saturating piezo Fast Steering Mirror. Initiating coarse gimbal realignment and beacon re-acquisition.";
        }
        else if (isDegraded)
        {
            trackingState = "JITTER_WARNING_DAMPING_REQUIRED";
            fsmCommand = "APPLY_PIEZO_HIGH_BANDWIDTH_DAMPING";
            summary = $"WARNING: Pointing jitter reached {telemetry.PointingJitterRmsMicroRadians:F2} µrad ({jitterRatio:P1} of beam divergence), producing {jitterAttenuationDb:F2} dB loss. High-bandwidth piezo FSM loop activated.";
        }
        else
        {
            trackingState = "LOCKED_NOMINAL_TRACKING";
            fsmCommand = "ZERO_OFFSET_HOLD";
            summary = $"Optical link locked nominally across {telemetry.InterSatelliteDistanceKm:F0} km baseline. Pointing jitter {telemetry.PointingJitterRmsMicroRadians:F2} µrad (divergence: {beamDivergenceUrad:F1} µrad) with negligible {jitterAttenuationDb:F2} dB attenuation.";
        }

        return new OislLaserAnalysisResult(
            telemetry.LinkId,
            Math.Round(beamDivergenceUrad, 2),
            Math.Round(jitterRatio, 3),
            Math.Round(jitterAttenuationDb, 2),
            isDegraded,
            trackingState,
            fsmCommand,
            summary);
    }
}
