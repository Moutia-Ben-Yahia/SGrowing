namespace SGrowing.Api.Modules.Commerce.Robotics;

/// <summary>
/// Autonomous Mobile Robot (AMR) Multi-Pick Route Optimization Service.
/// Implements 2-Opt Traveling Salesperson Problem (TSP) optimization with
/// Nearest-Neighbor initialization to minimize warehouse travel distance.
/// </summary>
public class WarehousePickRouterService : IWarehousePickRouterService
{
    private const int MaxTwoOptIterations = 1000;
    private const double ImprovementEpsilon = 1e-6;

    /// <inheritdoc />
    public AmrPickRoute OptimizePickRoute(
        WarehouseBinLocation startLocation,
        IReadOnlyList<WarehouseBinLocation> pickLocations,
        double amrSpeedMetersPerSec = 1.5,
        double pickDwellTimeSeconds = 12.0)
    {
        var (route, _, _) = BenchmarkRoute(
            startLocation,
            pickLocations,
            amrSpeedMetersPerSec,
            pickDwellTimeSeconds);

        return route;
    }

    /// <inheritdoc />
    public (AmrPickRoute Route, double NaiveDistanceMeters, double DistanceSavedPercentage) BenchmarkRoute(
        WarehouseBinLocation startLocation,
        IReadOnlyList<WarehouseBinLocation> pickLocations,
        double amrSpeedMetersPerSec = 1.5,
        double pickDwellTimeSeconds = 12.0)
    {
        double speed = amrSpeedMetersPerSec > 0 ? amrSpeedMetersPerSec : 1.5;
        double dwell = pickDwellTimeSeconds >= 0 ? pickDwellTimeSeconds : 12.0;

        if (pickLocations.Count == 0)
        {
            var emptyRoute = new AmrPickRoute(
                RouteId: $"route_{Guid.NewGuid():N}"[..14],
                TotalDistanceMeters: 0.0,
                EstimatedDurationSeconds: 0.0,
                Waypoints: [startLocation],
                OptimizedPickSequence: []);

            return (emptyRoute, 0.0, 0.0);
        }

        // Calculate naive route distance (order as provided)
        double naiveDistance = CalculateTotalDistance(startLocation, pickLocations);

        if (pickLocations.Count == 1)
        {
            double dist = Math.Round(naiveDistance, 2);
            double duration = Math.Round((dist / speed) + dwell, 1);
            var singleRoute = new AmrPickRoute(
                RouteId: $"route_{Guid.NewGuid():N}"[..14],
                TotalDistanceMeters: dist,
                EstimatedDurationSeconds: duration,
                Waypoints: [startLocation, pickLocations[0]],
                OptimizedPickSequence: [pickLocations[0].LocationId]);

            return (singleRoute, dist, 0.0);
        }

        // 1. Initial heuristic: Nearest-Neighbor from startLocation
        var unvisited = new List<WarehouseBinLocation>(pickLocations);
        var initialRoute = new List<WarehouseBinLocation>(pickLocations.Count);

        var current = startLocation;
        while (unvisited.Count > 0)
        {
            int nearestIdx = 0;
            double nearestDist = double.MaxValue;

            for (int i = 0; i < unvisited.Count; i++)
            {
                double d = current.DistanceTo(unvisited[i]);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearestIdx = i;
                }
            }

            var next = unvisited[nearestIdx];
            initialRoute.Add(next);
            current = next;
            unvisited.RemoveAt(nearestIdx);
        }

        // 2. 2-Opt local search refinement on open path
        var optimizedPicks = Solve2Opt(startLocation, initialRoute);
        double optimizedDistance = CalculateTotalDistance(startLocation, optimizedPicks);

        // Safeguard: never return a route worse than naive input
        if (naiveDistance < optimizedDistance)
        {
            optimizedPicks = [.. pickLocations];
            optimizedDistance = naiveDistance;
        }

        double distanceSavedPercentage = 0.0;
        if (naiveDistance > ImprovementEpsilon)
        {
            distanceSavedPercentage = Math.Max(0.0, Math.Round(((naiveDistance - optimizedDistance) / naiveDistance) * 100.0, 2));
        }

        double travelDuration = optimizedDistance / speed;
        double pickDwellDuration = pickLocations.Count * dwell;
        double totalDurationSeconds = Math.Round(travelDuration + pickDwellDuration, 1);

        var waypoints = new List<WarehouseBinLocation>(pickLocations.Count + 1)
        {
            startLocation
        };
        waypoints.AddRange(optimizedPicks);

        var resultRoute = new AmrPickRoute(
            RouteId: $"route_{Guid.NewGuid():N}"[..14],
            TotalDistanceMeters: Math.Round(optimizedDistance, 2),
            EstimatedDurationSeconds: totalDurationSeconds,
            Waypoints: waypoints,
            OptimizedPickSequence: optimizedPicks.Select(p => p.LocationId).ToList());

        return (resultRoute, Math.Round(naiveDistance, 2), distanceSavedPercentage);
    }

    /// <inheritdoc />
    public double CalculateTotalDistance(
        WarehouseBinLocation startLocation,
        IReadOnlyList<WarehouseBinLocation> sequence)
    {
        if (sequence.Count == 0) return 0.0;

        double distance = startLocation.DistanceTo(sequence[0]);
        for (int i = 0; i < sequence.Count - 1; i++)
        {
            distance += sequence[i].DistanceTo(sequence[i + 1]);
        }

        return distance;
    }

    private static List<WarehouseBinLocation> Solve2Opt(
        WarehouseBinLocation start,
        List<WarehouseBinLocation> picks)
    {
        int n = picks.Count;
        if (n <= 2) return picks;

        var route = new List<WarehouseBinLocation>(picks);
        bool improved = true;
        int iterations = 0;

        while (improved && iterations < MaxTwoOptIterations)
        {
            improved = false;
            iterations++;

            for (int i = 0; i < n - 1; i++)
            {
                var prevPoint = (i == 0) ? start : route[i - 1];
                var curPoint = route[i];

                for (int j = i + 1; j < n; j++)
                {
                    var targetPoint = route[j];
                    var nextPoint = (j == n - 1) ? null : route[j + 1];

                    double currentEdge1 = prevPoint.DistanceTo(curPoint);
                    double currentEdge2 = nextPoint is not null ? targetPoint.DistanceTo(nextPoint) : 0.0;

                    double newEdge1 = prevPoint.DistanceTo(targetPoint);
                    double newEdge2 = nextPoint is not null ? curPoint.DistanceTo(nextPoint) : 0.0;

                    double delta = (newEdge1 + newEdge2) - (currentEdge1 + currentEdge2);

                    if (delta < -ImprovementEpsilon)
                    {
                        // Reverse segment between i and j
                        route.Reverse(i, j - i + 1);
                        improved = true;
                        break;
                    }
                }

                if (improved) break;
            }
        }

        return route;
    }
}
