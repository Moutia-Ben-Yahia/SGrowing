using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Security.ZtnaContinuousPosture;

/// <summary>
/// Autonomous geovelocity anomaly detector identifying impossible travel patterns
/// exceeding standard commercial flight velocities (>900 km/h) between consecutive access events.
/// </summary>
public sealed class GeovelocityAnomalyDetectorService
{
    private const double MaxFeasibleVelocityKmH = 900.0; // Commercial airliner cruise limit
    private readonly ConcurrentDictionary<string, (double Lat, double Lon, DateTime TimestampUtc)> _lastLocations = new(StringComparer.OrdinalIgnoreCase);

    public GeovelocityCheckResult CheckVelocity(string sessionId, double lat, double lon, DateTime timestampUtc)
    {
        if (!_lastLocations.TryGetValue(sessionId, out var previous))
        {
            _lastLocations[sessionId] = (lat, lon, timestampUtc);
            return new GeovelocityCheckResult(
                IsImpossibleTravel: false,
                DistanceKm: 0.0,
                TimeElapsed: TimeSpan.Zero,
                ImpliedVelocityKmH: 0.0,
                Message: "First observed geographic location registered."
            );
        }

        var distKm = CalculateHaversineDistanceKm(previous.Lat, previous.Lon, lat, lon);
        var elapsed = timestampUtc - previous.TimestampUtc;

        // Prevent division by zero or negative elapsed time
        var hoursElapsed = Math.Max(0.000277, elapsed.TotalHours); // min 1 second
        var velocity = distKm / hoursElapsed;

        var isImpossible = distKm > 50.0 && velocity > MaxFeasibleVelocityKmH;

        // Update record if valid or impossible travel flagged
        _lastLocations[sessionId] = (lat, lon, timestampUtc);

        var msg = isImpossible
            ? $"IMPOSSIBLE TRAVEL: Displaced {distKm:F1} km in {elapsed.TotalMinutes:F1} minutes (implied velocity {velocity:F0} km/h > {MaxFeasibleVelocityKmH} km/h)."
            : $"Feasible displacement ({distKm:F1} km at {velocity:F0} km/h).";

        return new GeovelocityCheckResult(
            IsImpossibleTravel: isImpossible,
            DistanceKm: Math.Round(distKm, 2),
            TimeElapsed: elapsed,
            ImpliedVelocityKmH: Math.Round(velocity, 1),
            Message: msg
        );
    }

    private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return R * c;
    }
}
