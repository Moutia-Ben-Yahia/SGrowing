using System;
using SGrowing.Api.Modules.BioTech.CytidineBaseEditingBystanderMutagenesis;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class CytidineBaseEditingBystanderMutagenesisTests
{
    private readonly CytidineBaseEditingBystanderMutagenesisEngine _engine = new();

    [Fact]
    public void AnalyzeBaseEditing_NullTelemetry_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.AnalyzeBaseEditing(null!));
    }

    [Fact]
    public void AnalyzeBaseEditing_InvalidProtospacerPosition_ThrowsArgumentException()
    {
        var telemetry = new CytidineBaseEditingTelemetry(
            EditTargetId: "PCSK9-EXON-1",
            BaseEditorArchitecture: "BE4max",
            TargetCytidineProtospacerPosition: 25, // Outside protospacer 1-20!
            BystanderCytidineCountInWindow: 1,
            OnTargetCtoTConversionEfficiencyPercent: 65.0,
            BystanderCtoTConversionEfficiencyPercent: 5.0,
            IndelFormationFrequencyPercent: 1.0,
            TranscriptomeWideRnaOffTargetEventsPerMillion: 10.0
        );

        Assert.Throws<ArgumentException>(() => _engine.AnalyzeBaseEditing(telemetry));
    }

    [Fact]
    public void AnalyzeBaseEditing_HighPrecisionCbe_ReturnsCleanBaseEditingRegime()
    {
        var telemetry = new CytidineBaseEditingTelemetry(
            EditTargetId: "HBB-SICKLE-CORRECTION",
            BaseEditorArchitecture: "YE1-BE4max",
            TargetCytidineProtospacerPosition: 5,
            BystanderCytidineCountInWindow: 1,
            OnTargetCtoTConversionEfficiencyPercent: 78.0,
            BystanderCtoTConversionEfficiencyPercent: 2.8, // Low bystander
            IndelFormationFrequencyPercent: 0.6,
            TranscriptomeWideRnaOffTargetEventsPerMillion: 8.5
        );

        var result = _engine.AnalyzeBaseEditing(telemetry);

        Assert.NotNull(result);
        Assert.Equal("HBB-SICKLE-CORRECTION", result.EditTargetId);
        Assert.True(result.EditingSpecificityRatio >= 15.0);
        Assert.True(result.IsTherapeuticEditingGradeAchieved);
        Assert.Equal("HIGH_PRECISION_CLEAN_BASE_EDITING_REGIME", result.EditingPrecisionRegime);
    }

    [Fact]
    public void AnalyzeBaseEditing_SevereBystander_ReturnsBystanderHazard()
    {
        var telemetry = new CytidineBaseEditingTelemetry(
            EditTargetId: "TP53-MUTATION-HOTSPOT",
            BaseEditorArchitecture: "rAPOBEC1-BE3",
            TargetCytidineProtospacerPosition: 6,
            BystanderCytidineCountInWindow: 2,
            OnTargetCtoTConversionEfficiencyPercent: 60.0,
            BystanderCtoTConversionEfficiencyPercent: 38.0, // High bystander!
            IndelFormationFrequencyPercent: 2.0,
            TranscriptomeWideRnaOffTargetEventsPerMillion: 45.0
        );

        var result = _engine.AnalyzeBaseEditing(telemetry);

        Assert.NotNull(result);
        Assert.Equal("SEVERE_BYSTANDER_MUTAGENESIS_HAZARD", result.EditingPrecisionRegime);
        Assert.False(result.IsTherapeuticEditingGradeAchieved);
        Assert.Contains("Switch deaminase architecture", result.DeaminaseEngineeringGuidance);
    }

    [Fact]
    public void AnalyzeBaseEditing_HighIndels_ReturnsUngExcisionWarning()
    {
        var telemetry = new CytidineBaseEditingTelemetry(
            EditTargetId: "DMD-EXON-51",
            BaseEditorArchitecture: "BE2-NO-UGI",
            TargetCytidineProtospacerPosition: 5,
            BystanderCytidineCountInWindow: 0,
            OnTargetCtoTConversionEfficiencyPercent: 40.0,
            BystanderCtoTConversionEfficiencyPercent: 0.0,
            IndelFormationFrequencyPercent: 9.5, // High indels!
            TranscriptomeWideRnaOffTargetEventsPerMillion: 15.0
        );

        var result = _engine.AnalyzeBaseEditing(telemetry);

        Assert.NotNull(result);
        Assert.Equal("ELEVATED_INDEL_UNG_EXCISION_BREAK_WARNING", result.EditingPrecisionRegime);
        Assert.False(result.IsTherapeuticEditingGradeAchieved);
    }
}
