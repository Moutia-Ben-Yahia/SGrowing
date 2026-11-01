namespace SGrowing.Api.Modules.Commerce.DigitalTwin;

/// <summary>
/// Forward kinematics and dynamic torque simulator for robotic palletizer arms in warehouse cells.
/// </summary>
public interface IRoboticsKinematicsSimulatorService
{
    /// <summary>
    /// Computes the forward kinematics transformation matrix and end-effector pose from D-H parameters.
    /// </summary>
    PalletizerKinematicsResult ComputeForwardKinematics(PalletizerKinematicsRequest request);

    /// <summary>
    /// Validates whether a target $(X, Y, Z)$ position is within the reachable spherical work envelope of the arm.
    /// </summary>
    bool IsWithinWorkEnvelope(double x, double y, double z, double maxReachMeters = 2.5);

    /// <summary>
    /// Calculates electrical power consumption (Watts) across all active servo joints.
    /// </summary>
    double CalculateArmPowerConsumption(IReadOnlyList<DhJointParameters> joints, double jointAngularVelocityRadPerSec = 1.0);
}

/// <summary>
/// Production robotic kinematics simulator utilizing Denavit-Hartenberg (D-H) 4x4 matrix chaining.
/// </summary>
public sealed class RoboticsKinematicsSimulatorService : IRoboticsKinematicsSimulatorService
{
    private const double StandbyPowerWatts = 75.0;
    private const double DefaultMaxReachMeters = 2.8;

    public PalletizerKinematicsResult ComputeForwardKinematics(PalletizerKinematicsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Joints == null || request.Joints.Count == 0)
        {
            return new PalletizerKinematicsResult(
                ArmId: request.ArmId,
                EndEffector: new EndEffectorPose(0, 0, 0, 0, 0, 0, WithinWorkEnvelope: true, TorqueLimitsExceeded: false),
                TotalPowerWatts: StandbyPowerWatts,
                CycleTimeSeconds: 0.0,
                SafetyInterlockTriggered: false);
        }

        // Initialize 4x4 Identity Matrix
        double[,] cumulativeTransform = new double[4, 4]
        {
            { 1, 0, 0, 0 },
            { 0, 1, 0, 0 },
            { 0, 0, 1, 0 },
            { 0, 0, 0, 1 }
        };

        bool torqueExceeded = false;
        double maxJointDeflectionDeg = 0.0;

        foreach (var joint in request.Joints)
        {
            if (Math.Abs(joint.CurrentTorqueNm) > joint.MaxTorqueNm)
            {
                torqueExceeded = true;
            }

            maxJointDeflectionDeg = Math.Max(maxJointDeflectionDeg, Math.Abs(joint.ThetaDegrees));

            double[,] a_i = BuildDhTransformationMatrix(
                thetaDeg: joint.ThetaDegrees,
                d: joint.DistanceD,
                a: joint.LinkLengthA,
                alphaDeg: joint.AlphaDegrees);

            cumulativeTransform = Multiply4x4(cumulativeTransform, a_i);
        }

        // Extract End-Effector Cartesian Position
        double x = cumulativeTransform[0, 3];
        double y = cumulativeTransform[1, 3];
        double z = cumulativeTransform[2, 3];

        // Extract Euler Orientation Angles from Rotation Submatrix
        // R31 = -sin(pitch), etc.
        double pitchRad = Math.Asin(Math.Clamp(-cumulativeTransform[2, 0], -1.0, 1.0));
        double rollRad = Math.Atan2(cumulativeTransform[2, 1], cumulativeTransform[2, 2]);
        double yawRad = Math.Atan2(cumulativeTransform[1, 0], cumulativeTransform[0, 0]);

        double rollDeg = rollRad * (180.0 / Math.PI);
        double pitchDeg = pitchRad * (180.0 / Math.PI);
        double yawDeg = yawRad * (180.0 / Math.PI);

        bool inEnvelope = IsWithinWorkEnvelope(x, y, z, DefaultMaxReachMeters);
        bool safetyTriggered = torqueExceeded || !inEnvelope;

        double powerWatts = CalculateArmPowerConsumption(request.Joints);
        // Estimate cycle time based on max joint rotation angle (at ~60 deg/sec average servo velocity) + 1.2s pick/place dwell
        double cycleTimeSec = Math.Round((maxJointDeflectionDeg / 60.0) + 1.2, 2);

        var endEffector = new EndEffectorPose(
            X: Math.Round(x, 4),
            Y: Math.Round(y, 4),
            Z: Math.Round(z, 4),
            RollDegrees: Math.Round(rollDeg, 2),
            PitchDegrees: Math.Round(pitchDeg, 2),
            YawDegrees: Math.Round(yawDeg, 2),
            WithinWorkEnvelope: inEnvelope,
            TorqueLimitsExceeded: torqueExceeded);

        return new PalletizerKinematicsResult(
            ArmId: request.ArmId,
            EndEffector: endEffector,
            TotalPowerWatts: Math.Round(powerWatts, 2),
            CycleTimeSeconds: cycleTimeSec,
            SafetyInterlockTriggered: safetyTriggered);
    }

    public bool IsWithinWorkEnvelope(double x, double y, double z, double maxReachMeters = 2.5)
    {
        double distance = Math.Sqrt((x * x) + (y * y) + (z * z));
        return distance <= maxReachMeters && z >= -0.10; // Floor clearance
    }

    public double CalculateArmPowerConsumption(IReadOnlyList<DhJointParameters> joints, double jointAngularVelocityRadPerSec = 1.0)
    {
        if (joints == null || joints.Count == 0) return StandbyPowerWatts;

        double mechanicalPower = 0.0;
        foreach (var j in joints)
        {
            // P = tau * omega
            mechanicalPower += Math.Abs(j.CurrentTorqueNm) * jointAngularVelocityRadPerSec;
        }

        // Efficiency loss factor (~80% servo drive efficiency) + baseline standby power
        return (mechanicalPower / 0.80) + StandbyPowerWatts;
    }

    private static double[,] BuildDhTransformationMatrix(double thetaDeg, double d, double a, double alphaDeg)
    {
        double thetaRad = thetaDeg * (Math.PI / 180.0);
        double alphaRad = alphaDeg * (Math.PI / 180.0);

        double cosTheta = Math.Cos(thetaRad);
        double sinTheta = Math.Sin(thetaRad);
        double cosAlpha = Math.Cos(alphaRad);
        double sinAlpha = Math.Sin(alphaRad);

        return new double[4, 4]
        {
            { cosTheta, -sinTheta * cosAlpha,  sinTheta * sinAlpha, a * cosTheta },
            { sinTheta,  cosTheta * cosAlpha, -cosTheta * sinAlpha, a * sinTheta },
            { 0,         sinAlpha,             cosAlpha,            d },
            { 0,         0,                    0,                   1 }
        };
    }

    private static double[,] Multiply4x4(double[,] a, double[,] b)
    {
        double[,] result = new double[4, 4];
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                double sum = 0;
                for (int k = 0; k < 4; k++)
                {
                    sum += a[i, k] * b[k, j];
                }
                result[i, j] = sum;
            }
        }
        return result;
    }
}
