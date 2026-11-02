using FluentAssertions;
using SGrowing.Api.Modules.BioTech.CrisprBaseEditorDeaminaseBystander;
using Xunit;

namespace SGrowing.Tests.BioTech;

public sealed class CrisprBaseEditorDeaminaseBystanderTests
{
    private readonly CrisprBaseEditorDeaminaseBystanderEngine _engine = new();

    [Fact]
    public void AnalyzeBaseEditingFidelity_NominalTarget_ReturnsHighFidelityPrecision()
    {
        var telemetry = new CrisprBaseEditorTelemetry(
            EditorTargetId: "TARGET-HBB-BETA-THALASSEMIA",
            BaseEditorType: "ABE",
            TargetProtospacerPosition: 5,
            BystanderBasesCountInWindow: 0,
            OnTargetDeaminationRate: 0.78,
            BystanderDeaminationRate: 0.01,
            RnaOffTargetDeaminationRate: 0.002,
            IndelFrequency: 0.008,
            EngineeredConstrainedDeaminaseVariantUsed: true
        );

        var result = _engine.AnalyzeBaseEditingFidelity(telemetry);

        result.EditingFidelityRegime.Should().Be("HIGH_FIDELITY_PRECISION_WINDOW");
        result.IsClinicallyActionable.Should().BeTrue();
        result.OnToBystanderEditingPurityRatio.Should().BeGreaterThan(10.0);
        result.EditToIndelRatio.Should().BeGreaterThan(50.0);
    }

    [Fact]
    public void AnalyzeBaseEditingFidelity_HighIndelRate_ReturnsGenomicDamageAlert()
    {
        var telemetry = new CrisprBaseEditorTelemetry(
            EditorTargetId: "TARGET-ONCO-P53",
            BaseEditorType: "CBE",
            TargetProtospacerPosition: 6,
            BystanderBasesCountInWindow: 1,
            OnTargetDeaminationRate: 0.65,
            BystanderDeaminationRate: 0.05,
            RnaOffTargetDeaminationRate: 0.005,
            IndelFrequency: 0.085, // High indel rate > 0.045
            EngineeredConstrainedDeaminaseVariantUsed: false
        );

        var result = _engine.AnalyzeBaseEditingFidelity(telemetry);

        result.EditingFidelityRegime.Should().Be("INDEL_GENOMIC_DAMAGE_ALERT");
        result.IsClinicallyActionable.Should().BeFalse();
        result.RecommendedProteinEngineeringAction.Should().Contain("Uracil DNA Glycosylase Inhibitor");
    }

    [Fact]
    public void AnalyzeBaseEditingFidelity_ActiveBystanderEditing_ReturnsBystanderRisk()
    {
        var telemetry = new CrisprBaseEditorTelemetry(
            EditorTargetId: "TARGET-PCSK9-CHOLESTEROL",
            BaseEditorType: "CBE",
            TargetProtospacerPosition: 5,
            BystanderBasesCountInWindow: 2, // 2 bystander C's
            OnTargetDeaminationRate: 0.50,
            BystanderDeaminationRate: 0.25, // purity = 2.0 < 4.0
            RnaOffTargetDeaminationRate: 0.004,
            IndelFrequency: 0.012,
            EngineeredConstrainedDeaminaseVariantUsed: false
        );

        var result = _engine.AnalyzeBaseEditingFidelity(telemetry);

        result.EditingFidelityRegime.Should().Be("BYSTANDER_MUTAGENESIS_RISK");
        result.IsClinicallyActionable.Should().BeFalse();
        result.RecommendedProteinEngineeringAction.Should().Contain("narrow-window engineered deaminase");
    }

    [Fact]
    public void AnalyzeBaseEditingFidelity_HighRnaOffTarget_ReturnsTranscriptomeWarning()
    {
        var telemetry = new CrisprBaseEditorTelemetry(
            EditorTargetId: "TARGET-CFTR-DELTAF508",
            BaseEditorType: "ABE",
            TargetProtospacerPosition: 4,
            BystanderBasesCountInWindow: 0,
            OnTargetDeaminationRate: 0.60,
            BystanderDeaminationRate: 0.02,
            RnaOffTargetDeaminationRate: 0.045, // High RNA off-target > 0.02
            IndelFrequency: 0.009,
            EngineeredConstrainedDeaminaseVariantUsed: false
        );

        var result = _engine.AnalyzeBaseEditingFidelity(telemetry);

        result.EditingFidelityRegime.Should().Be("OFF_TARGET_TRANSCRIPTOME_DEAMINATION_WARNING");
        result.IsClinicallyActionable.Should().BeFalse();
        result.RecommendedProteinEngineeringAction.Should().Contain("SECURE/FE1 mutations");
    }
}
