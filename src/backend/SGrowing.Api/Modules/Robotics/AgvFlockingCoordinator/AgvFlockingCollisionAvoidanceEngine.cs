namespace SGrowing.Api.Modules.Robotics.AgvFlockingCoordinator;

/// <summary>
/// Autonomous space-constrained multi-agent AGV flocking and Reciprocal Velocity Obstacle (ORCA) engine.
/// Computes collision-free velocity vectors for robotic warehouse fleets in real time.
/// </summary>
public sealed class AgvFlockingCollisionAvoidanceEngine
{
    public FleetFlockingSimulationResult CoordinateFleetVelocity(CoordinateAgvFleetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tau = request.TimeHorizonSeconds > 0.5 ? request.TimeHorizonSeconds : 2.5;
        var agvs = request.Agvs;
        var dispatches = new List<AgvAvoidanceStep>();
        var collisionCount = 0;

        for (var i = 0; i < agvs.Count; i++)
        {
            var a = agvs[i];

            // 1. Compute Preferred Velocity towards Goal
            var toGoal = a.GoalPosition - a.CurrentPosition;
            var distToGoal = toGoal.Magnitude;

            var vPref = distToGoal > 0.05
                ? toGoal.Normalize() * Math.Min(a.MaxSpeedMps, distToGoal)
                : new Vector2D(0, 0);

            var vSafe = vPref;
            var avoidanceActive = false;
            string? obstacleId = null;
            var minSeparation = double.MaxValue;

            // 2. Evaluate Pairwise Intersections with other AGVs
            for (var j = 0; j < agvs.Count; j++)
            {
                if (i == j) continue;

                var b = agvs[j];
                var pBA = b.CurrentPosition - a.CurrentPosition;
                var dist = pBA.Magnitude;
                var combinedRadius = a.RadiusMeters + b.RadiusMeters + 0.15; // 15cm safety cushion

                if (dist < minSeparation)
                {
                    minSeparation = dist;
                }

                // A. Direct Proximity Collision (Overlapping or touching)
                if (dist < combinedRadius)
                {
                    avoidanceActive = true;
                    obstacleId = b.AgvId;
                    collisionCount++;

                    // Strong repulsive velocity pushing away from B
                    var pushAway = pBA.Normalize() * -1.0;
                    vSafe = (vPref * 0.3) + (pushAway * (a.MaxSpeedMps * 0.7));
                    break;
                }

                // B. Velocity Obstacle (VO) projection within time horizon tau
                var vRel = vSafe - b.CurrentVelocity;
                var timeToClosest = (pBA.X * vRel.X + pBA.Y * vRel.Y) / Math.Max(0.001, vRel.Magnitude * vRel.Magnitude);

                if (timeToClosest > 0 && timeToClosest <= tau)
                {
                    var closestPoint = pBA - (vRel * timeToClosest);
                    if (closestPoint.Magnitude < combinedRadius)
                    {
                        avoidanceActive = true;
                        obstacleId = b.AgvId;
                        collisionCount++;

                        // Reciprocal evasion: compute lateral deflection vector
                        var perp = new Vector2D(-pBA.Y, pBA.X).Normalize();
                        var evasionSpeed = (combinedRadius - closestPoint.Magnitude) / timeToClosest;

                        // Add 50% reciprocal responsibility
                        vSafe = vSafe + (perp * (evasionSpeed * 0.5));
                    }
                }
            }

            // Clamp velocity to robot maximum physical speed
            if (vSafe.Magnitude > a.MaxSpeedMps)
            {
                vSafe = vSafe.Normalize() * a.MaxSpeedMps;
            }

            dispatches.Add(new AgvAvoidanceStep(
                AgvId: a.AgvId,
                PreferredVelocity: vPref,
                SafeAdjustedVelocity: vSafe,
                AvoidanceManeuverActive: avoidanceActive,
                ObstacleAgvId: obstacleId,
                SeparationDistanceMeters: Math.Round(minSeparation == double.MaxValue ? 100.0 : minSeparation, 2)
            ));
        }

        var avgSpeed = dispatches.Count > 0
            ? dispatches.Average(d => d.SafeAdjustedVelocity.Magnitude)
            : 0.0;

        return new FleetFlockingSimulationResult(
            SimulationStepId: $"SIM-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            TotalAgvsCoordinated: agvs.Count,
            PotentialCollisionsResolved: collisionCount,
            AverageFleetSpeedMps: Math.Round(avgSpeed, 2),
            AgvDispatches: dispatches,
            SimulatedAtUtc: DateTime.UtcNow
        );
    }
}
