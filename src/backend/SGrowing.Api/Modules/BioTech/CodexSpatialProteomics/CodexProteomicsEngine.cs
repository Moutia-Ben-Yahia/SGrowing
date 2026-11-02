namespace SGrowing.Api.Modules.BioTech.CodexSpatialProteomics;

public interface ICodexProteomicsEngine
{
    CodexProteomicsEvaluation AnalyzeCodexRun(CodexProteomicsTelemetry telemetry);
}

public class CodexProteomicsEngine : ICodexProteomicsEngine
{
    public CodexProteomicsEvaluation AnalyzeCodexRun(CodexProteomicsTelemetry telemetry)
    {
        var regimes = new List<string>();
        var mitigations = new List<string>();

        // Dynamic range in dB = -10 * log10(background / signal)
        double carryover = Math.Max(1e-4, telemetry.ResidualBackgroundCarryoverRatio);
        double sbrDb = -10.0 * Math.Log10(carryover);

        // Tissue integrity retention
        double tissueRetention = Math.Max(0.0, 100.0 - telemetry.TissueSectionDetachmentFractionPercent);

        bool isStrippingComplete = telemetry.IterativeReporterStrippingEfficiencyPercent >= 98.0 && carryover <= 0.020;
        bool isTissueAdherent = telemetry.TissueSectionDetachmentFractionPercent <= 2.5;
        bool isSegmentationAccurate = telemetry.SingleCellSpatialSegmentationF1Score >= 0.88;

        if (carryover > 0.020 || telemetry.IterativeReporterStrippingEfficiencyPercent < 98.0)
        {
            regimes.Add("CODEX_REPORTER_CARRYOVER_BACKGROUND_ACCUMULATION");
            mitigations.Add("Increase chemical stripping incubation time with tris(2-carboxyethyl)phosphine (TCEP) reducing agent or enzymatic digestion.");
        }

        if (!isTissueAdherent)
        {
            regimes.Add("TISSUE_MICROENVIRONMENT_EPITHELIAL_DETACHMENT");
            mitigations.Add("Apply poly-L-lysine pre-coating and post-fixation with 4% paraformaldehyde / BS3 crosslinker matrix.");
        }

        if (telemetry.AntibodySignalAttenuationOverRunPercent > 30.0)
        {
            regimes.Add("ANTIBODY_BARCODE_STERIC_HINDRANCE_FADE");
            mitigations.Add("Optimize oligonucleotide linker flexibilities and reduce primary antibody conjugate crowding on cell surface epitopes.");
        }

        bool isValid = isStrippingComplete && isTissueAdherent && isSegmentationAccurate &&
                       telemetry.PhotobleachingDecayConstantSeconds >= 40.0;

        return new CodexProteomicsEvaluation
        {
            TissueRunUid = telemetry.TissueRunUid,
            SignalToBackgroundDynamicRangeDb = Math.Round(sbrDb, 1),
            TissueIntegrityRetentionPercent = Math.Round(tissueRetention, 2),
            IsReporterStrippingComplete = isStrippingComplete,
            IsTissueAdhesionPreserved = isTissueAdherent,
            IsSegmentationQualityAccurate = isSegmentationAccurate,
            IsCodexRunClinicallyValid = isValid,
            ActiveRegimes = regimes,
            RecommendedMitigations = mitigations
        };
    }
}
