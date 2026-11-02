namespace SGrowing.Api.Modules.BioTech.CytidineBaseEditingBystanderMutagenesis;

public sealed record CytidineBaseEditingTelemetry(
    string EditTargetId,
    string BaseEditorArchitecture, // e.g. "YE1-BE4max", "AncBE4max"
    int TargetCytidineProtospacerPosition, // e.g. 5 (canonical window: 4-8)
    int BystanderCytidineCountInWindow, // e.g. 1
    double OnTargetCtoTConversionEfficiencyPercent, // e.g. 74.5%
    double BystanderCtoTConversionEfficiencyPercent, // e.g. 3.2%
    double IndelFormationFrequencyPercent, // e.g. 0.8%
    double TranscriptomeWideRnaOffTargetEventsPerMillion // e.g. 12.0
);

public sealed record CytidineBaseEditingAnalysisResult(
    string EditTargetId,
    double EditingSpecificityRatio,
    double CleanEditingYieldPercent,
    string EditingPrecisionRegime,
    string DeaminaseEngineeringGuidance,
    bool IsTherapeuticEditingGradeAchieved
);

public interface ICytidineBaseEditingBystanderMutagenesisEngine
{
    CytidineBaseEditingAnalysisResult AnalyzeBaseEditing(CytidineBaseEditingTelemetry telemetry);
}
