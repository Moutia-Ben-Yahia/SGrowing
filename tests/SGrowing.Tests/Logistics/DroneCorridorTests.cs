using FluentAssertions;
using SGrowing.Api.Modules.Logistics.DroneCorridors;
using Xunit;

namespace SGrowing.Tests.Logistics;

public sealed class DroneCorridorTests
{
    private readonly GeofenceAirspaceManager _geofenceManager = new();
    private readonly FlightCorridorPathfinderEngine _pathfinder;

    public DroneCorridorTests()
    {
        _pathfinder = new FlightCorridorPathfinderEngine(_geofenceManager);
    }

    private static UavSpecs StandardDroneSpecs => new(
        DroneModel: "HexaRotor-Cargo-Pro-X8",
        MaxPayloadKg: 5.0,
        CruiseAirspeedKmH: 60.0,
        BatteryCapacityWh: 800.0,
        BaseWhPerKm: 25.0,
        MinimumReservePercent: 20.0
    );

    #region Clear Airspace & Aerodynamic Flight Planning Tests

    [Fact]
    public void PlanMission_ClearAirspaceWithTailwind_ReachesDestinationWithHighBatterySoC()
    {
        // Algiers Port to Bab Ezzouar (~14 km East)
        var origin = new GeoPoint3D(36.7538, 3.0588, 100.0);
        var dest = new GeoPoint3D(36.7150, 3.1830, 100.0);

        var req = new PlanFlightRequest(
            DroneId: "UAV-ALGIERS-01",
            Specs: StandardDroneSpecs,
            Origin: origin,
            Destination: dest,
            PayloadKg: 2.5,
            InitialBatterySoCPercent: 100.0,
            CurrentWind: new WindVector(SpeedKmH: 15.0, DirectionDegrees: 270.0) // Westerly tailwind assisting Eastbound flight
        );

        var plan = _pathfinder.PlanMission(req);

        plan.Should().NotBeNull();
        plan.DroneId.Should().Be("UAV-ALGIERS-01");
        plan.TotalDistanceKm.Should().BeInRange(10.0, 20.0);
        plan.BatteryReservePreserved.Should().BeTrue();
        plan.EstimatedArrivalSoCPercent.Should().BeGreaterThan(50.0);
        plan.AirspaceClearanceStatus.Should().StartWith("CLEARED");
        plan.Legs.Should().ContainSingle();
        plan.Legs[0].EffectiveGroundSpeedKmH.Should().BeGreaterThan(StandardDroneSpecs.CruiseAirspeedKmH, "Tailwind must boost groundspeed.");
    }

    [Fact]
    public void PlanMission_HeadwindVersusTailwind_TakesLongerDuration()
    {
        var origin = new GeoPoint3D(36.7538, 3.0588, 100.0);
        var dest = new GeoPoint3D(36.7150, 3.1830, 100.0);

        // Eastbound flight against Easterly headwind (090 deg)
        var headwindReq = new PlanFlightRequest(
            DroneId: "UAV-HEADWIND",
            Specs: StandardDroneSpecs,
            Origin: origin,
            Destination: dest,
            PayloadKg: 2.0,
            InitialBatterySoCPercent: 100.0,
            CurrentWind: new WindVector(SpeedKmH: 25.0, DirectionDegrees: 90.0) // Direct headwind
        );

        var headwindPlan = _pathfinder.PlanMission(headwindReq);

        // Eastbound flight with Westerly tailwind (270 deg)
        var tailwindReq = headwindReq with
        {
            DroneId = "UAV-TAILWIND",
            CurrentWind = new WindVector(SpeedKmH: 25.0, DirectionDegrees: 270.0)
        };

        var tailwindPlan = _pathfinder.PlanMission(tailwindReq);

        headwindPlan.TotalFlightDurationMinutes.Should().BeGreaterThan(tailwindPlan.TotalFlightDurationMinutes);
        headwindPlan.Legs[0].EffectiveGroundSpeedKmH.Should().BeLessThan(tailwindPlan.Legs[0].EffectiveGroundSpeedKmH);
    }

    #endregion

    #region Geofence Conflict & Detour Insertion Tests

    [Fact]
    public void PlanMission_PathIntersectsNoFlyZone_InsertsDetourWaypointAndClearsAirspace()
    {
        // Register Houari Boumediene Airport No-Fly Polygon directly between Origin and Dest
        var airportNoFly = new GeofencePolygon(
            ZoneId: "ZONE-ALG-AIRPORT",
            Name: "Algiers International Airport CTR",
            ZoneType: GeofenceZoneType.NoFlyZoneAirport,
            Vertices:
            [
                new GeoPoint3D(36.6800, 3.1900, 0.0),
                new GeoPoint3D(36.7200, 3.1900, 0.0),
                new GeoPoint3D(36.7200, 3.2500, 0.0),
                new GeoPoint3D(36.6800, 3.2500, 0.0)
            ],
            MinAltitudeMeters: 0.0,
            MaxAltitudeMeters: 500.0
        );
        _geofenceManager.RegisterZone(airportNoFly);

        var origin = new GeoPoint3D(36.7000, 3.1500, 100.0);
        var dest = new GeoPoint3D(36.7000, 3.2800, 100.0); // Line directly crosses airport box

        var req = new PlanFlightRequest(
            DroneId: "UAV-AVOIDANCE-01",
            Specs: StandardDroneSpecs,
            Origin: origin,
            Destination: dest,
            PayloadKg: 1.0,
            InitialBatterySoCPercent: 90.0,
            CurrentWind: new WindVector(0.0, 0.0)
        );

        var plan = _pathfinder.PlanMission(req);

        plan.Should().NotBeNull();
        plan.Legs.Should().HaveCount(2, "Pathfinder must insert avoidance waypoint around no-fly polygon.");
        plan.Legs[0].AvoidanceWaypointInserted.Should().BeTrue();
        plan.ViolatesAirspaceGeofence.Should().BeFalse();
        plan.AirspaceClearanceStatus.Should().StartWith("CLEARED");
    }

    #endregion

    #region Battery Exhaustion Abort Tests

    [Fact]
    public void PlanMission_LowInitialBattery_AbortsMissionForSafety()
    {
        var origin = new GeoPoint3D(36.7538, 3.0588, 100.0);
        var dest = new GeoPoint3D(36.7150, 3.1830, 100.0);

        var req = new PlanFlightRequest(
            DroneId: "UAV-LOW-BATTERY",
            Specs: StandardDroneSpecs, // Min reserve is 20%
            Origin: origin,
            Destination: dest,
            PayloadKg: 4.5,
            InitialBatterySoCPercent: 22.0, // Insufficient battery to complete 14km flight + 20% reserve
            CurrentWind: new WindVector(10.0, 90.0)
        );

        var plan = _pathfinder.PlanMission(req);

        plan.BatteryReservePreserved.Should().BeFalse();
        plan.AirspaceClearanceStatus.Should().StartWith("ABORT");
    }

    #endregion
}
