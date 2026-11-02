using System;

namespace SGrowing.Api.Modules.Physics.StedSuperResolutionMicroscopy;

public sealed class StedSuperResolutionMicroscopyEngine : IStedSuperResolutionMicroscopyEngine
{
    public StedMicroscopyAnalysisResult AnalyzeStedMicroscopy(StedMicroscopyTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var lambdaExc = Math.Max(300.0, telemetry.ExcitationWavelengthNanometers);
        var na = Math.Clamp(telemetry.ObjectiveNumericalAperture, 0.4, 1.70);

        // Abbe diffraction limit: d = lambda / (2 * NA)
        var abbeLimit = lambdaExc / (2.0 * na);

        // Depletion saturation factor: zeta = I_sted / I_sat
        var isat = Math.Max(0.5, telemetry.SaturationIntensityMegawattsPerSquareCm);
        var pSted = Math.Max(0.0, telemetry.StedDoughnutLaserPowerMilliwatts);
        var saturationRatio = pSted / (5.0 * isat);

        // Deformable mirror / adaptive optics wavefront aberration degradation factor
        var wfe = Math.Max(0.0, telemetry.AdaptiveOpticsWavefrontRmsErrorWavelengths);
        var aberrationPenalty = 1.0 + 2.8 * wfe;

        // Effective STED resolution: d_eff = d_abbe / sqrt(1 + zeta) * aberrationPenalty
        var idealStedLimit = abbeLimit / Math.Sqrt(1.0 + saturationRatio);
        var effectiveResolution = idealStedLimit * aberrationPenalty;
        var improvementFactor = abbeLimit / effectiveResolution;

        // Phototoxicity and fluorophore destruction index
        var bleachRate = Math.Max(0.0, telemetry.PhotobleachingDecayRatePercentPerFrame);
        var phototoxicityRisk = (pSted / 200.0) * (1.0 + (bleachRate / 6.0));

        string regime;
        string guidance;
        bool isOptimal;

        if (bleachRate > 8.0 || phototoxicityRisk > 1.25)
        {
            regime = "SEVERE_PHOTOBLEACHING_PHOTOTOXICITY";
            guidance = "Excessive fluorophore destruction and cellular phototoxicity. Attenuate STED doughnut laser power or pulse-interleave illumination.";
            isOptimal = false;
        }
        else if (wfe > 0.12)
        {
            regime = "ABERRATION_DEFORMED_DOUGHNUT_CREST";
            guidance = "Specimen refractive index mismatch induced severe spherical/coma aberrations (> 0.12 lambda). Update deformable mirror Zernike correction modes.";
            isOptimal = false;
        }
        else if (pSted < 25.0)
        {
            regime = "DIFFRACTION_LIMITED_UNDERDEPLETED";
            guidance = "STED laser intensity insufficient to saturate stimulated emission depletion. Resolution remains near classical Abbe diffraction limit.";
            isOptimal = false;
        }
        else
        {
            regime = "NANOSCALE_SUPER_RESOLUTION_OPTIMAL";
            guidance = "Doughnut depletion zero-intensity center preserved. Sub-diffraction nanoscale resolution attained with sustainable photobleaching rate.";
            isOptimal = true;
        }

        return new StedMicroscopyAnalysisResult(
            MicroscopeId: telemetry.MicroscopeId,
            AbbeDiffractionLimitNanometers: abbeLimit,
            EffectiveStedResolutionNanometers: effectiveResolution,
            ResolutionImprovementFactor: improvementFactor,
            PhototoxicityRiskIndex: phototoxicityRisk,
            MicroscopyRegime: regime,
            LaserPowerAndOpticsGuidance: guidance,
            IsImagingOptimal: isOptimal
        );
    }
}
