namespace SGrowing.Api.Modules.Commerce.DigitalTwin;

/// <summary>
/// Type of mobile robot operating in the warehouse.
/// </summary>
public enum AgvRobotType
{
    AutonomousMobileRobot,      // AMR (LiDAR + SLAM)
    AutomatedGuidedVehicle,     // AGV (Magnetic tape/QR grid)
    ForkliftTugger,             // Heavy pallet mover
    HighBayCraneAsrs            // Vertical storage retrieval unit
}

/// <summary>
/// Operational state of a simulated or physical robot.
/// </summary>
public enum RobotOperatingState
{
    Idle,
    NavigatingToPick,
    LiftingPayload,
    TransportingPayload,
    LoweringPayload,
    EmergencyBraking,
    ChargingDepot,
    MaintenanceRequired
}

/// <summary>
/// Surface condition of warehouse concrete floor influencing friction.
/// </summary>
public enum FloorSurfaceCondition
{
    DryEpoxyClean,     // mu = 0.70
    DustyConcrete,     // mu = 0.50
    WetMoisture,       // mu = 0.35
    OilSpillContaminated // mu = 0.15
}

/// <summary>
/// 2D Vector representing position or velocity.
/// </summary>
public readonly record struct Vector2D(double X, double Y)
{
    public static readonly Vector2D Zero = new(0, 0);

    public double Magnitude => Math.Sqrt((X * X) + (Y * Y));

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2D operator *(Vector2D a, double scalar) => new(a.X * scalar, a.Y * scalar);
    public static Vector2D operator /(Vector2D a, double scalar) => scalar != 0 ? new(a.X / scalar, a.Y / scalar) : Zero;

    public double DistanceTo(Vector2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

/// <summary>
/// Center of mass coordinates in 3D relative to robot base.
/// </summary>
public readonly record struct CenterOfMass3D(double X, double Y, double Z);

/// <summary>
/// Telemetry state of a robotic unit in the digital twin.
/// </summary>
public record AgvTelemetryState(
    string RobotId,
    AgvRobotType RobotType,
    RobotOperatingState State,
    Vector2D Position,
    Vector2D Velocity,
    double HeadingRadians,
    double AngularVelocityRadPerSec,
    double PayloadMassKg,
    CenterOfMass3D CenterOfMass,
    FloorSurfaceCondition SurfaceCondition,
    double BatteryLevelPct,
    bool TipOverWarning,
    double CalculatedStoppingDistanceMeters,
    DateTime TimestampUtc
);

/// <summary>
/// Kinematic parameters for a multi-joint robotic arm (Denavit-Hartenberg parameters).
/// </summary>
public record DhJointParameters(
    int JointIndex,
    double ThetaDegrees,
    double DistanceD,
    double LinkLengthA,
    double AlphaDegrees,
    double MaxTorqueNm,
    double CurrentTorqueNm
);

/// <summary>
/// Forward kinematics calculation result for an end-effector.
/// </summary>
public record EndEffectorPose(
    double X,
    double Y,
    double Z,
    double RollDegrees,
    double PitchDegrees,
    double YawDegrees,
    bool WithinWorkEnvelope,
    bool TorqueLimitsExceeded
);

/// <summary>
/// Space-Time waypoint in trajectory planning (X, Y, TimeStep).
/// </summary>
public record SpaceTimeWaypoint(
    double X,
    double Y,
    int TimeStep,
    double TargetSpeedMetersPerSec
);

/// <summary>
/// Conflict-free multi-agent path finding result.
/// </summary>
public record TrajectoryPlanResult(
    string RobotId,
    bool PathFound,
    IReadOnlyList<SpaceTimeWaypoint> Waypoints,
    double TotalDistanceMeters,
    double EstimatedDurationSeconds,
    int ConflictsResolved,
    string? Notes = null
);

/// <summary>
/// Request to simulate emergency braking dynamics.
/// </summary>
public record EmergencyBrakingSimulationRequest(
    string RobotId,
    double CurrentSpeedMetersPerSec,
    double PayloadMassKg,
    double PayloadHeightMeters,
    FloorSurfaceCondition SurfaceCondition,
    double TrackWidthMeters = 0.8
);

/// <summary>
/// Result of emergency braking physics simulation.
/// </summary>
public record EmergencyBrakingSimulationResult(
    string RobotId,
    double FrictionCoefficientMu,
    double StoppingDistanceMeters,
    double StoppingTimeSeconds,
    double MaxDecelerationMetersPerSec2,
    double CentrifugalOverturnThresholdMetersPerSec,
    bool TipOverRiskDetected,
    string SafetyRecommendation
);

/// <summary>
/// Request to compute robotic arm forward kinematics.
/// </summary>
public record PalletizerKinematicsRequest(
    string ArmId,
    IReadOnlyList<DhJointParameters> Joints,
    double PayloadMassKg
);

/// <summary>
/// Result of robotic arm kinematics simulation.
/// </summary>
public record PalletizerKinematicsResult(
    string ArmId,
    EndEffectorPose EndEffector,
    double TotalPowerWatts,
    double CycleTimeSeconds,
    bool SafetyInterlockTriggered
);

/// <summary>
/// Request to plan conflict-free trajectories for multiple AGVs.
/// </summary>
public record MultiAgentPathRequest(
    IReadOnlyList<RobotRouteRequest> Requests,
    IReadOnlyList<Vector2D>? StaticObstacles = null,
    int MaxTimeHorizon = 100
);

/// <summary>
/// Individual robot navigation request.
/// </summary>
public record RobotRouteRequest(
    string RobotId,
    Vector2D StartPosition,
    Vector2D TargetPosition,
    double MaxSpeedMetersPerSec = 2.0
);

/// <summary>
/// Summary response for multi-agent trajectory planning.
/// </summary>
public record MultiAgentPathResponse(
    bool AllRoutesFound,
    IReadOnlyList<TrajectoryPlanResult> Plans,
    int TotalDeadlocksPrevented,
    DateTime PlannedAtUtc
);
