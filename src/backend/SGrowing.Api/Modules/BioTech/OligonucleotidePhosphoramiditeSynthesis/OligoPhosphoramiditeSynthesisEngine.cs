using System;

namespace SGrowing.Api.Modules.BioTech.OligonucleotidePhosphoramiditeSynthesis;

public interface IOligoPhosphoramiditeSynthesisEngine
{
    OligoPhosphoramiditeAnalysisResult AnalyzeSynthesis(OligoPhosphoramiditeTelemetry telemetry);
}

public sealed class OligoPhosphoramiditeSynthesisEngine : IOligoPhosphoramiditeSynthesisEngine
{
    private const double MinTargetStepwiseCouplingYield = 99.2; // SCY >= 99.2% for gene-length oligos
    private const double MaxAllowableSolventMoisturePpm = 30.0; // Karl Fischer < 30 ppm
    private const double MinCappingEfficiencyPercent = 98.5; // Capping >= 98.5% to suppress N-1 deletions
    private const double MinTargetFullLengthPurityPercent = 45.0; // P_full >= 45% for long oligos

    public OligoPhosphoramiditeAnalysisResult AnalyzeSynthesis(OligoPhosphoramiditeTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.OligoLengthNtds <= 1)
        {
            throw new ArgumentException("Oligonucleotide length must be greater than 1 nucleotide.", nameof(telemetry));
        }

        // 1. Stepwise Coupling Yield: Reconcile between trityl ratio and reported SCY
        double tritylScy = telemetry.InLineTritylAbsorbance498NmRatio * 100.0;
        double effectiveScy = Math.Min(telemetry.StepwiseCouplingYieldPercent, tritylScy);
        effectiveScy = Math.Round(Math.Clamp(effectiveScy, 80.0, 99.95), 2);

        // 2. Moisture Hydrolysis Risk: Exponential degradation if moisture exceeds 30 ppm
        double excessMoisture = Math.Max(0.0, telemetry.AcetonitrileMoisturePpm - MaxAllowableSolventMoisturePpm);
        double moistureHydrolysisRisk = 1.0 - Math.Exp(-0.045 * excessMoisture);
        moistureHydrolysisRisk = Math.Round(moistureHydrolysisRisk * 100.0, 2);

        // 3. Cumulative Full-Length Product Purity: P_full = (SCY / 100)^(N - 1) * (1 - moistureRisk)
        int couplingCycles = telemetry.OligoLengthNtds - 1;
        double scyFraction = effectiveScy / 100.0;
        double theoreticalPurity = Math.Pow(scyFraction, couplingCycles);
        double moisturePenalty = 1.0 - (moistureHydrolysisRisk / 100.0);
        double cumulativePurityPercent = theoreticalPurity * moisturePenalty * 100.0;
        cumulativePurityPercent = Math.Round(Math.Clamp(cumulativePurityPercent, 0.01, 99.0), 2);

        // 4. Estimated N-1 Deletion Purity Loss:
        // Uncoupled 5'-OH that fails capping propagates into N-1 failure sequence
        double uncappedFraction = (1.0 - scyFraction) * (1.0 - (telemetry.CappingEfficiencyPercent / 100.0));
        double nMinus1LossPercent = Math.Round(uncappedFraction * couplingCycles * 100.0, 2);

        // 5. Validation Checks
        bool isScyOk = effectiveScy >= MinTargetStepwiseCouplingYield;
        bool isMoistureOk = telemetry.AcetonitrileMoisturePpm <= MaxAllowableSolventMoisturePpm;
        bool isCappingOk = telemetry.CappingEfficiencyPercent >= MinCappingEfficiencyPercent;
        bool isPurityOk = cumulativePurityPercent >= MinTargetFullLengthPurityPercent;

        // 6. Regime & Guidance
        string regime;
        string guidance;

        if (!isMoistureOk)
        {
            regime = "ANHYDROUS_SOLVENT_MOISTURE_HYDROLYSIS_CRISIS";
            guidance = $"Acetonitrile solvent moisture ({telemetry.AcetonitrileMoisturePpm:F1} ppm) exceeds the critical Karl Fischer limit ({MaxAllowableSolventMoisturePpm:F1} ppm), causing rapid phosphoramidite hydrolysis. Replace solvent lines with fresh activated molecular sieve-dried acetonitrile.";
        }
        else if (!isScyOk)
        {
            regime = "SUB_CRITICAL_STEPWISE_COUPLING_YIELD_DEFICIT";
            guidance = $"Stepwise coupling yield ({effectiveScy:F2}%) fell below target ({MinTargetStepwiseCouplingYield:F1}%). Extend 5-(ethylthio)-1H-tetrazole (ETT) or DCI activator coupling contact time to >= 120 seconds and check phosphoramidite amidite lot purity.";
        }
        else if (!isCappingOk)
        {
            regime = "INCOMPLETE_CAPPING_N_MINUS_ONE_MUTATION_ACCUMULATION";
            guidance = $"Capping efficiency ({telemetry.CappingEfficiencyPercent:F1}%) is sub-threshold ({MinCappingEfficiencyPercent:F1}%), creating elevated N-1 truncated deletion sequences ({nMinus1LossPercent:F2}% loss). Replenish Cap A (acetic anhydride/pyridine) and Cap B (N-methylimidazole) reagents.";
        }
        else if (!isPurityOk)
        {
            regime = "LONG_OLIGO_EXPONENTIAL_ATTRITION_WARNING";
            guidance = $"Full-length product purity ({cumulativePurityPercent:F1}%) fell below target ({MinTargetFullLengthPurityPercent:F1}%) due to oligo length ({telemetry.OligoLengthNtds} nt). Consider convergent block ligation (split-and-pool) or enzymatic assembly for constructs > 120 nt.";
        }
        else
        {
            regime = "HIGH_FIDELITY_PHOSPHORAMIDITE_SYNTHESIS_STABLE";
            guidance = "Solid-phase oligonucleotide synthesis operating with world-class metrics: ultra-low solvent moisture, excellent stepwise trityl yield, efficient capping, and robust full-length product yield ready for HPLC/PAGE purification.";
        }

        return new OligoPhosphoramiditeAnalysisResult
        {
            SynthesisRunId = telemetry.SynthesisRunId,
            CalculatedStepwiseCouplingYieldPercent = effectiveScy,
            CumulativeFullLengthProductPurityPercent = cumulativePurityPercent,
            EstimatedNMinus1DeletionPurityLossPercent = nMinus1LossPercent,
            MoistureHydrolysisInactivationRiskPercent = moistureHydrolysisRisk,
            IsStepwiseCouplingYieldOptimal = isScyOk,
            IsSolventMoistureWithinSpecification = isMoistureOk,
            IsCappingEfficiencySufficient = isCappingOk,
            IsFullLengthPurityTargetMet = isPurityOk,
            SynthesisIntegrityRegime = regime,
            SynthesisOptimizationGuidance = guidance
        };
    }
}
