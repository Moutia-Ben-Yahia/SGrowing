namespace SGrowing.Api.Modules.Robotics.DroneUtmAirTraffic;

public class DroneUtmAirTrafficEngine
{
    private const double EarthRadiusMeters = 6_371_000.0;

    public DroneUtmDeconflictionResult DeconflictAirspace(DroneUtmDeconflictionRequest request)
    {
        var conflicts = new List<ConflictDetectionRecord>();
        int conflictIndex = 1;

        // 1. Battery critical checks
        foreach (var flight in request.ActiveFlights)
        {
            if (flight.BatteryStateOfChargePercent < 20.0)
            {
                var wp = flight.Trajectory.FirstOrDefault();
                conflicts.Add(new ConflictDetectionRecord(
                    ConflictId: $"CONF-BATT-{conflictIndex++:D3}",
                    PrimaryFlightId: flight.FlightId,
                    ConflictingFlightIdOrGeofence: "LowBatteryThreshold",
                    ConflictType: "BatteryEmergency",
                    MinimumDistanceMeters: 0.0,
                    EstimatedConflictUtc: wp?.TimestampUtc ?? DateTimeOffset.UtcNow,
                    TacticalAvoidanceCommand: "Emergency: Divert to nearest rooftop vertipad landing spot immediately."
                ));
            }
        }

        // 2. Geofence breach checks
        foreach (var flight in request.ActiveFlights)
        {
            foreach (var wp in flight.Trajectory)
            {
                foreach (var gf in request.ActiveGeofences)
                {
                    double dist = CalculateHaversineDistanceMeters(wp.Latitude, wp.Longitude, gf.CenterLat, gf.CenterLon);
                    bool insideRadius = dist <= gf.RadiusMeters;
                    bool insideAlt = wp.AltitudeMetersAgl >= gf.FloorAltitudeMeters && wp.AltitudeMetersAgl <= gf.CeilingAltitudeMeters;

                    if (insideRadius && insideAlt)
                    {
                        conflicts.Add(new ConflictDetectionRecord(
                            ConflictId: $"CONF-GEO-{conflictIndex++:D3}",
                            PrimaryFlightId: flight.FlightId,
                            ConflictingFlightIdOrGeofence: gf.ZoneId,
                            ConflictType: "GeofenceBreach",
                            MinimumDistanceMeters: Math.Round(dist, 1),
                            EstimatedConflictUtc: wp.TimestampUtc,
                            TacticalAvoidanceCommand: $"Geofence violation ({gf.Name}): Divert trajectory 100m west of perimeter."
                        ));
                        break;
                    }
                }
            }
        }

        // 3. Air-to-Air pairwise 4D separation loss checks
        var flights = request.ActiveFlights;
        for (int i = 0; i < flights.Count; i++)
        {
            for (int j = i + 1; j < flights.Count; j++)
            {
                var fA = flights[i];
                var fB = flights[j];

                foreach (var wpA in fA.Trajectory)
                {
                    foreach (var wpB in fB.Trajectory)
                    {
                        // Overlapping time window (< 10 seconds)
                        if (Math.Abs((wpA.TimestampUtc - wpB.TimestampUtc).TotalSeconds) <= 10.0)
                        {
                            double horizDist = CalculateHaversineDistanceMeters(wpA.Latitude, wpA.Longitude, wpB.Latitude, wpB.Longitude);
                            double vertDist = Math.Abs(wpA.AltitudeMetersAgl - wpB.AltitudeMetersAgl);

                            if (horizDist < request.HorizontalSeparationMeters && vertDist < request.VerticalSeparationMeters)
                            {
                                conflicts.Add(new ConflictDetectionRecord(
                                    ConflictId: $"CONF-A2A-{conflictIndex++:D3}",
                                    PrimaryFlightId: fA.FlightId,
                                    ConflictingFlightIdOrGeofence: fB.FlightId,
                                    ConflictType: "AirToAirSeparationLoss",
                                    MinimumDistanceMeters: Math.Round(horizDist, 1),
                                    EstimatedConflictUtc: wpA.TimestampUtc,
                                    TacticalAvoidanceCommand: $"{fA.FlightId} climb +20m altitude; {fB.FlightId} reduce speed to 5 m/s."
                                ));
                                break;
                            }
                        }
                    }
                }
            }
        }

        bool safe = conflicts.Count == 0;
        string advisory = safe
            ? "Airspace Clear: All micro-delivery drones maintain 4D trajectory separation and geofence clearance."
            : $"Tactical Deconfliction Active: {conflicts.Count} collision/geofence alerts require immediate command execution.";

        return new DroneUtmDeconflictionResult(
            TotalFlightsAudited: request.ActiveFlights.Count,
            DetectedConflictsCount: conflicts.Count,
            Conflicts: conflicts,
            AirspaceSafe: safe,
            TrafficManagementAdvisory: advisory
        );
    }

    private static double CalculateHaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusMeters * c;
    }
}
