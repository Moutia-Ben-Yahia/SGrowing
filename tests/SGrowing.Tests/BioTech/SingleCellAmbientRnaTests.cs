using System;
using SGrowing.Api.Modules.BioTech.SingleCellAmbientRnaDecontamination;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class SingleCellAmbientRnaTests
{
    private readonly SingleCellAmbientRnaEngine _engine = new();

    [Fact]
    public void AnalyzeAmbientDecontamination_CleanSample_ReturnsDecontaminatedRegime()
    {
        var telemetry = new SingleCellAmbientRnaTelemetry
        {
            SampleRunId = "SCRNA-PBMC-RUN-01",
            TissueType = "PBMC",
            TotalDropletsAnalyzedCount = 10000,
            EstimatedAmbientSoupContaminationFraction = 0.08, // 8% soup
            EmptyDropletMeanUmiCount = 30.0,
            CellularDropletMedianUmiCount = 4500.0, // 150x S/B
            MitochondrialTranscriptFractionPercent = 4.5, // < 15%
            HemoglobinContaminationFractionPercent = 1.2
        };

        var result = _engine.AnalyzeAmbientDecontamination(telemetry);

        Assert.Equal("SCRNA-PBMC-RUN-01", result.SampleRunId);
        Assert.True(result.IsAmbientSoupContaminationAcceptable);
        Assert.True(result.IsMitochondrialQualityPassing);
        Assert.True(result.IsSingleCellDatasetDecontaminated);
        Assert.True(result.CellularPurityScorePercent >= 90.0);
        Assert.True(result.SignalToAmbientBackgroundRatio >= 25.0);
        Assert.Equal("CLEAN_SINGLE_CELL_TRANSCRIPTOME_DECONTAMINATED", result.DecontaminationRegime);
    }

    [Fact]
    public void AnalyzeAmbientDecontamination_HighMitoFraction_TriggersApoptoticRupture()
    {
        var telemetry = new SingleCellAmbientRnaTelemetry
        {
            SampleRunId = "SCRNA-DYING-CELLS-02",
            TissueType = "Dissociated-Tumor",
            TotalDropletsAnalyzedCount = 5000,
            EstimatedAmbientSoupContaminationFraction = 0.10,
            EmptyDropletMeanUmiCount = 40.0,
            CellularDropletMedianUmiCount = 3000.0,
            MitochondrialTranscriptFractionPercent = 22.5 // High MT% > 15%
        };

        var result = _engine.AnalyzeAmbientDecontamination(telemetry);

        Assert.False(result.IsMitochondrialQualityPassing);
        Assert.False(result.IsSingleCellDatasetDecontaminated);
        Assert.Equal("APOPTOTIC_CELL_MEMBRANE_RUPTURE_MITOCHONDRIAL_LEAK", result.DecontaminationRegime);
        Assert.Contains("exceeds quality threshold", result.BioinformaticCurationGuidance);
    }

    [Fact]
    public void AnalyzeAmbientDecontamination_HeavySoupContamination_TriggersHighAmbientSoupAlert()
    {
        var telemetry = new SingleCellAmbientRnaTelemetry
        {
            SampleRunId = "SCRNA-SOUP-03",
            TissueType = "Brain-Cortex",
            TotalDropletsAnalyzedCount = 7500,
            EstimatedAmbientSoupContaminationFraction = 0.32, // Heavy soup > 22%
            EmptyDropletMeanUmiCount = 80.0,
            CellularDropletMedianUmiCount = 2800.0,
            MitochondrialTranscriptFractionPercent = 6.0
        };

        var result = _engine.AnalyzeAmbientDecontamination(telemetry);

        Assert.False(result.IsAmbientSoupContaminationAcceptable);
        Assert.Equal("HIGH_AMBIENT_RNA_SOUP_CROSS_CONTAMINATION", result.DecontaminationRegime);
        Assert.Contains("Poisson-Gamma modeling", result.BioinformaticCurationGuidance);
    }

    [Fact]
    public void AnalyzeAmbientDecontamination_LowSignalToBackground_TriggersComplexityFloorHazard()
    {
        var telemetry = new SingleCellAmbientRnaTelemetry
        {
            SampleRunId = "SCRNA-LOW-SB-04",
            TissueType = "Stem-Cell-Culture",
            TotalDropletsAnalyzedCount = 4000,
            EstimatedAmbientSoupContaminationFraction = 0.15,
            EmptyDropletMeanUmiCount = 120.0, // High empty GEM floor
            CellularDropletMedianUmiCount = 1800.0, // S/B = 15x < 25x
            MitochondrialTranscriptFractionPercent = 5.0
        };

        var result = _engine.AnalyzeAmbientDecontamination(telemetry);

        Assert.False(result.IsAmbientSoupContaminationAcceptable);
        Assert.Equal("LOW_LIBRARY_COMPLEXITY_AMBIENT_FLOOR_HAZARD", result.DecontaminationRegime);
        Assert.Contains("Signal-to-background UMI ratio", result.BioinformaticCurationGuidance);
    }

    [Fact]
    public void AnalyzeAmbientDecontamination_ZeroOrNegativeDroplets_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeAmbientDecontamination(null!));

        var invalidTelemetry = new SingleCellAmbientRnaTelemetry
        {
            SampleRunId = "INV-SAMPLE",
            TotalDropletsAnalyzedCount = 0,
            CellularDropletMedianUmiCount = 2000.0
        };

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeAmbientDecontamination(invalidTelemetry));
    }
}
