namespace SGrowing.Api.Modules.BioTech.PhageAssistedContinuousEvolution;

public sealed record PaceTelemetry(
    string LagoonId,
    double DilutionRatePerHour,
    double PhageReplicationRatePerHour,
    double PhageTiterPfuPerMilliliter,
    double HostCellDensityOd600,
    double HostCellSurvivalRatePercent,
    double MutagenesisInducerConcentrationMicroMolar,
    double SelectionStringencyScore,
    double CumulativeGenerationsElapsed
);

public sealed record PaceAnalysisResult(
    string LagoonId,
    double NetPopulationGrowthPerHour,
    double EstimatedMutationsPerKilobase,
    double EvolutionaryAdaptationVelocityScore,
    string EvolutionRegime,
    string BioreactorActionGuidance,
    bool IsEvolutionOptimal
);

public interface IPhageAssistedContinuousEvolutionEngine
{
    PaceAnalysisResult AnalyzeContinuousEvolution(PaceTelemetry telemetry);
}
