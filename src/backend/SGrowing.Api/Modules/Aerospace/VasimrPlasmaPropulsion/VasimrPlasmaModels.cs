namespace SGrowing.Api.Modules.Aerospace.VasimrPlasmaPropulsion;

public record VasimrPlasmaTelemetry(
    string SatelliteId,
    double OrbitalAltitudeKm,
    double OrbitalVelocityMPerSec,
    double AtmosphericDensityKgPerM3,
    double SatelliteRamAreaM2,
    double DragCoefficient,
    double AvailableRfPowerWatts,
    double CurrentIspSeconds,
    double PropellantMassRemainingKg);

public record VasimrPlasmaAnalysisResult(
    string SatelliteId,
    double AerodynamicDragForceNewtons,
    double VasimrThrustGeneratedNewtons,
    double NetThrustMarginNewtons,
    bool IsAltitudeLossPrevented,
    string OptimalThrustMode,
    double PropellantMassFlowRateMilligramsPerSec,
    string ThrusterGuidanceAdvisory);
