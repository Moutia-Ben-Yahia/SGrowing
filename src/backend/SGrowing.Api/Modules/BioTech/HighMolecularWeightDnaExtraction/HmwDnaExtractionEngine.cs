namespace SGrowing.Api.Modules.BioTech.HighMolecularWeightDnaExtraction;

public interface IHmwDnaExtractionEngine
{
    HmwDnaExtractionEvaluation AnalyzeExtraction(HmwDnaExtractionTelemetry telemetry);
}

public class HmwDnaExtractionEngine : IHmwDnaExtractionEngine
{
    public HmwDnaExtractionEvaluation AnalyzeExtraction(HmwDnaExtractionTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Purity evaluations
        bool proteinClean = telemetry.AbsorbanceRatio260_280 >= 1.75 && telemetry.AbsorbanceRatio260_280 <= 2.05;
        bool saltClean = telemetry.AbsorbanceRatio260_230 >= 1.85;
        bool isChemicalPurityCompliant = proteinClean && saltClean;

        // Length profile
        bool isLengthProfileUltraLongReady = telemetry.FragmentLengthN50Kbp >= 35.0 && telemetry.UltraLongFractionAbove100KbpPercent >= 18.0;

        // Hydrodynamic shear
        bool isHydrodynamicShearControlled = telemetry.PipettingHydrodynamicShearRateSecInv <= 250.0;

        // Nanopore pore blocking risk index (0.0 to 10.0)
        double poreBlockingHazard = 1.0;
        if (telemetry.AbsorbanceRatio260_230 < 1.85)
        {
            poreBlockingHazard += (1.85 - telemetry.AbsorbanceRatio260_230) * 8.0;
        }
        if (!proteinClean)
        {
            poreBlockingHazard += 2.5;
        }
        if (telemetry.ActiveNanoporePoreFractionPercent < 80.0)
        {
            poreBlockingHazard += (80.0 - telemetry.ActiveNanoporePoreFractionPercent) * 0.15;
        }
        poreBlockingHazard = Math.Clamp(poreBlockingHazard, 0.5, 10.0);

        bool isNanoporeTranslocationOptimal = telemetry.ActiveNanoporePoreFractionPercent >= 75.0 &&
                                              telemetry.TranslocationSpeedBasesPerSecond >= 380.0 &&
                                              telemetry.TranslocationSpeedBasesPerSecond <= 480.0 &&
                                              poreBlockingHazard <= 3.5;

        // Overall readiness score
        double score = 100.0;
        if (!isLengthProfileUltraLongReady) score -= 30.0;
        if (!isChemicalPurityCompliant) score -= 25.0;
        if (!isHydrodynamicShearControlled) score -= 15.0;
        if (!isNanoporeTranslocationOptimal) score -= 20.0;
        score = Math.Clamp(score, 0.0, 100.0);

        if (!isLengthProfileUltraLongReady)
        {
            regimes.Add("HYDRODYNAMIC_SHEAR_FRAGMENTATION_ALERT");
            mitigations.Add("Switch to magnetic bead or gravity-flow membrane isolation with wide-bore pipette tips to elevate N50 >= 40 kbp.");
        }

        if (!saltClean)
        {
            regimes.Add("CHAOTROPIC_SALT_PHENOL_PORE_BLOCKING");
            mitigations.Add("Perform an additional 70% ethanol/isopropanol wash step and extended elution incubation to remove residual chaotropic salts (A260/230 >= 2.0).");
        }

        if (!proteinClean)
        {
            regimes.Add("PROTEIN_HISTONE_RESIDUAL_CONTAMINATION");
            mitigations.Add("Extend proteinase K enzymatic lysis digestion at 56C to digest tightly bound histone core complexes (A260/280 in 1.8-2.0).");
        }

        return new HmwDnaExtractionEvaluation
        {
            ExtractionBatchId = telemetry.ExtractionBatchId,
            CalculatedSequencingReadinessScorePercent = Math.Round(score, 1),
            EstimatedPoreBlockingHazardIndex = Math.Round(poreBlockingHazard, 2),
            IsLengthProfileUltraLongReady = isLengthProfileUltraLongReady,
            IsChemicalPurityCompliant = isChemicalPurityCompliant,
            IsHydrodynamicShearControlled = isHydrodynamicShearControlled,
            IsNanoporeTranslocationOptimal = isNanoporeTranslocationOptimal,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
