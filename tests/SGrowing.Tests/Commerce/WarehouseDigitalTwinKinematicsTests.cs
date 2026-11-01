using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Commerce.DigitalTwin;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class WarehouseDigitalTwinKinematicsTests
{
    private readonly WarehouseDigitalTwinPhysicsEngine _physicsEngine = new();
    private readonly RoboticsKinematicsSimulatorService _kinematicsService = new();
    private readonly CollisionAvoidanceTrajectoryPlanner _trajectoryPlanner = new();

    #region 1. Emergency Braking Dynamics & Friction Tests

    [Fact]
    public void SimulateEmergencyBraking_DryEpoxyCleanFloor_CalculatesExactStoppingDistance()
    {
        // Dry Epoxy mu = 0.70, g = 9.80665 m/s^2 -> maxDecel = 6.864655 m/s^2
        // Speed = 2.0 m/s -> StoppingDistance = (2.0)^2 / (2 * 6.864655) = 4 / 13.72931 ~ 0.291 m
        var request = new EmergencyBrakingSimulationRequest(
            RobotId: "AMR-01",
            CurrentSpeedMetersPerSec: 2.0,
            PayloadMassKg: 50.0,
            PayloadHeightMeters: 0.6,
            SurfaceCondition: FloorSurfaceCondition.DryEpoxyClean
        );

        var result = _physicsEngine.SimulateEmergencyBraking(request);

        result.FrictionCoefficientMu.Should().Be(0.70);
        result.StoppingDistanceMeters.Should().BeApproximately(0.291, 0.01);
        result.StoppingTimeSeconds.Should().BeApproximately(0.291, 0.01);
        result.MaxDecelerationMetersPerSec2.Should().BeApproximately(6.865, 0.01);
        result.TipOverRiskDetected.Should().BeFalse();
    }

    [Fact]
    public void SimulateEmergencyBraking_OilSpillContaminatedFloor_QuadruplesStoppingDistance()
    {
        // Oil spill mu = 0.15 (vs Dry Epoxy 0.70)
        // Decel drops from ~6.86 to ~1.47 m/s^2, stopping distance increases by ~4.67x
        var requestDry = new EmergencyBrakingSimulationRequest("AMR-DRY", 2.0, 50.0, 0.6, FloorSurfaceCondition.DryEpoxyClean);
        var requestOil = new EmergencyBrakingSimulationRequest("AMR-OIL", 2.0, 50.0, 0.6, FloorSurfaceCondition.OilSpillContaminated);

        var resultDry = _physicsEngine.SimulateEmergencyBraking(requestDry);
        var resultOil = _physicsEngine.SimulateEmergencyBraking(requestOil);

        resultOil.FrictionCoefficientMu.Should().Be(0.15);
        resultOil.StoppingDistanceMeters.Should().BeGreaterThan(resultDry.StoppingDistanceMeters * 4.0);
        resultOil.StoppingDistanceMeters.Should().BeApproximately(1.36, 0.05);
    }

    [Fact]
    public void SimulateEmergencyBraking_ZeroSpeed_ReturnsZeroDistance()
    {
        var request = new EmergencyBrakingSimulationRequest("AMR-STOPPED", 0.0, 100.0, 0.5, FloorSurfaceCondition.DryEpoxyClean);
        var result = _physicsEngine.SimulateEmergencyBraking(request);

        result.StoppingDistanceMeters.Should().Be(0.0);
        result.StoppingTimeSeconds.Should().Be(0.0);
    }

    [Fact]
    public void SimulateEmergencyBraking_NegativeSpeed_ThrowsArgumentOutOfRangeException()
    {
        var request = new EmergencyBrakingSimulationRequest("AMR-NEG", -1.5, 50.0, 0.5, FloorSurfaceCondition.DryEpoxyClean);
        var action = () => _physicsEngine.SimulateEmergencyBraking(request);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SimulateEmergencyBraking_HighPayloadElevatedCenterOfMass_DetectsTipOverRisk()
    {
        // Heavy 500kg payload stacked 2.2 meters high travelling at 3.5 m/s
        var request = new EmergencyBrakingSimulationRequest(
            RobotId: "AMR-HEAVY-HIGH",
            CurrentSpeedMetersPerSec: 3.5,
            PayloadMassKg: 500.0,
            PayloadHeightMeters: 2.2,
            SurfaceCondition: FloorSurfaceCondition.DryEpoxyClean,
            TrackWidthMeters: 0.7
        );

        var result = _physicsEngine.SimulateEmergencyBraking(request);

        result.TipOverRiskDetected.Should().BeTrue();
        result.SafetyRecommendation.Should().Contain("WARNING");
    }

    #endregion

    #region 2. Center of Mass & Kinematic Stability Tests

    [Fact]
    public void CalculateCombinedCenterOfMass_EmptyChassis_CenterOfMassAtHalfChassisHeight()
    {
        // Chassis mass = 250kg, height = 0.40m, payload = 0kg -> Z_com = 0.20m
        var com = _physicsEngine.CalculateCombinedCenterOfMass(250.0, 0.40, 0.0, 0.0);
        com.Z.Should().Be(0.20);
    }

    [Fact]
    public void CalculateCombinedCenterOfMass_HeavyPayload_ElevatesCenterOfMass()
    {
        // Chassis: 250kg at h = 0.40m (z1 = 0.20m)
        // Payload: 250kg at h = 1.0m above chassis (z2 = 0.40 + 0.50 = 0.90m)
        // Z_com = (250 * 0.20 + 250 * 0.90) / 500 = (50 + 225) / 500 = 275 / 500 = 0.55m
        var com = _physicsEngine.CalculateCombinedCenterOfMass(250.0, 0.40, 250.0, 1.0);
        com.Z.Should().Be(0.55);
    }

    [Fact]
    public void CalculateMaxSafeCorneringSpeed_HigherCenterOfMass_ReducesPermissibleCorneringSpeed()
    {
        double radius = 2.0;
        double trackWidth = 0.8;

        double speedLowCom = _physicsEngine.CalculateMaxSafeCorneringSpeed(radius, trackWidth, centerOfMassHeightMeters: 0.3, FloorSurfaceCondition.DryEpoxyClean);
        double speedHighCom = _physicsEngine.CalculateMaxSafeCorneringSpeed(radius, trackWidth, centerOfMassHeightMeters: 1.2, FloorSurfaceCondition.DryEpoxyClean);

        speedHighCom.Should().BeLessThan(speedLowCom);
    }

    #endregion

    #region 3. Robotic Arm Kinematics & Palletizer Simulation Tests

    [Fact]
    public void ComputeForwardKinematics_ZeroJointAngles_PositionsEndEffectorAtExtendedReach()
    {
        // 2-link planar arm in Z-plane: Link 1 = 1.0m (along Z), Link 2 = 1.0m (along X)
        List<DhJointParameters> joints =
        [
            new(JointIndex: 1, ThetaDegrees: 0, DistanceD: 1.0, LinkLengthA: 0.0, AlphaDegrees: 90.0, MaxTorqueNm: 200.0, CurrentTorqueNm: 50.0),
            new(JointIndex: 2, ThetaDegrees: 0, DistanceD: 0.0, LinkLengthA: 1.0, AlphaDegrees: 0.0, MaxTorqueNm: 150.0, CurrentTorqueNm: 40.0)
        ];

        var request = new PalletizerKinematicsRequest("ARM-PALLET-01", joints, PayloadMassKg: 20.0);
        var result = _kinematicsService.ComputeForwardKinematics(request);

        result.EndEffector.Z.Should().BeApproximately(1.0, 0.05);
        result.EndEffector.X.Should().BeApproximately(1.0, 0.05);
        result.EndEffector.WithinWorkEnvelope.Should().BeTrue();
        result.SafetyInterlockTriggered.Should().BeFalse();
    }

    [Fact]
    public void ComputeForwardKinematics_ExceedingJointTorqueLimit_TriggersSafetyInterlock()
    {
        List<DhJointParameters> joints =
        [
            new(JointIndex: 1, ThetaDegrees: 45.0, DistanceD: 0.5, LinkLengthA: 0.8, AlphaDegrees: 0.0, MaxTorqueNm: 100.0, CurrentTorqueNm: 135.0) // 135 > 100
        ];

        var request = new PalletizerKinematicsRequest("ARM-OVERTORQUE", joints, PayloadMassKg: 40.0);
        var result = _kinematicsService.ComputeForwardKinematics(request);

        result.EndEffector.TorqueLimitsExceeded.Should().BeTrue();
        result.SafetyInterlockTriggered.Should().BeTrue();
    }

    [Fact]
    public void IsWithinWorkEnvelope_TargetOutsideMaxReach_ReturnsFalse()
    {
        // Target at 4.0 meters (exceeding default 2.8m reach)
        bool within = _kinematicsService.IsWithinWorkEnvelope(3.0, 3.0, 1.0, maxReachMeters: 2.8);
        within.Should().BeFalse();
    }

    [Fact]
    public void CalculateArmPowerConsumption_ActiveTorqueLoad_IncludesServoMechanicalPowerAndStandby()
    {
        List<DhJointParameters> joints =
        [
            new(1, 0, 0, 1, 0, 200, CurrentTorqueNm: 80.0),
            new(2, 0, 0, 1, 0, 150, CurrentTorqueNm: 60.0)
        ];

        // Mechanical power = (80 + 60) * 1.0 rad/s = 140 W. At 80% efficiency: 140 / 0.8 = 175 W. Standby = 75 W -> Total = 250 W
        double powerWatts = _kinematicsService.CalculateArmPowerConsumption(joints, jointAngularVelocityRadPerSec: 1.0);
        powerWatts.Should().BeApproximately(250.0, 1.0);
    }

    #endregion

    #region 4. Collision-Avoidance Multi-Agent Path Finding (MAPF) Tests

    [Fact]
    public void PlanMultiAgentTrajectories_SingleRobotNoObstacles_PlansDirectRoute()
    {
        var request = new MultiAgentPathRequest(
            Requests:
            [
                new RobotRouteRequest("AGV-1", new Vector2D(0, 0), new Vector2D(5, 0), MaxSpeedMetersPerSec: 1.5)
            ]
        );

        var response = _trajectoryPlanner.PlanMultiAgentTrajectories(request);

        response.AllRoutesFound.Should().BeTrue();
        response.Plans.Should().HaveCount(1);
        var plan = response.Plans[0];
        plan.PathFound.Should().BeTrue();
        plan.Waypoints.Should().NotBeEmpty();
        plan.Waypoints[0].X.Should().Be(0);
        plan.Waypoints[^1].X.Should().Be(5);
        plan.Waypoints[^1].Y.Should().Be(0);
    }

    [Fact]
    public void PlanMultiAgentTrajectories_WithStaticObstacle_NavigatesAroundObstacle()
    {
        // Start (0, 0), Target (2, 0), Obstacle at (1, 0)
        var request = new MultiAgentPathRequest(
            Requests:
            [
                new RobotRouteRequest("AGV-BYPASS", new Vector2D(0, 0), new Vector2D(2, 0))
            ],
            StaticObstacles: [new Vector2D(1, 0)]
        );

        var response = _trajectoryPlanner.PlanMultiAgentTrajectories(request);

        response.AllRoutesFound.Should().BeTrue();
        var plan = response.Plans[0];
        plan.PathFound.Should().BeTrue();
        // Path should never step on (1, 0)
        plan.Waypoints.Should().NotContain(w => Math.Abs(w.X - 1.0) < 0.1 && Math.Abs(w.Y - 0.0) < 0.1);
    }

    [Fact]
    public void PlanMultiAgentTrajectories_TwoRobotsCrossingHeadOn_ResolvesConflictViaSpaceTimeWaitOrDetour()
    {
        // Robot 1: (0, 2) -> (4, 2)
        // Robot 2: (4, 2) -> (0, 2)  [Head-on collision course on same aisle]
        var request = new MultiAgentPathRequest(
            Requests:
            [
                new RobotRouteRequest("AGV-EAST", new Vector2D(0, 2), new Vector2D(4, 2)),
                new RobotRouteRequest("AGV-WEST", new Vector2D(4, 2), new Vector2D(0, 2))
            ]
        );

        var response = _trajectoryPlanner.PlanMultiAgentTrajectories(request);

        response.AllRoutesFound.Should().BeTrue();
        response.Plans.Should().HaveCount(2);

        var plan1 = response.Plans[0];
        var plan2 = response.Plans[1];

        // Verify there is zero space-time conflict between the two trajectories
        bool hasConflict = _trajectoryPlanner.HasConflict(plan1.Waypoints, plan2.Waypoints);
        hasConflict.Should().BeFalse();
    }

    [Fact]
    public void HasConflict_IdenticalWaypointsAtSameTimeStep_ReturnsTrue()
    {
        List<SpaceTimeWaypoint> trajA = [new(2, 2, 0, 1), new(3, 2, 1, 1)];
        List<SpaceTimeWaypoint> trajB = [new(5, 2, 0, 1), new(3, 2, 1, 1)]; // Collision at t = 1 at (3, 2)

        bool conflict = _trajectoryPlanner.HasConflict(trajA, trajB);
        conflict.Should().BeTrue();
    }

    [Fact]
    public void HasConflict_DisjointTrajectories_ReturnsFalse()
    {
        List<SpaceTimeWaypoint> trajA = [new(0, 0, 0, 1), new(1, 0, 1, 1)];
        List<SpaceTimeWaypoint> trajB = [new(0, 10, 0, 1), new(1, 10, 1, 1)];

        bool conflict = _trajectoryPlanner.HasConflict(trajA, trajB);
        conflict.Should().BeFalse();
    }

    #endregion

    #region 5. Digital Twin State Registry & Telemetry Tests

    [Fact]
    public void UpdateRobotTelemetry_RegistersAndRetrievesState()
    {
        _physicsEngine.ClearRobots();

        var state = new AgvTelemetryState(
            RobotId: "ROBOT-TWIN-01",
            RobotType: AgvRobotType.AutonomousMobileRobot,
            State: RobotOperatingState.NavigatingToPick,
            Position: new Vector2D(12.5, 34.2),
            Velocity: new Vector2D(1.2, 0.0),
            HeadingRadians: 0.0,
            AngularVelocityRadPerSec: 0.0,
            PayloadMassKg: 75.0,
            CenterOfMass: new CenterOfMass3D(0, 0, 0.45),
            SurfaceCondition: FloorSurfaceCondition.DryEpoxyClean,
            BatteryLevelPct: 88.5,
            TipOverWarning: false,
            CalculatedStoppingDistanceMeters: 0.35,
            TimestampUtc: DateTime.UtcNow
        );

        _physicsEngine.UpdateRobotTelemetry(state);

        _physicsEngine.ActiveRobotCount.Should().Be(1);
        var retrieved = _physicsEngine.GetRobotTelemetry("ROBOT-TWIN-01");
        retrieved.Should().NotBeNull();
        retrieved!.Position.X.Should().Be(12.5);
        retrieved.Position.Y.Should().Be(34.2);
        retrieved.State.Should().Be(RobotOperatingState.NavigatingToPick);
    }

    [Fact]
    public void ClearRobots_RemovesAllSimulatedRobots()
    {
        _physicsEngine.UpdateRobotTelemetry(new AgvTelemetryState(
            "R1", AgvRobotType.ForkliftTugger, RobotOperatingState.Idle, Vector2D.Zero, Vector2D.Zero,
            0, 0, 0, new CenterOfMass3D(), FloorSurfaceCondition.DryEpoxyClean, 100, false, 0, DateTime.UtcNow));

        _physicsEngine.ActiveRobotCount.Should().Be(1);
        _physicsEngine.ClearRobots();
        _physicsEngine.ActiveRobotCount.Should().Be(0);
    }

    [Fact]
    public void DirectServiceCollectionRegistration_ValidatesDescriptorsWithoutBuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDigitalTwinServices();

        services.Should().Contain(d => d.ServiceType == typeof(IWarehouseDigitalTwinPhysicsEngine) &&
                                       d.ImplementationType == typeof(WarehouseDigitalTwinPhysicsEngine) &&
                                       d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d => d.ServiceType == typeof(IRoboticsKinematicsSimulatorService) &&
                                       d.ImplementationType == typeof(RoboticsKinematicsSimulatorService) &&
                                       d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d => d.ServiceType == typeof(ICollisionAvoidanceTrajectoryPlanner) &&
                                       d.ImplementationType == typeof(CollisionAvoidanceTrajectoryPlanner) &&
                                       d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
