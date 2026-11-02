namespace SGrowing.Api.Modules.Aerospace.ObliqueDetonationWaveEngine;

public sealed record ObliqueDetonationTelemetry(
    string EngineId,
    double FlightMachNumber,
    double CombustorInletTotalPressureKPa,
    double CombustorInletTotalTemperatureKelvin,
    double RampDeflectionAngleDegrees,
    double HydrogenFuelEquivalenceRatio,
    double InductionZoneLengthMillimeters,
    double CombustorLengthMillimeters,
    bool VariableCowlRampActuatorActive
);

public sealed record ObliqueDetonationAnalysisResult(
    string EngineId,
    double ChapmanJouguetMachNumber,
    double DetonationWaveAngleDegrees,
    double CombustorStaticPressureRatio,
    double HeatReleaseEfficiency,
    string CombustionRegime,
    string RampActuationCommand,
    bool IsPropulsionStable
);

public interface IObliqueDetonationWaveEngine
{
    ObliqueDetonationAnalysisResult AnalyzeDetonationWaveState(ObliqueDetonationTelemetry telemetry);
}
