namespace SGrowing.Api.Modules.Aerospace.AerogravityAssistTrajectory;

public sealed record AerogravityAssistTelemetry(
    string SpacecraftId,
    string TargetPlanet,
    double AtmosphericEntryVelocityKmPerSec,
    double PeriapsisAltitudeKilometers,
    double WaveriderLiftToDragRatio,
    double InvertedBankAngleDegrees,
    double DynamicPressureKiloPascals,
    double StagnationHeatFluxMegawattsPerSquareMeter,
    double SpacecraftMassKilograms
);

public sealed record AerogravityAssistAnalysisResult(
    string SpacecraftId,
    double TrajectoryTurningAngleDegrees,
    double DeltaVHyperbolicEquivalentKmPerSec,
    double CentrifugalForceToGravityRatio,
    string FlightRegime,
    string WaveriderFlightGuidance,
    bool IsTrajectoryNominal
);

public interface IAerogravityAssistTrajectoryEngine
{
    AerogravityAssistAnalysisResult AnalyzeAerogravityAssist(AerogravityAssistTelemetry telemetry);
}
