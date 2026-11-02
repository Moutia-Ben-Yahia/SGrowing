using System;

namespace SGrowing.Api.Modules.Energy.Sco2BraytonCompressorSurge;

public sealed record Sco2CompressorTelemetry(
    string CycleId,
    double OperatingMassFlowRateKgPerSec,
    double SurgeMassFlowRateKgPerSec,
    double InletPressureMpa,
    double InletTemperatureCelsius,
    double RotorTipSpeedMPerSec,
    double NearCriticalAcousticVelocityMPerSec,
    double PlenumVolumeM3,
    double InletDuctAreaM2,
    double EquivalentDuctLengthMeters,
    double CurrentBypassValveOpeningPercent);

public sealed record Sco2CompressorAnalysisResult(
    string CycleId,
    double CompressorSurgeMarginPercent,
    double GreitzerBParameter,
    double RequiredBypassFlowKgPerSec,
    double RecommendedAntiSurgeValveOpeningPercent,
    string CompressorOperatingRegime,
    string RecommendedControlAction,
    bool IsSurgeImminent,
    DateTimeOffset Timestamp);
