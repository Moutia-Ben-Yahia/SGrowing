using SGrowing.Api.Modules.BioTech.GuideSeqPrimeEditingOffTarget;
using Xunit;

namespace SGrowing.Tests.BioTech;

public class GuideSeqEditingTests
{
    private readonly GuideSeqEditingEngine _engine = new();

    [Fact]
    public void AnalyzeAssay_NominalPrimeEditor_ReturnsClinicalReady()
    {
        var telemetry = new GuideSeqEditingTelemetry
        {
            AssayExperimentId = "GUIDE-SEQ-PE-001",
            EditorArchitecture = "PEmax_PrimeEditor",
            OnTargetEditingEfficiencyPercent = 74.5,
            OffTargetCleavageFrequencyPercent = 0.045,
            UnintendedIndelFrequencyPercent = 1.15,
            ConfirmedGenomeWideOffTargetLociCount = 1,
            PamProximalMismatchDiscriminationScore = 9.2,
            TranscriptomeRnaDeaminaseOffTargetPercent = 0.025
        };

        var result = _engine.AnalyzeAssay(telemetry);

        Assert.NotNull(result);
        Assert.Equal("GUIDE-SEQ-PE-001", result.AssayExperimentId);
        Assert.True(result.IsOnTargetEfficiencyAcceptable);
        Assert.True(result.IsOffTargetCleavageSuppressed);
        Assert.True(result.IsIndelFormationControlled);
        Assert.True(result.IsGenomeWidePrecisionClinicalReady);
        Assert.Empty(result.ActiveRegimes);
        Assert.True(result.EditToIndelSelectivityRatio >= 50.0);
        Assert.True(result.PrecisionEditingTherapeuticSafetyIndex >= 8.0);
    }

    [Fact]
    public void AnalyzeAssay_ExcessiveOffTargetLoci_TriggersCleavageSurgeAlert()
    {
        var telemetry = new GuideSeqEditingTelemetry
        {
            AssayExperimentId = "GUIDE-SEQ-PE-002",
            ConfirmedGenomeWideOffTargetLociCount = 5,
            OffTargetCleavageFrequencyPercent = 0.18
        };

        var result = _engine.AnalyzeAssay(telemetry);

        Assert.False(result.IsOffTargetCleavageSuppressed);
        Assert.False(result.IsGenomeWidePrecisionClinicalReady);
        Assert.Contains("GENOME_WIDE_OFF_TARGET_CLEAVAGE_SURGE", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeAssay_HighIndelFormation_TriggersDsbIndelAlert()
    {
        var telemetry = new GuideSeqEditingTelemetry
        {
            AssayExperimentId = "GUIDE-SEQ-PE-003",
            UnintendedIndelFrequencyPercent = 3.4
        };

        var result = _engine.AnalyzeAssay(telemetry);

        Assert.False(result.IsIndelFormationControlled);
        Assert.False(result.IsGenomeWidePrecisionClinicalReady);
        Assert.Contains("INDEL_FORMATION_DOUBLE_STRAND_BREAK_ALERT", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeAssay_HighRnaDeamination_TriggersTranscriptomeMutagenesisAlert()
    {
        var telemetry = new GuideSeqEditingTelemetry
        {
            AssayExperimentId = "GUIDE-SEQ-PE-004",
            TranscriptomeRnaDeaminaseOffTargetPercent = 0.095
        };

        var result = _engine.AnalyzeAssay(telemetry);

        Assert.Contains("TRANSCRIPTOME_DEAMINASE_RNA_MUTAGENESIS", result.ActiveRegimes);
    }

    [Fact]
    public void AnalyzeAssay_SuboptimalEfficiency_FailsClinicalReadiness()
    {
        var telemetry = new GuideSeqEditingTelemetry
        {
            AssayExperimentId = "GUIDE-SEQ-PE-005",
            OnTargetEditingEfficiencyPercent = 54.0
        };

        var result = _engine.AnalyzeAssay(telemetry);

        Assert.False(result.IsOnTargetEfficiencyAcceptable);
        Assert.False(result.IsGenomeWidePrecisionClinicalReady);
    }
}
