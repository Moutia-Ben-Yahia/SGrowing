using System;

namespace SGrowing.Api.Modules.BioTech.PrimeEditingSearchAndReplace;

public sealed class PrimeEditingSearchAndReplaceEngine : IPrimeEditingSearchAndReplaceEngine
{
    public PrimeEditingAnalysisResult AnalyzePrimeEditing(PrimeEditingTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var pbsLen = Math.Max(5, telemetry.PrimerBindingSiteLengthNucleotides);
        var rttLen = Math.Max(5, telemetry.ReverseTranscriptionTemplateLengthNucleotides);
        var pbsTm = telemetry.PbsMeltingTemperatureCelsius;
        var rttGc = Math.Clamp(telemetry.RttGcContentPercent, 5.0, 95.0);
        var accessScore = Math.Clamp(telemetry.TargetGenomicLocusAccessibilityScore, 0.05, 1.0);

        // PBS annealing fitness (peaks around 35-42 C and length 13-16 nt)
        var pbsLengthPenalty = Math.Abs(pbsLen - 14) * 0.04;
        var pbsTmFactor = Math.Clamp(1.0 - (Math.Abs(pbsTm - 38.0) / 30.0), 0.1, 1.0);
        var pbsEfficiency = Math.Clamp((1.0 - pbsLengthPenalty) * pbsTmFactor, 0.1, 1.0);

        // RTT RT extension factor (optimal GC 40-60%)
        var rttGcPenalty = Math.Abs(rttGc - 50.0) * 0.012;
        var rttLengthPenalty = rttLen > 30 ? (rttLen - 30) * 0.015 : 0.0;
        var rttEfficiency = Math.Clamp(1.0 - rttGcPenalty - rttLengthPenalty, 0.1, 1.0);

        // Secondary structure impediment (hairpin DeltaG < -6.5 stalls RT)
        var deltaG = telemetry.SecondaryStructureFreeEnergyDeltaG;
        var hairpinFactor = deltaG < -6.5 ? 0.35 : (deltaG < -4.0 ? 0.70 : 1.0);

        // Base PE2 editing efficiency
        var basePe2Efficiency = 28.0 * pbsEfficiency * rttEfficiency * hairpinFactor * accessScore;
        var basePe2Indel = 0.35;

        double editingEfficiency;
        double indelRate;
        string systemType;

        if (telemetry.IsPe3NonEditedStrandNickApplied)
        {
            systemType = "PE3_DUAL_NICKING_SEARCH_AND_REPLACE";
            var nickDist = telemetry.NonEditedStrandNickDistanceBasePairs;

            if (nickDist < 35.0)
            {
                // Proximal nicking generates double-strand breaks
                editingEfficiency = basePe2Efficiency * 1.8;
                indelRate = 7.5 + (35.0 - nickDist) * 0.25;
            }
            else if (nickDist <= 95.0)
            {
                // Optimal PE3 window (40-90 bp)
                editingEfficiency = Math.Clamp(basePe2Efficiency * 2.6, 5.0, 88.0);
                indelRate = 0.85 + (Math.Abs(nickDist - 60.0) / 100.0) * 0.5;
            }
            else
            {
                // Distant nicking decays PE3 enhancement
                editingEfficiency = Math.Clamp(basePe2Efficiency * 1.6, 5.0, 75.0);
                indelRate = 0.65;
            }
        }
        else
        {
            systemType = "PE2_SINGLE_STRAND_SEARCH_AND_REPLACE";
            editingEfficiency = Math.Clamp(basePe2Efficiency, 2.0, 45.0);
            indelRate = basePe2Indel;
        }

        var editToIndelRatio = editingEfficiency / Math.Max(0.01, indelRate);

        string regime;
        string guidance;
        bool isPrecisionHigh;

        if (telemetry.IsPe3NonEditedStrandNickApplied && telemetry.NonEditedStrandNickDistanceBasePairs < 35.0)
        {
            regime = "DOUBLE_STRAND_BREAK_INDEL_SURGE";
            guidance = "Non-edited strand nick placed too close to primary target (< 35 bp), inducing double-strand breaks and high indel frequency (> 5%). Relocate sgRNA nick site to +45 to +70 bp.";
            isPrecisionHigh = false;
        }
        else if (deltaG < -6.5)
        {
            regime = "PEGRNA_HAIRPIN_REVERSE_TRANSCRIPTION_STALL";
            guidance = "Stable secondary hairpin structure detected in pegRNA extension (Delta G < -6.5 kcal/mol). Introduce synonymous nucleotide substitutions or incorporate evoPreQ1 3' structural motif.";
            isPrecisionHigh = false;
        }
        else if (pbsTm < 28.0 || pbsLen < 11)
        {
            regime = "PBS_HYBRIDIZATION_INSTABILITY_WARNING";
            guidance = "Primer binding site (PBS) melting temperature below 28 C or length < 11 nt. Extend PBS by 2-3 nucleotides to stabilize initiation flap annealing.";
            isPrecisionHigh = false;
        }
        else
        {
            regime = "HIGH_PRECISION_SEARCH_AND_REPLACE_REGIME";
            guidance = "Optimal PBS/RTT thermodynamics and mismatch repair bias. High on-target search-and-replace editing efficiency with negligible indel formation.";
            isPrecisionHigh = true;
        }

        return new PrimeEditingAnalysisResult(
            EditTargetLocusId: telemetry.EditTargetLocusId,
            PredictedOnTargetEditingEfficiencyPercent: editingEfficiency,
            PredictedIndelFormationRatePercent: indelRate,
            EditToIndelPrecisionRatio: editToIndelRatio,
            PrimeEditingSystemType: systemType,
            PrimeEditingRegime: regime,
            PegRnaOptimizationGuidance: guidance,
            IsEditingPrecisionHigh: isPrecisionHigh
        );
    }
}
