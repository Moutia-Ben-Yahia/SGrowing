using System;

namespace SGrowing.Api.Modules.Environmental.DacSolidAmineChemisorption;

public sealed record DacContactorTelemetry(
    string ContactorModuleId,
    double AmbientCo2Ppm,
    double AmbientRelativeHumidityPercent,
    double AmbientTemperatureCelsius,
    double AirFlowRateM3PerSec,
    double AdsorbentBedMassKg,
    double CurrentCo2LoadingMmolPerG,
    double CurrentWaterLoadingMmolPerG,
    double ContactorPressureDropKpa,
    double AdsorptionCycleDurationMinutes);

public sealed record DacContactorAnalysisResult(
    string ContactorModuleId,
    double BedSaturationFractionPercent,
    double WaterToCo2LoadingRatio,
    double EstimatedDesorptionEnergyGjPerTonCo2,
    double InstantaneousCaptureRateKgPerHour,
    string ContactorOperatingState,
    string CycleControlRecommendation,
    bool IsDesorptionRequired,
    DateTimeOffset Timestamp);
