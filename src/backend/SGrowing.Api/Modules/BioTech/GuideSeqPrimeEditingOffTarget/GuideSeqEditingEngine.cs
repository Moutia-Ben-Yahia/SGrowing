namespace SGrowing.Api.Modules.BioTech.GuideSeqPrimeEditingOffTarget;

public interface IGuideSeqEditingEngine
{
    GuideSeqEditingEvaluation AnalyzeAssay(GuideSeqEditingTelemetry telemetry);
}

public class GuideSeqEditingEngine : IGuideSeqEditingEngine
{
    public GuideSeqEditingEvaluation AnalyzeAssay(GuideSeqEditingTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Edit to indel ratio
        double indel = Math.Max(0.01, telemetry.UnintendedIndelFrequencyPercent);
        double editToIndelRatio = telemetry.OnTargetEditingEfficiencyPercent / indel;

        // Therapeutic safety index (0 to 10)
        double safetyIndex = 10.0;
        if (telemetry.ConfirmedGenomeWideOffTargetLociCount > 2)
        {
            safetyIndex -= (telemetry.ConfirmedGenomeWideOffTargetLociCount - 2) * 2.5;
        }
        if (telemetry.OffTargetCleavageFrequencyPercent > 0.08)
        {
            safetyIndex -= (telemetry.OffTargetCleavageFrequencyPercent - 0.08) * 45.0;
        }
        if (telemetry.TranscriptomeRnaDeaminaseOffTargetPercent > 0.04)
        {
            safetyIndex -= (telemetry.TranscriptomeRnaDeaminaseOffTargetPercent - 0.04) * 60.0;
        }
        safetyIndex = Math.Clamp(safetyIndex, 0.5, 10.0);

        bool isOnTargetOk = telemetry.OnTargetEditingEfficiencyPercent >= 65.0;
        bool isOffTargetSuppressed = telemetry.OffTargetCleavageFrequencyPercent <= 0.08 && telemetry.ConfirmedGenomeWideOffTargetLociCount <= 2;
        bool isIndelControlled = telemetry.UnintendedIndelFrequencyPercent <= 1.8 && editToIndelRatio >= 30.0;

        if (telemetry.ConfirmedGenomeWideOffTargetLociCount > 2 || telemetry.OffTargetCleavageFrequencyPercent > 0.12)
        {
            regimes.Add("GENOME_WIDE_OFF_TARGET_CLEAVAGE_SURGE");
            mitigations.Add("Engineer high-fidelity Cas9 variant (e.g. HiFi-Cas9 / SuperFi-Cas9) with mutated contact residues to sharpen R-loop proofreading.");
        }

        if (telemetry.UnintendedIndelFrequencyPercent > 2.0)
        {
            regimes.Add("INDEL_FORMATION_DOUBLE_STRAND_BREAK_ALERT");
            mitigations.Add("Switch Cas9 nickase nick orientation or optimize pegRNA reverse-transcriptase template (RTT) length to avoid double-strand cut repair by NHEJ.");
        }

        if (telemetry.TranscriptomeRnaDeaminaseOffTargetPercent > 0.06)
        {
            regimes.Add("TRANSCRIPTOME_DEAMINASE_RNA_MUTAGENESIS");
            mitigations.Add("Incorporate deaminase domain mutations (e.g. TadA-8e V106W or SECURE mutations) to abolish non-specific cellular RNA deamination.");
        }

        bool isClinicalReady = isOnTargetOk && isOffTargetSuppressed && isIndelControlled && safetyIndex >= 8.0;

        return new GuideSeqEditingEvaluation
        {
            AssayExperimentId = telemetry.AssayExperimentId,
            EditToIndelSelectivityRatio = Math.Round(editToIndelRatio, 1),
            PrecisionEditingTherapeuticSafetyIndex = Math.Round(safetyIndex, 2),
            IsOnTargetEfficiencyAcceptable = isOnTargetOk,
            IsOffTargetCleavageSuppressed = isOffTargetSuppressed,
            IsIndelFormationControlled = isIndelControlled,
            IsGenomeWidePrecisionClinicalReady = isClinicalReady,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
