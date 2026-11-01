namespace SGrowing.Api.Modules.Commerce.DigitalTwin;

/// <summary>
/// Physics simulation engine for warehouse digital twin modeling kinematics, braking, friction, and stability.
/// </summary>
public interface IWarehouseDigitalTwinPhysicsEngine
{
    /// <summary>
    /// Total simulated robots actively tracked in the digital twin.
    /// </summary>
    int ActiveRobotCount { get; }

    /// <summary>
    /// Gravity acceleration constant in m/s^2.
    /// </summary>
    double Gravity { get; }

    /// <summary>
    /// Simulates emergency braking dynamics taking into account payload mass, center of gravity, and floor surface friction.
    /// </summary>
    EmergencyBrakingSimulationResult SimulateEmergencyBraking(EmergencyBrakingSimulationRequest request);

    /// <summary>
    /// Calculates the 3D center of mass combining robot chassis and elevated payload.
    /// </summary>
    CenterOfMass3D CalculateCombinedCenterOfMass(
        double chassisMassKg,
        double chassisHeightMeters,
        double payloadMassKg,
        double payloadHeightMeters,
        double payloadElevationOffsetMeters = 0.0);

    /// <summary>
    /// Calculates the maximum safe cornering speed to prevent lateral tip-over on curves.
    /// </summary>
    double CalculateMaxSafeCorneringSpeed(
        double curveRadiusMeters,
        double trackWidthMeters,
        double centerOfMassHeightMeters,
        FloorSurfaceCondition surfaceCondition);

    /// <summary>
    /// Updates or registers the live digital twin telemetry state of an AGV/AMR.
    /// </summary>
    void UpdateRobotTelemetry(AgvTelemetryState telemetry);

    /// <summary>
    /// Retrieves the latest telemetry state of a robot from the digital twin.
    /// </summary>
    AgvTelemetryState? GetRobotTelemetry(string robotId);

    /// <summary>
    /// Retrieves all active robots in the digital twin.
    /// </summary>
    IReadOnlyList<AgvTelemetryState> GetAllRobots();

    /// <summary>
    /// Clears all robots from the digital twin.
    /// </summary>
    void ClearRobots();
}

/// <summary>
/// Production real-time physics simulator for warehouse digital twins.
/// </summary>
public sealed class WarehouseDigitalTwinPhysicsEngine : IWarehouseDigitalTwinPhysicsEngine
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, AgvTelemetryState> _robots = new(StringComparer.OrdinalIgnoreCase);

    public const double StandardGravity = 9.80665;
    public const double DefaultChassisMassKg = 250.0;
    public const double DefaultChassisHeightMeters = 0.35;
    public const double DefaultWheelbaseMeters = 0.90;

    public int ActiveRobotCount
    {
        get
        {
            lock (_lock)
            {
                return _robots.Count;
            }
        }
    }

    public double Gravity => StandardGravity;

    public EmergencyBrakingSimulationResult SimulateEmergencyBraking(EmergencyBrakingSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CurrentSpeedMetersPerSec < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Current speed cannot be negative.");

        if (request.PayloadMassKg < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Payload mass cannot be negative.");

        double mu = GetFrictionCoefficient(request.SurfaceCondition);
        double maxDecel = mu * StandardGravity;

        double speed = request.CurrentSpeedMetersPerSec;
        double stoppingDistance = 0.0;
        double stoppingTime = 0.0;

        if (speed > 0 && maxDecel > 0)
        {
            stoppingDistance = (speed * speed) / (2.0 * maxDecel);
            stoppingTime = speed / maxDecel;
        }

        var com = CalculateCombinedCenterOfMass(
            DefaultChassisMassKg,
            DefaultChassisHeightMeters,
            request.PayloadMassKg,
            request.PayloadHeightMeters
        );

        // Lateral overturning threshold on standard 1.5m radius turn
        double curveRadius = 1.5;
        double overturnSpeed = CalculateMaxSafeCorneringSpeed(
            curveRadius,
            request.TrackWidthMeters > 0 ? request.TrackWidthMeters : 0.8,
            com.Z,
            request.SurfaceCondition
        );

        // Tip-over risk during sudden deceleration if deceleration exceeds longitudinal stability ratio
        double longitudinalLimit = (DefaultWheelbaseMeters / (2.0 * Math.Max(0.1, com.Z))) * StandardGravity;
        bool tipOverRisk = maxDecel > longitudinalLimit || speed > overturnSpeed;

        string recommendation = tipOverRisk
            ? $"WARNING: High payload center of mass ({com.Z:F2}m) creates tip-over risk during deceleration. Reduce speed below {overturnSpeed:F2} m/s and clean floor moisture."
            : "Emergency braking trajectory is stable. Deceleration within safe kinematic envelope.";

        return new EmergencyBrakingSimulationResult(
            RobotId: request.RobotId,
            FrictionCoefficientMu: mu,
            StoppingDistanceMeters: Math.Round(stoppingDistance, 3),
            StoppingTimeSeconds: Math.Round(stoppingTime, 3),
            MaxDecelerationMetersPerSec2: Math.Round(maxDecel, 3),
            CentrifugalOverturnThresholdMetersPerSec: Math.Round(overturnSpeed, 3),
            TipOverRiskDetected: tipOverRisk,
            SafetyRecommendation: recommendation
        );
    }

    public CenterOfMass3D CalculateCombinedCenterOfMass(
        double chassisMassKg,
        double chassisHeightMeters,
        double payloadMassKg,
        double payloadHeightMeters,
        double payloadElevationOffsetMeters = 0.0)
    {
        double m1 = Math.Max(1.0, chassisMassKg);
        double m2 = Math.Max(0.0, payloadMassKg);
        double totalMass = m1 + m2;

        double z1 = chassisHeightMeters / 2.0;
        double z2 = chassisHeightMeters + payloadElevationOffsetMeters + (payloadHeightMeters / 2.0);

        double zCom = ((m1 * z1) + (m2 * z2)) / totalMass;

        return new CenterOfMass3D(0.0, 0.0, Math.Round(zCom, 4));
    }

    public double CalculateMaxSafeCorneringSpeed(
        double curveRadiusMeters,
        double trackWidthMeters,
        double centerOfMassHeightMeters,
        FloorSurfaceCondition surfaceCondition)
    {
        if (curveRadiusMeters <= 0 || trackWidthMeters <= 0 || centerOfMassHeightMeters <= 0)
            return 0.0;

        // Kinematic overturning limit: v_overturn = sqrt((w * g * R) / (2 * h))
        double vOverturn = Math.Sqrt((trackWidthMeters * StandardGravity * curveRadiusMeters) / (2.0 * centerOfMassHeightMeters));

        // Lateral friction skid limit: v_skid = sqrt(mu * g * R)
        double mu = GetFrictionCoefficient(surfaceCondition);
        double vSkid = Math.Sqrt(mu * StandardGravity * curveRadiusMeters);

        // Safe speed is the minimum of overturning limit and friction skid limit, with a 15% safety factor
        double safeSpeed = Math.Min(vOverturn, vSkid) * 0.85;
        return Math.Round(safeSpeed, 3);
    }

    public void UpdateRobotTelemetry(AgvTelemetryState telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        lock (_lock)
        {
            _robots[telemetry.RobotId] = telemetry;
        }
    }

    public AgvTelemetryState? GetRobotTelemetry(string robotId)
    {
        if (string.IsNullOrWhiteSpace(robotId)) return null;

        lock (_lock)
        {
            return _robots.GetValueOrDefault(robotId);
        }
    }

    public IReadOnlyList<AgvTelemetryState> GetAllRobots()
    {
        lock (_lock)
        {
            return [.. _robots.Values];
        }
    }

    public void ClearRobots()
    {
        lock (_lock)
        {
            _robots.Clear();
        }
    }

    private static double GetFrictionCoefficient(FloorSurfaceCondition condition) => condition switch
    {
        FloorSurfaceCondition.DryEpoxyClean => 0.70,
        FloorSurfaceCondition.DustyConcrete => 0.50,
        FloorSurfaceCondition.WetMoisture => 0.35,
        FloorSurfaceCondition.OilSpillContaminated => 0.15,
        _ => 0.60
    };
}
