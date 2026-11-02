using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Robotics.AgvFlockingCoordinator;
using Xunit;

namespace SGrowing.Tests.Robotics;

public sealed class AgvFlockingTests
{
    private readonly AgvFlockingCollisionAvoidanceEngine _engine = new();

    [Fact]
    public void CoordinateFleet_HeadOnCollisionCourse_AppliesReciprocalLateralAvoidance()
    {
        // Arrange: Robot A at (0, 0) moving towards (10, 0). Robot B at (5, 0) moving towards (0, 0). Direct head-on collision course!
        var agvA = new AgvRobotAgent(
            AgvId: "AGV-01",
            CurrentPosition: new Vector2D(0.0, 0.0),
            CurrentVelocity: new Vector2D(1.5, 0.0),
            GoalPosition: new Vector2D(10.0, 0.0),
            RadiusMeters: 0.60,
            MaxSpeedMps: 2.0
        );

        var agvB = new AgvRobotAgent(
            AgvId: "AGV-02",
            CurrentPosition: new Vector2D(4.0, 0.0),
            CurrentVelocity: new Vector2D(-1.5, 0.0),
            GoalPosition: new Vector2D(-2.0, 0.0),
            RadiusMeters: 0.60,
            MaxSpeedMps: 2.0
        );

        var request = new CoordinateAgvFleetRequest(
            TimeHorizonSeconds: 3.0,
            Agvs: [agvA, agvB]
        );

        // Act
        var result = _engine.CoordinateFleetVelocity(request);

        // Assert
        result.Should().NotBeNull();
        result.TotalAgvsCoordinated.Should().Be(2);
        result.PotentialCollisionsResolved.Should().BeGreaterThan(0);

        var dispatchA = result.AgvDispatches.First(d => d.AgvId == "AGV-01");
        var dispatchB = result.AgvDispatches.First(d => d.AgvId == "AGV-02");

        dispatchA.AvoidanceManeuverActive.Should().BeTrue();
        dispatchB.AvoidanceManeuverActive.Should().BeTrue();

        // Check lateral avoidance component in Y axis
        Math.Abs(dispatchA.SafeAdjustedVelocity.Y).Should().BeGreaterThan(0.1, "Robot A should divert laterally to avoid head-on crash");
        dispatchA.SafeAdjustedVelocity.Magnitude.Should().BeLessThanOrEqualTo(2.0);
    }

    [Fact]
    public void CoordinateFleet_ParallelClearPaths_MaintainsPreferredVelocities()
    {
        // Arrange: Two robots moving in parallel aisles 5 meters apart
        var agv1 = new AgvRobotAgent("AGV-A1", new Vector2D(0, 0), new Vector2D(1.5, 0), new Vector2D(10, 0), MaxSpeedMps: 1.5);
        var agv2 = new AgvRobotAgent("AGV-A2", new Vector2D(0, 5), new Vector2D(1.5, 0), new Vector2D(10, 5), MaxSpeedMps: 1.5);

        var request = new CoordinateAgvFleetRequest(3.0, [agv1, agv2]);

        // Act
        var result = _engine.CoordinateFleetVelocity(request);

        // Assert
        result.PotentialCollisionsResolved.Should().Be(0);
        result.AgvDispatches.All(d => !d.AvoidanceManeuverActive).Should().BeTrue();
        result.AgvDispatches[0].SafeAdjustedVelocity.Magnitude.Should().BeApproximately(1.5, 0.05);
    }

    [Fact]
    public void CoordinateFleet_OverlappingEmergency_AppliesRepulsiveSeparationForce()
    {
        // Arrange: Robots starting dangerously close (< combined radius 1.2m + 0.15m cushion)
        var agvClose1 = new AgvRobotAgent("AGV-NEAR-1", new Vector2D(1.0, 1.0), new Vector2D(0, 0), new Vector2D(5, 1));
        var agvClose2 = new AgvRobotAgent("AGV-NEAR-2", new Vector2D(1.4, 1.0), new Vector2D(0, 0), new Vector2D(0, 1)); // Distance = 0.4m < 1.35m

        var request = new CoordinateAgvFleetRequest(2.5, [agvClose1, agvClose2]);

        // Act
        var result = _engine.CoordinateFleetVelocity(request);

        // Assert
        result.PotentialCollisionsResolved.Should().BeGreaterThan(0);
        var d1 = result.AgvDispatches.First(d => d.AgvId == "AGV-NEAR-1");
        d1.AvoidanceManeuverActive.Should().BeTrue();
        d1.SafeAdjustedVelocity.X.Should().BeLessThan(0.0, "Should push away left from robot 2");
    }

    [Fact]
    public void AddAgvFlockingServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAgvFlockingServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(AgvFlockingCollisionAvoidanceEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
