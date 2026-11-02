using System;

namespace SGrowing.Api.Modules.Aerospace.MartianAerocaptureGuidance;

public sealed record MartianAerocaptureTelemetry(
    string SpacecraftId,
    double CurrentAltitudeKm,
    double InertialVelocityMPerSec,
    double FlightPathAngleDegrees,
    double CurrentBankAngleDegrees,
    double SpacecraftMassKg,
    double AerodynamicReferenceAreaM2,
    double DragCoefficientCd,
    double LiftToDragRatio,
    double TargetApoapsisAltitudeKm,
    double AtmosphericDustFactor,
    double MeasuredDecelerationG);

public sealed record MartianAerocaptureAnalysisResult(
    string SpacecraftId,
    double BallisticCoefficientKgPerM2,
    double ProjectedExitVelocityMPerSec,
    double ProjectedApoapsisAltitudeKm,
    string AerocaptureCorridorStatus,
    string BankAngleGuidanceCommand,
    bool IsOrbitCaptureGuaranteed,
    DateTimeOffset Timestamp);
