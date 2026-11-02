using System;

namespace SGrowing.Api.Modules.BioTech.EpigeneticMethylationClock;

public sealed class EpigeneticMethylationClockEngine : IEpigeneticMethylationClockEngine
{
    public EpigeneticClockAnalysisResult AnalyzeEpigeneticClock(EpigeneticClockTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var chronoAge = Math.Max(1.0, telemetry.ChronologicalAgeYears);
        var beta = Math.Clamp(telemetry.MeanCpGIslandMethylationBeta, 0.05, 0.95);
        var hdac = Math.Max(0.0, telemetry.HdacEnzymeActivityUnits);

        // Horvath-like transformed biological age calculation
        var methylationScale = 0.70 + 0.55 * beta;
        var hdacShift = (hdac - 15.0) * 0.45;
        var bioAge = Math.Max(1.0, chronoAge * methylationScale + hdacShift);

        var ageAcceleration = bioAge - chronoAge;

        // Genomic stability index
        var line1 = Math.Clamp(telemetry.Line1RetrotransposonMethylationPercent, 10.0, 100.0);
        var h3k27ac = Math.Clamp(telemetry.HistoneAcetylationH3K27acRatio, 0.1, 3.0);
        var hypermeth = Math.Clamp(telemetry.TumorSuppressorPromoterHypermethylationPercent, 0.0, 100.0);

        var stabilityScore = Math.Clamp((line1 / 85.0) * 0.60 + (h3k27ac / 1.5) * 0.40 - (hypermeth / 100.0) * 0.45, 0.0, 1.0);

        string regime;
        string guidance;
        bool isOptimal;

        if (telemetry.TumorSuppressorPromoterHypermethylationPercent > 45.0)
        {
            regime = "EPIGENETIC_MALIGNANCY_SILENCING_HAZARD";
            guidance = "Critical hypermethylation of tumor suppressor promoters (p16/BRCA1). Administer DNA methyltransferase (DNMT) inhibitor (5-azacytidine) intervention.";
            isOptimal = false;
        }
        else if (telemetry.Line1RetrotransposonMethylationPercent < 55.0)
        {
            regime = "GENOME_HYPOMETHYLATION_INSTABILITY";
            guidance = "Severe hypomethylation of repetitive LINE-1 retrotransposons (< 55%). Genomic retrotransposition risk; supplement S-adenosylmethionine (SAMe) methyl donors.";
            isOptimal = false;
        }
        else if (ageAcceleration > 8.0)
        {
            regime = "ACCELERATED_EPIGENETIC_SENESCENCE";
            guidance = "Epigenetic biological age accelerated > 8 years over chronological age. Titrate selective HDAC inhibitor (vorinostat/trichostatin A) and NAD+ precursors.";
            isOptimal = false;
        }
        else
        {
            regime = "REJUVENATED_CHROMATIN_HOMEOSTASIS";
            guidance = "Epigenetic methylation clock aligned or rejuvenated relative to chronological age. Chromatin architecture and histone acetylation optimal.";
            isOptimal = true;
        }

        return new EpigeneticClockAnalysisResult(
            SampleId: telemetry.SampleId,
            BiologicalEpigeneticAgeYears: bioAge,
            EpigeneticAgeAccelerationYears: ageAcceleration,
            GenomicStabilityIndex: stabilityScore,
            EpigeneticRegime: regime,
            TherapeuticEpigeneticGuidance: guidance,
            IsEpigeneticProfileOptimal: isOptimal
        );
    }
}
