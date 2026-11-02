namespace SGrowing.Api.Modules.Aerospace.AtmosphericEntryBlackoutGuidance;

public interface IAtmosphericEntryEngine
{
    AtmosphericEntryEvaluation AnalyzeEntry(AtmosphericEntryTelemetry telemetry);
}

public class AtmosphericEntryEngine : IAtmosphericEntryEngine
{
    public AtmosphericEntryEvaluation AnalyzeEntry(AtmosphericEntryTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Plasma cutoff frequency: f_p = (1 / 2pi) * sqrt(n_e * e^2 / (eps0 * m_e))
        // Numerically: f_p (Hz) ~ 8980 * sqrt(n_e in cm^-3)
        double ne = Math.Max(1e10, telemetry.PeakPlasmaSheathElectronDensityPerCm3);
        double fpGhz = (8980.0 * Math.Sqrt(ne)) / 1e9; // in GHz

        // Corridor evaluation: gamma in [-6.8, -5.2]
        double gamma = telemetry.FlightPathAngleDegrees;
        bool isSkipOutAverted = gamma <= -5.10;
        bool isDecelThermalSafe = telemetry.PeakDecelerationGs <= 4.5 && telemetry.StagnationHeatFluxWattsPerCm2 <= 250.0;

        // Corridor margin (0 to 100%)
        double corridorMargin = 0.0;
        if (gamma >= -6.8 && gamma <= -5.2)
        {
            double center = -6.0;
            corridorMargin = (1.0 - Math.Abs(gamma - center) / 0.8) * 100.0;
        }
        corridorMargin = Math.Clamp(corridorMargin, 0.0, 100.0);

        // Telemetry preservation: Ka-band (32 GHz) or magnetic window can penetrate plasma cutoff
        bool isContinuousTelemetry = (telemetry.IsKaBandRelayTransceiverActive && 32.0 > fpGhz) ||
                                     telemetry.MagneticWindowFieldTesla >= 0.40 ||
                                     telemetry.SbandBlackoutDurationSeconds <= 60.0;

        if (gamma > -5.10)
        {
            regimes.Add("ATMOSPHERIC_SKIP_OUT_TRAJECTORY_HAZARD");
            mitigations.Add("Modulate vehicle aerodynamic lift-vector bank angle downwards (lift-down) to capture into atmospheric corridor.");
        }

        if (!isDecelThermalSafe)
        {
            regimes.Add("PEAK_DECELERATION_AEROTHERMAL_BREACH");
            mitigations.Add("Rotate roll bank angle to maximize cross-range lifting glide and decelerate in less dense rarefied upper atmosphere.");
        }

        if (!isContinuousTelemetry)
        {
            regimes.Add("TELEMETRY_BLACKOUT_WINDOW_EXCEEDANCE");
            mitigations.Add("Energize superconducting magnetic coil (B >= 0.45 T) to generate localized ExB electron cyclotron plasma window.");
        }

        bool isGoForLanding = isSkipOutAverted && isDecelThermalSafe && isContinuousTelemetry && corridorMargin >= 25.0;

        return new AtmosphericEntryEvaluation
        {
            EntryVehicleMissionId = telemetry.EntryVehicleMissionId,
            PlasmaCutoffFrequencyGigaHertz = Math.Round(fpGhz, 2),
            ReEntryCorridorSafetyMarginPercent = Math.Round(corridorMargin, 1),
            IsSkipOutOvershootAverted = isSkipOutAverted,
            IsPeakDecelerationThermalSafe = isDecelThermalSafe,
            IsContinuousTelemetryPreserved = isContinuousTelemetry,
            IsEntryGuidanceGoForLanding = isGoForLanding,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
