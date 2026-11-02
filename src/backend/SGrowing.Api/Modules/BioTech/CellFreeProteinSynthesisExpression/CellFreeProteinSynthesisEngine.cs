using System;

namespace SGrowing.Api.Modules.BioTech.CellFreeProteinSynthesisExpression;

public interface ICellFreeProteinSynthesisEngine
{
    CellFreeProteinSynthesisAnalysisResult AnalyzeSynthesis(CellFreeProteinSynthesisTelemetry telemetry);
}

public sealed class CellFreeProteinSynthesisEngine : ICellFreeProteinSynthesisEngine
{
    private const double MinDnaHalfLifeMinutes = 120.0; // At least 2h half life for robust yield
    private const double CriticalRbsHairpinDGFreeEnergy = -14.0; // Hairpin tighter than -14 kcal/mol severely impedes initiation
    private const double TargetMinYieldMgPerMl = 0.75; // mg/mL in CFPS

    public CellFreeProteinSynthesisAnalysisResult AnalyzeSynthesis(CellFreeProteinSynthesisTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.LinearDnaConcentrationNm <= 0.0)
        {
            throw new ArgumentException("Linear DNA concentration must be positive.", nameof(telemetry));
        }

        // 1. Linear DNA Degradation Half-Life with GamS Exonuclease Inhibitor
        // k_deg = k0 / (1 + (GamS / Ki)^2) where Ki ~ 0.5 uM
        double k0 = Math.Max(0.001, telemetry.UninhibitedDnaDegradationConstantPerMin);
        double kiGamS = 0.5; // uM
        double inhibitionFactor = 1.0 + Math.Pow(telemetry.GamSInhibitorConcentrationUm / kiGamS, 2.0);
        double effectiveKdeg = k0 / inhibitionFactor;
        double dnaHalfLifeMin = Math.Log(2.0) / effectiveKdeg;

        // 2. Translation Initiation Efficiency from 5' RBS Secondary Structure
        // dG_RBS: more negative than -14 kcal/mol causes exponential repression
        double initiationEfficiency;
        if (telemetry.RbsHairpinFreeEnergyKcalPerMol >= -10.0)
        {
            initiationEfficiency = 0.95;
        }
        else if (telemetry.RbsHairpinFreeEnergyKcalPerMol <= -16.0)
        {
            initiationEfficiency = 0.15;
        }
        else
        {
            // Linear interpolate between -10 (0.95) and -16 (0.15)
            double frac = (telemetry.RbsHairpinFreeEnergyKcalPerMol - (-16.0)) / 6.0;
            initiationEfficiency = 0.15 + 0.80 * frac;
        }

        // 3. Ribosome Elongation Stall Penalty
        // S_stall: values above 3.5 cause premature drop-off
        double elongationFactor = 1.0 / (1.0 + 0.35 * Math.Max(0.0, telemetry.RareCodonStallingIndex));

        // 4. Ionic Cation Fitness (Mg2+ opt: 12 mM, K+ opt: 150 mM)
        double mgDeviation = Math.Abs(telemetry.MagnesiumConcentrationMm - 12.0) / 4.0; // tolerance ~ 4 mM
        double kDeviation = Math.Abs(telemetry.PotassiumConcentrationMm - 150.0) / 50.0; // tolerance ~ 50 mM
        double cationFitness = Math.Clamp(1.0 - (0.5 * mgDeviation + 0.5 * kDeviation), 0.1, 1.0);

        // 5. Projected Protein Yield (mg/mL)
        // Max theoretical yield ~ 1.8 mg/mL in high-performance lysate
        double dnaPersistenceFactor = Math.Clamp(dnaHalfLifeMin / (dnaHalfLifeMin + 180.0), 0.05, 1.0);
        double maxBatchYield = 1.65;
        double projectedYield = maxBatchYield * dnaPersistenceFactor * initiationEfficiency * elongationFactor * cationFitness;
        projectedYield = Math.Round(Math.Clamp(projectedYield, 0.01, 2.5), 3);

        // 6. Validation Checks
        bool isDnaProtected = dnaHalfLifeMin >= MinDnaHalfLifeMinutes;
        bool isRbsAccessible = telemetry.RbsHairpinFreeEnergyKcalPerMol > CriticalRbsHairpinDGFreeEnergy;
        bool isYieldTargetAchieved = projectedYield >= TargetMinYieldMgPerMl;

        // 7. Regime & Recommendations
        string regime;
        string recommendation;

        if (!isDnaProtected)
        {
            regime = "EXONUCLEASE_TEMPLATE_RAPID_DIGESTION";
            recommendation = $"DNA half-life ({dnaHalfLifeMin:F1} min) is inadequate. Supplement GamS concentration to >= 2.5 uM or use terminal phosphorothioate/hairpin end caps to protect linear PCR templates.";
        }
        else if (!isRbsAccessible)
        {
            regime = "RBS_SECONDARY_HAIRPIN_TRANSLATION_BLOCKADE";
            recommendation = $"RBS folding free energy ({telemetry.RbsHairpinFreeEnergyKcalPerMol:F1} kcal/mol) forms a tight secondary hairpin. Introduce synonymous codon mutations in the first 15 nt of the coding sequence to relax secondary structure.";
        }
        else if (telemetry.RareCodonStallingIndex >= 4.0)
        {
            regime = "RARE_CODON_RIBOSOME_QUEUE_COLLAPSE";
            recommendation = $"High rare codon stalling index ({telemetry.RareCodonStallingIndex:F2}) induces ribosome queuing. Harmonize synonymous codons for host tRNA pool or supplement rare tRNAs (e.g. Rosetta lysate).";
        }
        else if (!isYieldTargetAchieved)
        {
            regime = "SUB_OPTIMAL_EXPRESSION_CATION_IMBALANCE";
            recommendation = $"Projected yield ({projectedYield:F3} mg/mL) fell below target ({TargetMinYieldMgPerMl:F2} mg/mL). Optimize Mg2+/K+ ionic titration and elevate linear DNA concentration.";
        }
        else
        {
            regime = "HIGH_YIELD_CELL_FREE_SYNTHESIS_STABLE";
            recommendation = "CFPS parameters fully optimal: robust GamS exonuclease protection, unhindered RBS initiation, minimal codon stalling, and excellent projected protein expression yield.";
        }

        return new CellFreeProteinSynthesisAnalysisResult
        {
            BatchId = telemetry.BatchId,
            DnaDegradationHalfLifeMinutes = Math.Round(dnaHalfLifeMin, 1),
            TranslationInitiationEfficiencyPercent = Math.Round(initiationEfficiency * 100.0, 1),
            RibosomeElongationStallPenalty = Math.Round(1.0 - elongationFactor, 3),
            IonicCationFitnessFactor = Math.Round(cationFitness, 3),
            ProjectedProteinYieldMgPerMl = projectedYield,
            IsDnaSufficientlyProtected = isDnaProtected,
            IsRbsFreeOfStrongSecondaryStructures = isRbsAccessible,
            IsYieldTargetAchieved = isYieldTargetAchieved,
            ExpressionRegime = regime,
            OptimizationRecommendation = recommendation
        };
    }
}
