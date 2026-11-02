namespace SGrowing.Tests.Robotics;

using FluentAssertions;
using SGrowing.Api.Modules.Robotics.DroneUtmAirTraffic;
using Xunit;

public class DroneUtmAirTrafficTests
{
    private readonly DroneUtmAirTrafficEngine _engine = new();

    [Fact]
    public void DeconflictAirspace_PairwiseSeparationLoss_DetectsAirToAirConflictAndIssuesCommands()
    {
        var now = DateTimeOffset.UtcNow;
        // Two drones converging on same 4D point (within 10m horizontally, same 60m altitude, same second)
        var flightA = new DroneFlightPlan("DRONE-FLIGHT-A", "REG-FR-001",
            [new DroneWaypoint4D(48.8584, 2.2945, 60.0, now)],
            CruiseSpeedMps: 12.0, BatteryStateOfChargePercent: 85.0);

        var flightB = new DroneFlightPlan("DRONE-FLIGHT-B", "REG-FR-002",
            [new DroneWaypoint4D(48.85841, 2.29451, 62.0, now.AddSeconds(1))],
            CruiseSpeedMps: 10.0, BatteryStateOfChargePercent: 78.0);

        var request = new DroneUtmDeconflictionRequest([flightA, flightB], []);
        var result = _engine.DeconflictAirspace(request);

        result.AirspaceSafe.Should().BeFalse();
        result.DetectedConflictsCount.Should().Be(1);

        var conflict = result.Conflicts.Should().ContainSingle().Subject;
        conflict.ConflictType.Should().Be("AirToAirSeparationLoss");
        conflict.TacticalAvoidanceCommand.Should().Contain("climb +20m altitude");
    }

    [Fact]
    public void DeconflictAirspace_DroneEntersNoFlyZone_DetectsGeofenceBreach()
    {
        var now = DateTimeOffset.UtcNow;
        var airportGeofence = new NoFlyGeofenceZone("GEO-CDG", "Charles de Gaulle No-Fly Zone", 49.0097, 2.5479, RadiusMeters: 5000.0, FloorAltitudeMeters: 0.0, CeilingAltitudeMeters: 500.0);

        // Drone path inside airport geofence at 80m altitude
        var flight = new DroneFlightPlan("DRONE-MED-03", "REG-HOSPITAL",
            [new DroneWaypoint4D(49.0100, 2.5480, 80.0, now)],
            CruiseSpeedMps: 15.0, BatteryStateOfChargePercent: 90.0);

        var request = new DroneUtmDeconflictionRequest([flight], [airportGeofence]);
        var result = _engine.DeconflictAirspace(request);

        result.AirspaceSafe.Should().BeFalse();
        var conflict = result.Conflicts.Should().ContainSingle().Subject;
        conflict.ConflictType.Should().Be("GeofenceBreach");
        conflict.TacticalAvoidanceCommand.Should().Contain("Geofence violation");
    }

    [Fact]
    public void DeconflictAirspace_CriticalBattery_TriggersImmediateLandingEmergency()
    {
        var flight = new DroneFlightPlan("DRONE-LOW-BATT", "REG-AMAZON-X",
            [new DroneWaypoint4D(48.85, 2.35, 50.0, DateTimeOffset.UtcNow)],
            10.0, BatteryStateOfChargePercent: 12.0); // 12% < 20% limit

        var request = new DroneUtmDeconflictionRequest([flight], []);
        var result = _engine.DeconflictAirspace(request);

        result.AirspaceSafe.Should().BeFalse();
        var conflict = result.Conflicts.Should().ContainSingle().Subject;
        conflict.ConflictType.Should().Be("BatteryEmergency");
        conflict.TacticalAvoidanceCommand.Should().Contain("vertipad landing spot");
    }

    [Fact]
    public void DeconflictAirspace_ClearSeparatedAirspace_ReturnsSafeWithZeroConflicts()
    {
        var now = DateTimeOffset.UtcNow;
        var flightA = new DroneFlightPlan("DRONE-OK-1", "REG-1",
            [new DroneWaypoint4D(48.85, 2.35, 60.0, now)], 12.0, 90.0);
        var flightB = new DroneFlightPlan("DRONE-OK-2", "REG-2",
            [new DroneWaypoint4D(48.95, 2.45, 120.0, now)], 12.0, 85.0); // 15 km away

        var request = new DroneUtmDeconflictionRequest([flightA, flightB], []);
        var result = _engine.DeconflictAirspace(request);

        result.AirspaceSafe.Should().BeTrue();
        result.DetectedConflictsCount.Should().Be(0);
        result.TrafficManagementAdvisory.Should().Contain("Airspace Clear");
    }
}
