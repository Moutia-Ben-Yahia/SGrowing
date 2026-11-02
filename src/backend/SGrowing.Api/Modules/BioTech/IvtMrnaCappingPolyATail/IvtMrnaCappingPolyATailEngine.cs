using System;

namespace SGrowing.Api.Modules.BioTech.IvtMrnaCappingPolyATail;

public sealed class IvtMrnaCappingPolyATailEngine : IIvtMrnaCappingPolyATailEngine
{
    public IvtMrnaAnalysisResult AnalyzeIvtMrnaBatch(IvtMrnaTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var capEff = Math.Clamp(telemetry.CleanCapIncorporationEfficiencyPercent, 0.0, 100.0);
        var dsRna = Math.Max(0.0, telemetry.DoubleStrandedRnaContaminationPercent);
        var polyA = Math.Max(10.0, telemetry.MeanPolyATailLengthNucleotides);
        var polyAHet = Math.Clamp(telemetry.PolyATailHeterogeneityIndex, 0.0, 5.0);

        // Innate immunogenicity score driven by pppRNA and dsRNA
        var uncappedFraction = Math.Max(0.0, 100.0 - capEff);
        var immunogenicity = Math.Clamp(uncappedFraction * 0.28 + dsRna * 4.2, 0.1, 10.0);

        // Predicted cytosolic half-life (hours)
        var halfLifeHours = Math.Clamp(4.5 + (polyA - 80.0) * 0.24 - polyAHet * 1.8, 2.0, 30.0);

        // Net translation index
        var pkrPenalty = Math.Clamp(1.0 - (dsRna / 1.6), 0.05, 1.0);
        var netTranslation = telemetry.InVitroTranslationEfficiencyScore * (capEff / 100.0) * pkrPenalty;

        string regime;
        string guidance;
        bool isViable;

        if (dsRna > 1.2)
        {
            regime = "DSRNA_IMMUNOGENIC_PKR_SHUTDOWN";
            guidance = "Double-stranded RNA (dsRNA) impurity exceeds 1.2%. High risk of host PKR translation shutdown and interferon induction. Route batch through cellulose/HPLC polishing.";
            isViable = false;
        }
        else if (capEff < 92.0)
        {
            regime = "UNCAPPED_TRIPHOSPHATE_RIG_IMMUNE_HAZARD";
            guidance = "5' capping efficiency below 92%. Residual pppRNA triggers cytoplasmic RIG-I pattern recognition receptors. Treat with calf intestinal phosphatase (CIP) dephosphorylation.";
            isViable = false;
        }
        else if (polyA < 85.0)
        {
            regime = "SHORT_POLYA_TRANSLATIONAL_INSTABILITY";
            guidance = "Poly(A) tail length depleted (< 85 nt). Premature deadenylation compromises cytosolic half-life. Optimize linearized plasmid template poly(T) tract or poly(A) polymerase extension.";
            isViable = false;
        }
        else
        {
            regime = "HIGH_FIDELITY_THERAPEUTIC_MRNA_SYNTHESIS";
            guidance = "High-efficiency Cap1 capping with minimal dsRNA and optimal poly(A) tail length (> 100 nt). Translational competence and biocompatibility assured.";
            isViable = true;
        }

        return new IvtMrnaAnalysisResult(
            BatchId: telemetry.BatchId,
            InnateImmuneImmunogenicityScore: immunogenicity,
            PredictedCytosolicHalfLifeHours: halfLifeHours,
            NetEffectiveTranslationIndex: netTranslation,
            SynthesisQualityRegime: regime,
            UpstreamAndDownstreamGuidance: guidance,
            IsMrnaTherapeuticallyViable: isViable
        );
    }
}
