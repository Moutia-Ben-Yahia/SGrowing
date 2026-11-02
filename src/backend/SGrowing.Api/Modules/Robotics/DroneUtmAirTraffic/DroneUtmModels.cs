namespace SGrowing.Api.Modules.Robotics.DroneUtmAirTraffic;

public record DroneWaypoint4D(
    double Latitude,
    double Longitude,
    double AltitudeMetersAgl,
    DateTimeOffset TimestampUtc);

public record DroneFlightPlan(
    string FlightId,
    string DroneRegistration,
    List<DroneWaypoint4D> Trajectory,
    double CruiseSpeedMps,
    double BatteryStateOfChargePercent);

public record NoFlyGeofenceZone(
    string ZoneId,
    string Name,
    double CenterLat,
    double CenterLon,
    double RadiusMeters,
    double FloorAltitudeMeters,
    double CeilingAltitudeMeters);

public record DroneUtmDeconflictionRequest(
    List<DroneFlightPlan> ActiveFlights,
    List<NoFlyGeofenceZone> ActiveGeofences,
    double HorizontalSeparationMeters = 50.0,
    double VerticalSeparationMeters = 15.0);

public record ConflictDetectionRecord(
    string ConflictId,
    string PrimaryFlightId,
    string ConflictingFlightIdOrGeofence,
    string ConflictType,
    double MinimumDistanceMeters,
    DateTimeOffset EstimatedConflictUtc,
    string TacticalAvoidanceCommand);

public record DroneUtmDeconflictionResult(
    int TotalFlightsAudited,
    int DetectedConflictsCount,
    List<ConflictDetectionRecord> Conflicts,
    bool AirspaceSafe,
    string TrafficManagementAdvisory);
