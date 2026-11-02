namespace SGrowing.Api.Modules.Logistics.DroneCorridors;

public enum GeofenceZoneType
{
    NoFlyZoneAirport,
    NoFlyZoneMilitary,
    HighTensionPowerGrid,
    ApprovedUrbanCorridor
}

public sealed record GeoPoint3D(
    double Latitude,
    double Longitude,
    double AltitudeMetersAgl
);

public sealed record WindVector(
    double SpeedKmH,
    double DirectionDegrees // 0 = North, 90 = East, 180 = South, 270 = West
);

public sealed record GeofencePolygon(
    string ZoneId,
    string Name,
    GeofenceZoneType ZoneType,
    IReadOnlyList<GeoPoint3D> Vertices,
    double MinAltitudeMeters,
    double MaxAltitudeMeters
);

public sealed record UavSpecs(
    string DroneModel,
    double MaxPayloadKg,
    double CruiseAirspeedKmH,
    double BatteryCapacityWh,
    double BaseWhPerKm,
    double MinimumReservePercent = 20.0
);

public sealed record FlightLegSegment(
    int LegIndex,
    GeoPoint3D Start,
    GeoPoint3D End,
    double DistanceKm,
    double EffectiveGroundSpeedKmH,
    double DurationMinutes,
    double EnergyConsumedWh,
    bool AvoidanceWaypointInserted
);

public sealed record UavFlightPlan(
    string FlightPlanId,
    string DroneId,
    GeoPoint3D Origin,
    GeoPoint3D Destination,
    double PayloadKg,
    double TotalDistanceKm,
    double TotalFlightDurationMinutes,
    double TotalEnergyConsumedWh,
    double InitialBatterySoCPercent,
    double EstimatedArrivalSoCPercent,
    bool BatteryReservePreserved,
    bool ViolatesAirspaceGeofence,
    string AirspaceClearanceStatus,
    IReadOnlyList<FlightLegSegment> Legs
);

public sealed record PlanFlightRequest(
    string DroneId,
    UavSpecs Specs,
    GeoPoint3D Origin,
    GeoPoint3D Destination,
    double PayloadKg,
    double InitialBatterySoCPercent,
    WindVector CurrentWind
);
