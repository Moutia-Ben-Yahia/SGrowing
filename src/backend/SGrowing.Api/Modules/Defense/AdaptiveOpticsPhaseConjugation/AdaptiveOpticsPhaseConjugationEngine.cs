using System;

namespace SGrowing.Api.Modules.Defense.AdaptiveOpticsPhaseConjugation;

public interface IAdaptiveOpticsPhaseConjugationEngine
{
    AdaptiveOpticsAnalysisResult AnalyzeAtmosphericWavefront(AtmosphericAdaptiveOpticsTelemetry telemetry);
}

public sealed class AdaptiveOpticsPhaseConjugationEngine : IAdaptiveOpticsPhaseConjugationEngine
{
    public AdaptiveOpticsAnalysisResult AnalyzeAtmosphericWavefront(AtmosphericAdaptiveOpticsTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.TargetRangeKilometers <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Target range must be strictly positive.");
        }

        double lambdaMeters = Math.Max(100.0, telemetry.LaserWavelengthNanometers) * 1e-9;
        double waveNumberK = (2.0 * Math.PI) / lambdaMeters;
        double pathLengthMeters = telemetry.TargetRangeKilometers * 1000.0;
        double cn2 = Math.Max(1e-18, telemetry.RefractiveIndexStructureConstantCn2);

        // Spherical wave Fried coherence parameter: r0 = (0.423 * k^2 * Cn2 * (3/8) * L)^(-3/5)
        double r0Denominator = 0.423 * Math.Pow(waveNumberK, 2.0) * cn2 * 0.375 * pathLengthMeters;
        double friedCoherenceMeters = Math.Pow(r0Denominator, -0.6);
        friedCoherenceMeters = Math.Clamp(friedCoherenceMeters, 0.005, 1.0);

        // Greenwood frequency: fg = 0.43 * (v_wind / r0)
        double greenwoodFrequencyHz = 0.43 * (Math.Max(0.5, telemetry.CrosswindVelocityMPerSec) / friedCoherenceMeters);

        // Rytov variance for spherical wave: sigma_R^2 = 0.563 * Cn2 * k^(7/6) * L^(11/6)
        double rytovCalculated = 0.563 * cn2 * Math.Pow(waveNumberK, 7.0 / 6.0) * Math.Pow(pathLengthMeters, 11.0 / 6.0);
        double effectiveRytov = Math.Max(rytovCalculated, telemetry.MeasuredRytovVariance);

        // Wavefront error: fitting + bandwidth lag
        double actuatorSpacing = Math.Max(0.005, telemetry.DeformableMirrorActuatorSpacingMeters);
        double fittingVariance = 0.28 * Math.Pow(actuatorSpacing / friedCoherenceMeters, 5.0 / 3.0);

        double bandwidth = Math.Max(10.0, telemetry.ControlLoopBandwidthHz);
        double bandwidthVariance = Math.Pow(greenwoodFrequencyHz / bandwidth, 5.0 / 3.0);

        double totalWavefrontVariance = fittingVariance + bandwidthVariance;
        double strehlRatio = Math.Exp(-totalWavefrontVariance);
        strehlRatio = Math.Clamp(strehlRatio, 0.01, 1.0);

        string regime;
        string command;
        bool isAuthorized;

        if (effectiveRytov > 0.80 || strehlRatio < 0.35)
        {
            regime = "SEVERE_TURBULENCE_SCINTILLATION";
            command = "ENGAGE_MULTI_BEAM_INCOHERENT_APERTURE_AVERAGING";
            isAuthorized = false;
        }
        else if ((greenwoodFrequencyHz / bandwidth) > 0.25 || strehlRatio < 0.70)
        {
            regime = "AO_BANDWIDTH_LAG_WARNING";
            command = "INCREASE_DEFORMABLE_MIRROR_LOOP_GAIN_TO_MAXIMUM";
            isAuthorized = false;
        }
        else
        {
            regime = "DIFFRACTION_LIMITED_FOCUS";
            command = "AUTHORIZE_FULL_POWER_HIGH_ENERGY_BEAM_FIRING";
            isAuthorized = true;
        }

        return new AdaptiveOpticsAnalysisResult(
            telemetry.LaserSystemId,
            Math.Round(friedCoherenceMeters, 4),
            Math.Round(greenwoodFrequencyHz, 1),
            Math.Round(effectiveRytov, 3),
            Math.Round(totalWavefrontVariance, 4),
            Math.Round(strehlRatio, 3),
            regime,
            command,
            isAuthorized,
            DateTimeOffset.UtcNow);
    }
}
