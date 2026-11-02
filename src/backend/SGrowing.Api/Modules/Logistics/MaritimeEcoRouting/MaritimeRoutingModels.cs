namespace SGrowing.Api.Modules.Logistics.MaritimeEcoRouting;

public sealed record GeoCoordinate(
    double Latitude,
    double Longitude
);

public sealed record AisVesselTelemetry(
    string Mmsi,
    string VesselName,
    GeoCoordinate Position,
    double SpeedKnots,
    double CourseOverGroundDegrees,
    double DeadweightTons
);

public sealed record SeaWeatherCondition(
    double SignificantWaveHeightMeters,
    double OceanCurrentSpeedKnots,
    double OceanCurrentDirectionDegrees,
    double WindSpeedKnots
);

public sealed record CollisionAvoidanceAssessment(
    string TargetMmsi,
    string TargetName,
    double DistanceCpaNauticalMiles,
    double TimeCpaMinutes,
    bool CollisionRiskDetected,
    double RecommendedCourseAdjustmentDegrees,
    string ColregsAdvisory
);

public sealed record EcoVoyagePlan(
    string PlanId,
    string OwnVesselMmsi,
    double RecommendedSpeedKnots,
    double DistanceNauticalMiles,
    double EstimatedFuelBurnMetricTons,
    decimal EstimatedFuelCostUsd,
    double CiiScoreGramsCo2PerTonNm,
    string CiiRatingGrade,
    IReadOnlyList<CollisionAvoidanceAssessment> CollisionRisks,
    DateTime GeneratedAtUtc
);

public sealed record EcoRoutingRequest(
    AisVesselTelemetry OwnVessel,
    GeoCoordinate Destination,
    IReadOnlyList<AisVesselTelemetry> NearbyAisTargets,
    SeaWeatherCondition Weather,
    decimal VlsfoFuelPricePerTonUsd
);
