namespace SGrowing.Api.Modules.BioTech.EpigeneticMethylationClock;

public sealed record EpigeneticClockTelemetry(
    string SampleId,
    double ChronologicalAgeYears,
    double MeanCpGIslandMethylationBeta,
    double TumorSuppressorPromoterHypermethylationPercent,
    double Line1RetrotransposonMethylationPercent,
    double HistoneAcetylationH3K27acRatio,
    double HdacEnzymeActivityUnits,
    int AnalyzedSingleCellCount
);

public sealed record EpigeneticClockAnalysisResult(
    string SampleId,
    double BiologicalEpigeneticAgeYears,
    double EpigeneticAgeAccelerationYears,
    double GenomicStabilityIndex,
    string EpigeneticRegime,
    string TherapeuticEpigeneticGuidance,
    bool IsEpigeneticProfileOptimal
);

public interface IEpigeneticMethylationClockEngine
{
    EpigeneticClockAnalysisResult AnalyzeEpigeneticClock(EpigeneticClockTelemetry telemetry);
}
