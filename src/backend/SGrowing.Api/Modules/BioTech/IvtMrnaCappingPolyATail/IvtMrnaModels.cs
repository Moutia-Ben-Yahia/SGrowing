namespace SGrowing.Api.Modules.BioTech.IvtMrnaCappingPolyATail;

public sealed record IvtMrnaTelemetry(
    string BatchId,
    double CleanCapIncorporationEfficiencyPercent,
    double DoubleStrandedRnaContaminationPercent,
    double MeanPolyATailLengthNucleotides,
    double PolyATailHeterogeneityIndex,
    double TotalTranscriptLengthNucleotides,
    double PostPurificationYieldPercent,
    double InVitroTranslationEfficiencyScore
);

public sealed record IvtMrnaAnalysisResult(
    string BatchId,
    double InnateImmuneImmunogenicityScore,
    double PredictedCytosolicHalfLifeHours,
    double NetEffectiveTranslationIndex,
    string SynthesisQualityRegime,
    string UpstreamAndDownstreamGuidance,
    bool IsMrnaTherapeuticallyViable
);

public interface IIvtMrnaCappingPolyATailEngine
{
    IvtMrnaAnalysisResult AnalyzeIvtMrnaBatch(IvtMrnaTelemetry telemetry);
}
