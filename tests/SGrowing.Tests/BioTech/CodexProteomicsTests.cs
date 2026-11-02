using SGrowing.Api.Modules.BioTech.CodexSpatialProteomics;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class CodexProteomicsTests
{
    private readonly CodexProteomicsEngine _engine = new();

    [Fact]
    public void AnalyzeCodexRun_NominalTumorTissue_ReturnsClinicallyValid()
    {
        var telemetry = new CodexProteomicsTelemetry
        {
            TissueRunUid = "CODEX-TUMOR-001",
            TissueType = "HumanTumorMicroenvironment_FFPE",
            MultiplexAntibodyPanelSize = 54,
            CompletedImagingCycles = 36,
            IterativeReporterStrippingEfficiencyPercent = 99.1,
            ResidualBackgroundCarryoverRatio = 0.009,
            TissueSectionDetachmentFractionPercent = 1.15,
            PhotobleachingDecayConstantSeconds = 52.0,
            SingleCellSpatialSegmentationF1Score = 0.93,
            AntibodySignalAttenuationOverRunPercent = 18.5
        };

        var result = _engine.AnalyzeCodexRun(telemetry);

        Assert.NotNull(result);
        Assert.Equal("CODEX-TUMOR-001", result.TissueRunUid);
        Assert.True(result.IsReporterStrippingComplete);
        Assert.True(result.IsTissueAdhesionPreserved);
        Assert.True(result.IsSegmentationQualityAccurate);
        Assert.True(result.IsCodexRunClinicallyValid);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.SignalToBackgroundDynamicRangeDb > 20.0);
        Assert.True(result.TissueIntegrityRetentionPercent > 98.0);
    }

    [Fact]
    public void AnalyzeCodexRun_CarryoverBackground_TriggersCarryoverAlert()
    {
        var telemetry = new CodexProteomicsTelemetry
        {
            TissueRunUid = "CODEX-TUMOR-002",
            IterativeReporterStrippingEfficiencyPercent = 96.5,
            ResidualBackgroundCarryoverRatio = 0.035
        };

        var result = _engine.AnalyzeCodexRun(telemetry);

        Assert.False(result.IsReporterStrippingComplete);
        Assert.False(result.IsCodexRunClinicallyValid);
        Assert.Contains("CODEX_REPORTER_CARRYOVER_BACKGROUND_ACCUMULATION", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCodexRun_TissueDetachment_TriggersEpithelialDetachmentAlert()
    {
        var telemetry = new CodexProteomicsTelemetry
        {
            TissueRunUid = "CODEX-TUMOR-003",
            TissueSectionDetachmentFractionPercent = 4.8
        };

        var result = _engine.AnalyzeCodexRun(telemetry);

        Assert.False(result.IsTissueAdhesionPreserved);
        Assert.False(result.IsCodexRunClinicallyValid);
        Assert.Contains("TISSUE_MICROENVIRONMENT_EPITHELIAL_DETACHMENT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCodexRun_SevereAttenuation_TriggersStericHindranceAlert()
    {
        var telemetry = new CodexProteomicsTelemetry
        {
            TissueRunUid = "CODEX-TUMOR-004",
            AntibodySignalAttenuationOverRunPercent = 38.0
        };

        var result = _engine.AnalyzeCodexRun(telemetry);

        Assert.Contains("ANTIBODY_BARCODE_STERIC_HINDRANCE_FADE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeCodexRun_FastPhotobleaching_FailsValidity()
    {
        var telemetry = new CodexProteomicsTelemetry
        {
            TissueRunUid = "CODEX-TUMOR-005",
            PhotobleachingDecayConstantSeconds = 28.0 // < 40 s
        };

        var result = _engine.AnalyzeCodexRun(telemetry);

        Assert.False(result.IsCodexRunClinicallyValid);
    }
}
