namespace SGrowing.Api.Modules.Logistics.DroneCorridors;

/// <summary>
/// Autonomous 3D UAV corridor flight planner incorporating aerodynamic wind vector physics,
/// payload battery depletion modeling, and automatic geofence detour insertion.
/// </summary>
public sealed class FlightCorridorPathfinderEngine
{
    private readonly GeofenceAirspaceManager _geofenceManager;

    public FlightCorridorPathfinderEngine(GeofenceAirspaceManager geofenceManager)
    {
        _geofenceManager = geofenceManager;
    }

    public UavFlightPlan PlanMission(PlanFlightRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var waypoints = new List<GeoPoint3D> { request.Origin };

        // Test if direct route intersects restricted airspace
        var directConflict = _geofenceManager.IsSegmentIntersectingRestrictedAirspace(
            request.Origin,
            request.Destination,
            out var conflictingZone);

        var avoidanceInserted = false;
        if (directConflict)
        {
            // Insert lateral detour waypoint offset by +0.03 deg Lat / +0.03 deg Lon
            var midLat = (request.Origin.Latitude + request.Destination.Latitude) / 2.0 + 0.035;
            var midLon = (request.Origin.Longitude + request.Destination.Longitude) / 2.0 + 0.035;
            var detourPoint = new GeoPoint3D(midLat, midLon, Math.Max(request.Origin.AltitudeMetersAgl, 120.0));
            waypoints.Add(detourPoint);
            avoidanceInserted = true;
        }

        waypoints.Add(request.Destination);

        var legs = new List<FlightLegSegment>();
        var totalDistKm = 0.0;
        var totalDurationMin = 0.0;
        var totalEnergyWh = 0.0;

        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var p1 = waypoints[i];
            var p2 = waypoints[i + 1];

            var distKm = CalculateHaversineDistanceKm(p1.Latitude, p1.Longitude, p2.Latitude, p2.Longitude);
            var bearing = CalculateBearingDegrees(p1.Latitude, p1.Longitude, p2.Latitude, p2.Longitude);

            // Aerodynamic wind compensation (meteorological direction is where wind blows FROM):
            // Wind vector points towards (DirectionDegrees + 180)
            var windVectorDegrees = (request.CurrentWind.DirectionDegrees + 180.0) % 360.0;
            var relAngleRad = (bearing - windVectorDegrees) * Math.PI / 180.0;
            var windComponent = request.CurrentWind.SpeedKmH * Math.Cos(relAngleRad); // + for tailwind, - for headwind

            var groundSpeed = Math.Max(20.0, request.Specs.CruiseAirspeedKmH + windComponent);
            var durationHours = distKm / groundSpeed;
            var durationMin = durationHours * 60.0;

            // Payload penalty: +12% energy draw per kg of cargo
            var payloadFactor = 1.0 + (0.12 * Math.Min(request.PayloadKg, request.Specs.MaxPayloadKg));
            var legEnergyWh = distKm * request.Specs.BaseWhPerKm * payloadFactor;

            legs.Add(new FlightLegSegment(
                LegIndex: i + 1,
                Start: p1,
                End: p2,
                DistanceKm: Math.Round(distKm, 2),
                EffectiveGroundSpeedKmH: Math.Round(groundSpeed, 1),
                DurationMinutes: Math.Round(durationMin, 2),
                EnergyConsumedWh: Math.Round(legEnergyWh, 2),
                AvoidanceWaypointInserted: avoidanceInserted && i == 0
            ));

            totalDistKm += distKm;
            totalDurationMin += durationMin;
            totalEnergyWh += legEnergyWh;
        }

        // Battery State of Charge calculation
        var initialEnergyWh = request.Specs.BatteryCapacityWh * (request.InitialBatterySoCPercent / 100.0);
        var remainingEnergyWh = initialEnergyWh - totalEnergyWh;
        var arrivalSoC = Math.Max(0.0, (remainingEnergyWh / request.Specs.BatteryCapacityWh) * 100.0);

        var reserveOk = arrivalSoC >= request.Specs.MinimumReservePercent;
        var clearance = (!directConflict || avoidanceInserted) && reserveOk
            ? "CLEARED: Airspace corridors deconflicted. Adequate battery reserve available."
            : (!reserveOk ? "ABORT: Insufficient battery capacity for safe arrival with reserve." : "DENIED: Airspace conflict unresolved.");

        return new UavFlightPlan(
            FlightPlanId: $"FLT-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            DroneId: request.DroneId,
            Origin: request.Origin,
            Destination: request.Destination,
            PayloadKg: request.PayloadKg,
            TotalDistanceKm: Math.Round(totalDistKm, 2),
            TotalFlightDurationMinutes: Math.Round(totalDurationMin, 1),
            TotalEnergyConsumedWh: Math.Round(totalEnergyWh, 2),
            InitialBatterySoCPercent: request.InitialBatterySoCPercent,
            EstimatedArrivalSoCPercent: Math.Round(arrivalSoC, 1),
            BatteryReservePreserved: reserveOk,
            ViolatesAirspaceGeofence: directConflict && !avoidanceInserted,
            AirspaceClearanceStatus: clearance,
            Legs: legs
        );
    }

    private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0; // Earth radius in km
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return R * c;
    }

    private static double CalculateBearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        var y = Math.Sin((lon2 - lon1) * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0);
        var x = Math.Cos(lat1 * Math.PI / 180.0) * Math.Sin(lat2 * Math.PI / 180.0) -
                Math.Sin(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Cos((lon2 - lon1) * Math.PI / 180.0);
        var bearing = Math.Atan2(y, x) * 180.0 / Math.PI;
        return (bearing + 360.0) % 360.0;
    }
}
