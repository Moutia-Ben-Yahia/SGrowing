namespace SGrowing.Api.Modules.Aerospace.HypersonicAerocaptureOrbitalInsertion;

public sealed record HypersonicAerocaptureTelemetry(
    string SpacecraftId,
    string TargetBody, // e.g. "Mars", "Titan", "Venus"
    double AtmosphericEntryVelocityKilometersPerSec, // v_entry: e.g. 6.2 km/s
    double EntryFlightPathAngleDegrees, // gamma: e.g. -11.5 deg
    double NominalCorridorCenterDegrees, // gamma_center: e.g. -11.4 deg
    double CorridorWidthDegrees, // Delta gamma: e.g. 1.2 deg
    double HypersonicLiftToDragRatio, // L/D: e.g. 0.32
    double PeakDecelerationEarthGs, // e.g. 5.8 g
    double PeakStagnationHeatFluxWattsPerSquareCm, // q_dot: e.g. 145.0 W/cm^2
    double TpsHeatFluxLimitWattsPerSquareCm // e.g. 250.0 W/cm^2
);

public sealed record HypersonicAerocaptureAnalysisResult(
    string SpacecraftId,
    double CorridorDeviationDegrees,
    double EquivalentDeltaVSavedMetersPerSecond,
    double TpsThermalMarginPercent,
    string AerocaptureRegime,
    string TrajectoryGuidance,
    bool IsOrbitalInsertionSafe
);

public interface IHypersonicAerocaptureOrbitalInsertionEngine
{
    HypersonicAerocaptureAnalysisResult AnalyzeAerocaptureTrajectory(HypersonicAerocaptureTelemetry telemetry);
}
