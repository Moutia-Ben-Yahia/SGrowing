using System;

namespace SGrowing.Api.Modules.Aerospace.LunarRegolithMicrowaveSintering;

public sealed record LunarRegolithSinteringTelemetry(
    string BatchBrickId,
    double MicrowaveFrequencyGhz,
    double MagnetronIncidentPowerWatts,
    double CavityElectricFieldVPerM,
    double RegolithCoreTemperatureCelsius,
    double HeatingRateCelsiusPerMin,
    double RelativeDielectricPermittivity,
    double LossTangent,
    double InitialBulkDensityGPerCm3,
    double SinteringDurationMinutes);

public sealed record LunarRegolithSinteringResult(
    string BatchBrickId,
    double DissipatedPowerDensityWattsPerM3,
    double FinalSinteredDensityGPerCm3,
    double ResidualPorosityPercent,
    double CompressiveStrengthMpa,
    string SinteringQualityRegime,
    string ThermalControlRecommendation,
    bool IsStructuralGrade,
    DateTimeOffset Timestamp);
