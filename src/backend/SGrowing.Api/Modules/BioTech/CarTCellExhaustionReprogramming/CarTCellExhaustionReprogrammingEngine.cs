using System;

namespace SGrowing.Api.Modules.BioTech.CarTCellExhaustionReprogramming;

public sealed class CarTCellExhaustionReprogrammingEngine : ICarTCellExhaustionReprogrammingEngine
{
    public CarTCellExhaustionAnalysisResult AnalyzeCarTExhaustion(CarTCellExhaustionTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var tox = Math.Max(0.0, telemetry.ToxTranscriptionFactorRelativeExpression);
        var nr4a = Math.Max(0.0, telemetry.Nr4aTranscriptionFactorRelativeExpression);

        var pd1 = Math.Max(0.0, telemetry.Pd1CheckpointSurfaceDensityMfi);
        var tim3 = Math.Max(0.0, telemetry.Tim3CheckpointSurfaceDensityMfi);
        var lag3 = Math.Max(0.0, telemetry.Lag3CheckpointSurfaceDensityMfi);
        var checkpointAvg = (pd1 + tim3 + lag3) / 3.0;

        // Base exhaustion drive
        var baseExhaustion = (tox * 0.40) + (nr4a * 0.25) + ((checkpointAvg / 50.0) * 0.35);

        // Interventions
        var toxMod = telemetry.IsToxCrisprKnockoutApplied ? 0.30 : 1.0;
        var cjunMod = telemetry.IsCJunOverexpressionApplied ? 0.55 : 1.0;

        var compositeExhaustion = Math.Clamp(baseExhaustion * toxMod * cjunMod, 0.05, 10.0);

        // Functional Cytotoxicity Index (degranulation efficiency adjusted for exhaustion)
        var rawDegran = Math.Max(1.0, telemetry.DegranulationCd107aFractionPercent);
        var cytotoxicity = Math.Clamp((rawDegran * (telemetry.IsCJunOverexpressionApplied ? 1.35 : 1.0)) / (1.0 + compositeExhaustion * 0.30), 2.0, 99.9);

        // Memory Stemness Retention Index
        var rawStem = Math.Max(0.5, telemetry.StemCellMemoryFractionPercent);
        var stemBoost = (telemetry.IsCJunOverexpressionApplied ? 1.45 : 1.0) * (telemetry.IsToxCrisprKnockoutApplied ? 1.25 : 1.0);
        var memoryRetention = Math.Clamp(rawStem * stemBoost, 0.5, 99.9);

        string regime;
        string guidance;
        bool isDurable;

        if (compositeExhaustion > 3.0 && memoryRetention < 20.0)
        {
            regime = "TERMINAL_EPIGENETIC_CAR_T_EXHAUSTION";
            guidance = "CAR-T cell batch trapped in irreversibly exhausted chromatin state (high TOX/NR4A, low T_SCM). Apply CRISPR-Cas9 TOX gene ablation and c-Jun viral transduction.";
            isDurable = false;
        }
        else if (compositeExhaustion > 1.75 || checkpointAvg > 75.0)
        {
            regime = "PROGRESSIVE_IMMUNE_CHECKPOINT_DYSFUNCTION";
            guidance = "Elevated surface expression of PD-1, TIM-3, and LAG-3 dampening signaling. Co-administer checkpoint blockade inhibitors or dual-target CAR construct.";
            isDurable = false;
        }
        else if (memoryRetention < 15.0)
        {
            regime = "SHORT_LIVED_EFFECTOR_DIFFERENTIATION_WARNING";
            guidance = "T_SCM stem memory pool depleted (< 15%). High risk of rapid in vivo clearance post-infusion. Culture with IL-7/IL-15 and Akt inhibitors to preserve stemness.";
            isDurable = false;
        }
        else
        {
            regime = "EPIGENETICALLY_REPROGRAMMED_STEM_LIKE_DURABLE_REGIME";
            guidance = "Robust self-renewing T_SCM stem memory phenotype retained with high cytotoxicity. Excellent in vivo persistence and solid tumor infiltration projected.";
            isDurable = true;
        }

        return new CarTCellExhaustionAnalysisResult(
            CellBatchId: telemetry.CellBatchId,
            CompositeExhaustionScore: compositeExhaustion,
            FunctionalCytotoxicityIndex: cytotoxicity,
            MemoryStemnessRetentionIndex: memoryRetention,
            ExhaustionRegime: regime,
            TherapeuticReprogrammingGuidance: guidance,
            IsTherapeuticallyDurable: isDurable
        );
    }
}
