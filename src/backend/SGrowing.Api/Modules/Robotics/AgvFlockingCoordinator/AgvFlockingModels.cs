namespace SGrowing.Api.Modules.Robotics.AgvFlockingCoordinator;

public sealed record Vector2D(
    double X,
    double Y
)
{
    public double Magnitude => Math.Sqrt((X * X) + (Y * Y));

    public Vector2D Normalize()
    {
        var mag = Magnitude;
        return mag > 0.0001 ? new Vector2D(X / mag, Y / mag) : new Vector2D(0, 0);
    }

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2D operator *(Vector2D a, double s) => new(a.X * s, a.Y * s);
    public static Vector2D operator /(Vector2D a, double s) => new(a.X / s, a.Y / s);
}

public sealed record AgvRobotAgent(
    string AgvId,
    Vector2D CurrentPosition,
    Vector2D CurrentVelocity,
    Vector2D GoalPosition,
    double RadiusMeters = 0.60,
    double MaxSpeedMps = 2.0,
    double BatterySoC = 0.85
);

public sealed record AgvAvoidanceStep(
    string AgvId,
    Vector2D PreferredVelocity,
    Vector2D SafeAdjustedVelocity,
    bool AvoidanceManeuverActive,
    string? ObstacleAgvId,
    double SeparationDistanceMeters
);

public sealed record FleetFlockingSimulationResult(
    string SimulationStepId,
    int TotalAgvsCoordinated,
    int PotentialCollisionsResolved,
    double AverageFleetSpeedMps,
    IReadOnlyList<AgvAvoidanceStep> AgvDispatches,
    DateTime SimulatedAtUtc
);

public sealed record CoordinateAgvFleetRequest(
    double TimeHorizonSeconds,
    IReadOnlyList<AgvRobotAgent> Agvs
);
