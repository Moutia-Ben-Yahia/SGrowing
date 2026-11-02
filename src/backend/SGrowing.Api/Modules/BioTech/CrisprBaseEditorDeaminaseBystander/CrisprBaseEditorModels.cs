namespace SGrowing.Api.Modules.BioTech.CrisprBaseEditorDeaminaseBystander;

public sealed record CrisprBaseEditorTelemetry(
    string EditorTargetId,
    string BaseEditorType,
    int TargetProtospacerPosition,
    int BystanderBasesCountInWindow,
    double OnTargetDeaminationRate,
    double BystanderDeaminationRate,
    double RnaOffTargetDeaminationRate,
    double IndelFrequency,
    bool EngineeredConstrainedDeaminaseVariantUsed
);

public sealed record CrisprBaseEditorAnalysisResult(
    string EditorTargetId,
    double OnToBystanderEditingPurityRatio,
    double OnToRnaOffTargetSpecificityRatio,
    double EditToIndelRatio,
    string EditingFidelityRegime,
    string RecommendedProteinEngineeringAction,
    bool IsClinicallyActionable
);

public interface ICrisprBaseEditorDeaminaseBystanderEngine
{
    CrisprBaseEditorAnalysisResult AnalyzeBaseEditingFidelity(CrisprBaseEditorTelemetry telemetry);
}
