namespace SGrowing.Api.Modules.BioTech.CarTCellExhaustionReprogramming;

public sealed record CarTCellExhaustionTelemetry(
    string CellBatchId,
    double ToxTranscriptionFactorRelativeExpression,
    double Nr4aTranscriptionFactorRelativeExpression,
    double Pd1CheckpointSurfaceDensityMfi,
    double Tim3CheckpointSurfaceDensityMfi,
    double Lag3CheckpointSurfaceDensityMfi,
    double StemCellMemoryFractionPercent, // T_SCM % (e.g. CD45RA+ CD62L+ CCR7+)
    double DegranulationCd107aFractionPercent,
    bool IsToxCrisprKnockoutApplied,
    bool IsCJunOverexpressionApplied
);

public sealed record CarTCellExhaustionAnalysisResult(
    string CellBatchId,
    double CompositeExhaustionScore,
    double FunctionalCytotoxicityIndex,
    double MemoryStemnessRetentionIndex,
    string ExhaustionRegime,
    string TherapeuticReprogrammingGuidance,
    bool IsTherapeuticallyDurable
);

public interface ICarTCellExhaustionReprogrammingEngine
{
    CarTCellExhaustionAnalysisResult AnalyzeCarTExhaustion(CarTCellExhaustionTelemetry telemetry);
}
