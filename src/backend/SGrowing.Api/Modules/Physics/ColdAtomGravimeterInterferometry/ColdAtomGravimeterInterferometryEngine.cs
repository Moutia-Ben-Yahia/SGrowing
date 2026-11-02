using System;

namespace SGrowing.Api.Modules.Physics.ColdAtomGravimeterInterferometry;

public sealed class ColdAtomGravimeterInterferometryEngine : IColdAtomGravimeterInterferometryEngine
{
    public ColdAtomGravimeterAnalysisResult AnalyzeGravimeter(ColdAtomGravimeterTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var lambdaMeters = Math.Max(100.0, telemetry.RamanLaserWavelengthNanometers) * 1e-9;
        var kEff = 4.0 * Math.PI / lambdaMeters;

        var g = Math.Max(1.0, telemetry.EffectiveGravitationalAccelerationMps2);
        var t = Math.Max(0.005, telemetry.InterrogationTimeTSeconds);

        // Theoretical gravitational phase shift: DeltaPhi = k_eff * g * T^2
        var phaseShiftRadians = kEff * g * Math.Pow(t, 2);

        // Doppler chirp rate matching freefall: alpha = (k_eff * g) / (2 * pi) [Hz/s]
        var idealChirpMhzPerSec = (kEff * g) / (2.0 * Math.PI * 1e6);
        var chirpErrorHz = Math.Abs(telemetry.RamanLaserChirpRateMhzPerSec - idealChirpMhzPerSec) * 1e6;

        // Metrology resolution: shot noise + seismic vibration
        var nAtoms = Math.Max(100.0, telemetry.Rb87AtomCloudCount);
        var contrast = Math.Clamp(telemetry.InterferometerInterferenceFringeContrastPercent / 100.0, 0.01, 1.0);
        var phaseNoise = 1.0 / (contrast * Math.Sqrt(nAtoms));
        var shotNoiseGravity = phaseNoise / (kEff * Math.Pow(t, 2));

        var seismicRejectionFactor = 1e-3; // active feedforward correlation rejection
        var seismicAccMps2 = telemetry.ResidualSeismicVibrationRmsMicroG * 1e-6 * g * seismicRejectionFactor;
        var totalGravityUncertaintyMps2 = Math.Sqrt(Math.Pow(shotNoiseGravity, 2) + Math.Pow(seismicAccMps2, 2));
        var resolutionMicroGal = totalGravityUncertaintyMps2 * 1e8; // 1 uGal = 1e-8 m/s2

        string regime;
        string guidance;
        bool isValid;

        if (telemetry.InterferometerInterferenceFringeContrastPercent < 15.0)
        {
            regime = "FRINGE_CONTRAST_WASHOUT_DECOHERENCE";
            guidance = "Interference fringe contrast collapsed below 15%. Re-tune Raman laser beam balance and optical molasses sub-Doppler cooling.";
            isValid = false;
        }
        else if (telemetry.ResidualSeismicVibrationRmsMicroG > 18.0)
        {
            regime = "SEISMIC_VIBRATION_DECORRELATION_WARNING";
            guidance = "Residual seismic floor exceeds 18 uG. Engage active pneumatically isolated optical breadboard feedforward loop.";
            isValid = false;
        }
        else if (chirpErrorHz > 4000.0)
        {
            regime = "DOPPLER_CHIRP_DESYNCHRONIZATION";
            guidance = "Raman chirp rate deviates from gravitational Doppler slope. Re-lock DDS frequency synthesizer to rubidium freefall rate.";
            isValid = false;
        }
        else
        {
            regime = "QUANTUM_SUB_MICROGAL_GRAVIMETRIC_PRECISION";
            guidance = "Atom interferometer operating at sub-microGal gravimetric sensitivity. Doppler chirp and active seismic feedforward locked.";
            isValid = true;
        }

        return new ColdAtomGravimeterAnalysisResult(
            GravimeterId: telemetry.GravimeterId,
            CalculatedGravitationalPhaseShiftRadians: phaseShiftRadians,
            GravityMeasurementResolutionMicroGal: resolutionMicroGal,
            ChirpRateDopplerErrorHertz: chirpErrorHz,
            GravimetryRegime: regime,
            FeedforwardVibrationControlGuidance: guidance,
            IsMetrologyValid: isValid
        );
    }
}
