namespace SGrowing.Api.Modules.BioTech.AcidMineDrainageBioleaching;

public sealed record BioleachingTelemetry(
    string BioreactorId,
    double SolutionPh,
    double OxidationReductionPotentialMv,
    double FerricToFerrousRatio,
    double DissolvedOxygenMilligramsPerLiter,
    double TemperatureCelsius,
    double BacterialCellDensityCellsPerMl,
    double FeedChalcopyriteGradePercent,
    double SulfateIonConcentrationGramsPerLiter
);

public sealed record BioleachingAnalysisResult(
    string BioreactorId,
    double BioleachingKineticsRateMgPerLiterPerHour,
    double CumulativeCopperExtractionPercent,
    double JarositePrecipitationRiskScore,
    double AcidMineDrainageNeutralizationDemandKgLimePerM3,
    string BioleachingRegime,
    string ProcessControlRecommendation,
    bool IsProcessOptimal
);

public interface IAcidMineDrainageBioleachingEngine
{
    BioleachingAnalysisResult AnalyzeBioleachingProcess(BioleachingTelemetry telemetry);
}
