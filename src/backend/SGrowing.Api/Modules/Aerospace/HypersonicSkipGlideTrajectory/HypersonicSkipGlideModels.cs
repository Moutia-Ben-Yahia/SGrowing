using System;

namespace SGrowing.Api.Modules.Aerospace.HypersonicSkipGlideTrajectory;

public sealed record HypersonicSkipGlideTelemetry(
    string VehicleId,
    double AltitudeKilometers,
    double VelocityMPerSec,
    double FlightPathAngleDegrees,
    double AngleOfAttackDegrees,
    double AtmosphericDensityKgPerM3,
    double NoseRadiusMeters,
    double VehicleMassKg,
    double AerodynamicLiftCoefficient,
    double AerodynamicDragCoefficient,
    double ReferenceAreaM2,
    double TpsNoseCapTemperatureCelsius);

public sealed record HypersonicSkipGlideResult(
    string VehicleId,
    double StagnationHeatFluxMegawattsPerM2,
    double EquilibriumSurfaceTemperatureCelsius,
    double NormalLoadFactorGForce,
    double LiftToDragRatio,
    string SkipTrajectoryRegime,
    string FlightGuidanceRecommendation,
    bool IsThermalStructuralEnvelopeSafe,
    DateTimeOffset Timestamp);
