namespace SGrowing.Api.Modules.Marine.DeepSeaBuoyancyMesh;

public record SubmersibleTelemetry(
    string VehicleId,
    double CurrentDepthMeters,
    double VerticalVelocityMps, // positive = ascending, negative = descending
    double SubmersibleDryMassKg,
    double SyntacticFoamVolumeM3,
    double CurrentVbeDisplacementLiters,
    double WaterTemperatureCelsius
);

public record DeepSeaBuoyancyRequest(
    SubmersibleTelemetry Telemetry,
    double TargetVerticalVelocityMps = 0.0,
    double UsblAcousticRangeMeters = 5000.0
);

public record BuoyancyControlPlan(
    string VehicleId,
    double HydrostaticPressureBar,
    double InSituSeawaterDensityKgPerM3,
    double NetBuoyancyForceNewtons,
    double RecommendedVbeAdjustmentLiters,
    string BuoyancyTrimState, // "NEUTRAL_HOVER", "CONTROLLED_DESCENT", "EMERGENCY_ASCENT_VENT"
    string ThrusterCommand,
    double EstimatedTimeToTargetDepthMinutes
);
