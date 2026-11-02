namespace SGrowing.Api.Modules.Environmental.ScwoPfasElimination;

public sealed record ScwoTelemetry(
    string ReactorId,
    double ReactorCoreTemperatureCelsius,
    double ReactorOperatingPressureMegaPascals,
    double ResidenceTimeSeconds,
    double DissolvedOxygenStoichiometricRatio,
    double InfluentTotalPfasConcentrationPpm,
    double InorganicSaltPrecipitationPluggingRisk,
    double TranspiringWallWaterFlowRateLitersPerMin
);

public sealed record ScwoAnalysisResult(
    string ReactorId,
    double PfasDefluorinationEfficiencyPercent,
    double ResidualPfasConcentrationPpb,
    double TotalOrganicCarbonDestructionPercent,
    string ScwoDestructionRegime,
    string HydrothermalProcessControlGuidance,
    bool IsDestructionComplete
);

public interface IScwoPfasEliminationEngine
{
    ScwoAnalysisResult AnalyzeScwoProcess(ScwoTelemetry telemetry);
}
