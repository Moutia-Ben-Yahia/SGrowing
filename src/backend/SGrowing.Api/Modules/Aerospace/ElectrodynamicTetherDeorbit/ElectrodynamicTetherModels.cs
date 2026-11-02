namespace SGrowing.Api.Modules.Aerospace.ElectrodynamicTetherDeorbit;

public sealed record ElectrodynamicTetherTelemetry(
    string SatelliteId,
    double OrbitalAltitudeKilometers,
    double OrbitalVelocityKmPerSec,
    double GeomagneticFieldMicroTesla,
    double TetherLengthKilometers,
    double TetherInducedCurrentAmperes,
    double IonosphericPlasmaDensityPerCubicCm,
    double TetherLibrationalSwingAngleDegrees,
    double TetherTensionNewtons
);

public sealed record ElectrodynamicTetherAnalysisResult(
    string SatelliteId,
    double InducedMotionalEmfVolts,
    double LorentzDecelerationDragNewtons,
    double ProjectedAltitudeLossKmPerDay,
    string DeorbitRegime,
    string ElectrodynamicControlGuidance,
    bool IsDeorbitingOptimal
);

public interface IElectrodynamicTetherDeorbitEngine
{
    ElectrodynamicTetherAnalysisResult AnalyzeElectrodynamicTether(ElectrodynamicTetherTelemetry telemetry);
}
