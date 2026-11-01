using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Logistics.Routing;

/// <summary>
/// Real-time meteorological hazard radar monitoring Sirocco sandstorms, flash floods,
/// high Atlas snow passes, and extreme heatwaves across the Algerian transit network.
/// </summary>
public sealed class DynamicWeatherHazardRadarService
{
    private readonly ConcurrentDictionary<string, WeatherHazardAlert> _activeHazards = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers or updates an active weather hazard alert.
    /// </summary>
    public void RegisterHazard(WeatherHazardAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        _activeHazards[alert.HazardId] = alert;
    }

    /// <summary>
    /// Removes a resolved or canceled hazard alert.
    /// </summary>
    public bool RevokeHazard(string hazardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hazardId);
        return _activeHazards.TryRemove(hazardId, out _);
    }

    /// <summary>
    /// Returns all currently unexpired weather hazards.
    /// </summary>
    public IReadOnlyList<WeatherHazardAlert> GetActiveHazards(DateTime? asOfUtc = null)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        return _activeHazards.Values
            .Where(h => !h.IsExpired(now))
            .OrderByDescending(h => h.Severity)
            .ToList();
    }

    /// <summary>
    /// Evaluates whether a given transport edge is impacted by active weather hazards.
    /// </summary>
    public (bool IsImpaired, bool IsBlocked, double EffectiveSpeedKmh, string? HazardId) EvaluateEdgeHazard(
        RoutingEdge edge,
        RoutingNode fromNode,
        RoutingNode toNode,
        DateTime? asOfUtc = null)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(fromNode);
        ArgumentNullException.ThrowIfNull(toNode);

        var now = asOfUtc ?? DateTime.UtcNow;
        var activeHazards = GetActiveHazards(now);

        var effectiveSpeed = edge.BaseSpeedKmh;
        string? activeHazardId = null;
        var isBlocked = false;
        var isImpaired = false;

        foreach (var hazard in activeHazards)
        {
            if (!hazard.AffectsMode(edge.Mode))
            {
                continue;
            }

            // Check if hazard affects either endpoint or edge midpoint
            var midpoint = new GeoCoordinate(
                (fromNode.Location.Latitude + toNode.Location.Latitude) / 2.0,
                (fromNode.Location.Longitude + toNode.Location.Longitude) / 2.0
            );

            if (hazard.AffectsLocation(fromNode.Location) ||
                hazard.AffectsLocation(toNode.Location) ||
                hazard.AffectsLocation(midpoint))
            {
                isImpaired = true;
                activeHazardId = hazard.HazardId;

                if (hazard.Severity == HazardSeverity.CriticalBlocked || hazard.SpeedReductionFactor <= 0.05)
                {
                    isBlocked = true;
                    effectiveSpeed = 0.0;
                    break;
                }

                effectiveSpeed = Math.Min(effectiveSpeed, edge.BaseSpeedKmh * Math.Clamp(hazard.SpeedReductionFactor, 0.1, 1.0));
            }
        }

        return (isImpaired, isBlocked, effectiveSpeed, activeHazardId);
    }

    /// <summary>
    /// Purges expired hazards from the active radar cache.
    /// </summary>
    public int ClearExpiredHazards(DateTime? asOfUtc = null)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        var expiredKeys = _activeHazards.Values
            .Where(h => h.IsExpired(now))
            .Select(h => h.HazardId)
            .ToList();

        var count = 0;
        foreach (var key in expiredKeys)
        {
            if (_activeHazards.TryRemove(key, out _))
            {
                count++;
            }
        }

        return count;
    }
}
