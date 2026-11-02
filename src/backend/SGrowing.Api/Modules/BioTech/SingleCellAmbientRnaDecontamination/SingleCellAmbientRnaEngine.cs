using System;

namespace SGrowing.Api.Modules.BioTech.SingleCellAmbientRnaDecontamination;

public interface ISingleCellAmbientRnaEngine
{
    SingleCellAmbientRnaAnalysisResult AnalyzeAmbientDecontamination(SingleCellAmbientRnaTelemetry telemetry);
}

public sealed class SingleCellAmbientRnaEngine : ISingleCellAmbientRnaEngine
{
    private const double MaxAllowableContaminationFraction = 0.22; // Contamination <= 22%
    private const double MaxAllowableMitoFractionPercent = 15.0; // MT% <= 15%
    private const double MinSignalToBackgroundRatio = 25.0; // Cell UMI / Empty GEM UMI >= 25x

    public SingleCellAmbientRnaAnalysisResult AnalyzeAmbientDecontamination(SingleCellAmbientRnaTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.TotalDropletsAnalyzedCount <= 0 || telemetry.CellularDropletMedianUmiCount <= 0.0)
        {
            throw new ArgumentException("Total droplets and cellular median UMI count must be positive.", nameof(telemetry));
        }

        // 1. Signal to Ambient Background Ratio:
        double emptyUmi = Math.Max(1.0, telemetry.EmptyDropletMeanUmiCount);
        double signalToBackground = telemetry.CellularDropletMedianUmiCount / emptyUmi;
        signalToBackground = Math.Round(signalToBackground, 1);

        // 2. Cellular Purity and Decontaminated Soup Fraction:
        double contamination = Math.Clamp(telemetry.EstimatedAmbientSoupContaminationFraction, 0.0, 1.0);
        double cellularPurity = (1.0 - contamination) * 100.0;
        cellularPurity = Math.Round(cellularPurity, 2);

        double soupPercent = Math.Round(contamination * 100.0, 2);

        // 3. Mitochondrial Stress Flag Ratio (relative to 15% threshold):
        double mitoRatio = telemetry.MitochondrialTranscriptFractionPercent / MaxAllowableMitoFractionPercent;
        mitoRatio = Math.Round(mitoRatio, 2);

        // 4. Validation Checks:
        bool isSoupAcceptable = contamination <= MaxAllowableContaminationFraction && signalToBackground >= MinSignalToBackgroundRatio;
        bool isMitoOk = telemetry.MitochondrialTranscriptFractionPercent <= MaxAllowableMitoFractionPercent;
        bool isDecontaminated = isSoupAcceptable && isMitoOk;

        // 5. Regime & Guidance:
        string regime;
        string guidance;

        if (!isMitoOk)
        {
            regime = "APOPTOTIC_CELL_MEMBRANE_RUPTURE_MITOCHONDRIAL_LEAK";
            guidance = $"Mitochondrial transcript fraction ({telemetry.MitochondrialTranscriptFractionPercent:F1}%) exceeds quality threshold ({MaxAllowableMitoFractionPercent:F1}%). High proportion of dying/lysed cells; apply strict single-cell QC filtering (cutoff MT% < 10%) before clustering.";
        }
        else if (contamination > MaxAllowableContaminationFraction)
        {
            regime = "HIGH_AMBIENT_RNA_SOUP_CROSS_CONTAMINATION";
            guidance = $"Ambient RNA contamination fraction ({soupPercent:F1}%) exceeds tolerance ({MaxAllowableContaminationFraction * 100.0:F1}%). Run generative Poisson-Gamma modeling (CellBender / SoupX) to subtract ambient background profile and eliminate spurious marker cross-talk.";
        }
        else if (signalToBackground < MinSignalToBackgroundRatio)
        {
            regime = "LOW_LIBRARY_COMPLEXITY_AMBIENT_FLOOR_HAZARD";
            guidance = $"Signal-to-background UMI ratio ({signalToBackground:F1}x) is below optimal ({MinSignalToBackgroundRatio:F1}x). Check microfluidic droplet emulsification pressure and improve cell washing centrifugation.";
        }
        else
        {
            regime = "CLEAN_SINGLE_CELL_TRANSCRIPTOME_DECONTAMINATED";
            guidance = "scRNA-seq sample verified with high cellular purity: low ambient soup contamination, intact cell membranes (MT% well within bounds), and high library complexity ready for downstream UMAP/t-SNE clustering.";
        }

        return new SingleCellAmbientRnaAnalysisResult
        {
            SampleRunId = telemetry.SampleRunId,
            CellularPurityScorePercent = cellularPurity,
            DecontaminatedSoupFractionPercent = soupPercent,
            SignalToAmbientBackgroundRatio = signalToBackground,
            MitochondrialStressFlagRatio = mitoRatio,
            IsAmbientSoupContaminationAcceptable = isSoupAcceptable,
            IsMitochondrialQualityPassing = isMitoOk,
            IsSingleCellDatasetDecontaminated = isDecontaminated,
            DecontaminationRegime = regime,
            BioinformaticCurationGuidance = guidance
        };
    }
}
