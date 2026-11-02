namespace SGrowing.Api.Modules.BioTech.MicrobialCrisprCascade;

public interface IMicrobialCrisprCascadeEngine
{
    MetabolicCascadeAssessment EvaluateCascade(MicrobialCascadeEvaluationRequest request);
}

public class MicrobialCrisprCascadeEngine : IMicrobialCrisprCascadeEngine
{
    public MetabolicCascadeAssessment EvaluateCascade(MicrobialCascadeEvaluationRequest request)
    {
        var st = request.State;
        var thresh = request.Thresholds;

        bool odSwitchReached = st.OpticalDensity600 >= thresh.CriticalOd600Trigger;
        bool acetateOverflow = st.AcetateConcentrationGramsPerLiter >= thresh.MaxAllowableAcetateGramsPerLiter;
        bool hypoxia = st.DissolvedOxygenPercent < thresh.MinimumDissolvedOxygenPercent;

        // Cellular metabolic burden from maintaining dCas9 and active sgRNA guide arrays:
        double burdenIndex = (st.ActiveSgRnaGuideCount * 0.045) + (Math.Min(15.0, st.OpticalDensity600) * 0.018);
        burdenIndex = Math.Clamp(burdenIndex, 0.05, 0.95);

        double carbonFluxRedirectionPercent;
        double predictedYieldGramsPerLiter;
        string phase;
        string directive;

        if (acetateOverflow)
        {
            phase = "ACETATE_OVERFLOW_TOXICITY";
            carbonFluxRedirectionPercent = 35.0;
            predictedYieldGramsPerLiter = st.TargetMetaboliteGramsPerLiter + 1.2;
            directive = $"REPRESS_PTA_ACKA: Acetate ({st.AcetateConcentrationGramsPerLiter:F2} g/L) exceeds {thresh.MaxAllowableAcetateGramsPerLiter:F1} g/L toxicity limit. Boost dCas9 repression of phosphotransacetylase and throttle glucose feed rate by 35% to prevent cell lysis.";
        }
        else if (odSwitchReached)
        {
            phase = "STATIONARY_BIOSYNTHESIS_CASCADE";
            // High efficiency redirection towards product synthesis
            carbonFluxRedirectionPercent = Math.Clamp(75.0 + (st.ActiveSgRnaGuideCount * 2.5) - (burdenIndex * 15.0), 55.0, 92.0);
            predictedYieldGramsPerLiter = st.TargetMetaboliteGramsPerLiter + (st.OpticalDensity600 * 0.85 * (carbonFluxRedirectionPercent / 100.0));
            directive = $"CASCADE_ACTIVE: Optical density threshold met ({st.OpticalDensity600:F1} OD600). dCas9 transcriptional switch engaged; {carbonFluxRedirectionPercent:F1}% carbon flux redirected to target molecule synthesis.";
        }
        else
        {
            phase = "EXPONENTIAL_BIOMASS_EXPANSION";
            carbonFluxRedirectionPercent = 15.0; // Minimal redirection, prioritizing growth
            predictedYieldGramsPerLiter = st.TargetMetaboliteGramsPerLiter;
            directive = $"ACCUMULATE_BIOMASS: Culture OD600 ({st.OpticalDensity600:F1}) is below threshold ({thresh.CriticalOd600Trigger:F1}). Keep dCas9 uninduced to maximize specific growth rate (mu_max) until target density.";
        }

        if (hypoxia && !acetateOverflow)
        {
            directive += " [ALERT: Oxygen mass transfer limitation detected; boost impeller agitation to 800 RPM.]";
        }

        return new MetabolicCascadeAssessment(
            BioreactorId: st.BioreactorId,
            Dcas9CascadeTriggered: odSwitchReached && !acetateOverflow,
            CarbonFluxRedirectionPercent: Math.Round(carbonFluxRedirectionPercent, 1),
            PredictedFinalYieldGramsPerLiter: Math.Round(predictedYieldGramsPerLiter, 2),
            MetabolicBurdenIndex: Math.Round(burdenIndex, 3),
            FermentationPhase: phase,
            RecommendedCascadeDirective: directive
        );
    }
}
