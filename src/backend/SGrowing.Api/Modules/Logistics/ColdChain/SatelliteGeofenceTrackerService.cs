using System.Security.Cryptography;
using System.Text;

namespace SGrowing.Api.Modules.Logistics.ColdChain;

/// <summary>
/// Thread-safe satellite geo-fence tracking engine powered by the Ray-Casting algorithm
/// and HMAC-SHA256 notarization tokens for customs and bonded corridors.
/// </summary>
public sealed class SatelliteGeofenceTrackerService : ISatelliteGeofenceTrackerService
{
    private const string DefaultNotaryKey = "sgrowing-satellite-oracle-secret-key-2026";
    private readonly Lock _syncLock = new();
    private readonly Dictionary<string, PolygonalGeofence> _geofences = [];
    private readonly Dictionary<(string ConsignmentId, string GeofenceId), bool> _presenceState = [];
    private readonly Dictionary<string, List<GeofenceTransitionEvent>> _transitionHistory = [];

    /// <inheritdoc />
    public void RegisterGeofence(PolygonalGeofence geofence)
    {
        ArgumentNullException.ThrowIfNull(geofence);
        if (string.IsNullOrWhiteSpace(geofence.GeofenceId))
        {
            throw new ArgumentException("GeofenceId cannot be empty.", nameof(geofence));
        }

        if (geofence.Coordinates.Count < 3)
        {
            throw new ArgumentException("Polygonal geofence requires at least 3 vertices.", nameof(geofence));
        }

        lock (_syncLock)
        {
            _geofences[geofence.GeofenceId] = geofence;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<GeofenceTransitionEvent> EvaluateCoordinate(
        string consignmentId,
        double latitude,
        double longitude,
        DateTime timestampUtc,
        string? secretKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);

        var events = new List<GeofenceTransitionEvent>();
        var keyToUse = string.IsNullOrWhiteSpace(secretKey) ? DefaultNotaryKey : secretKey;

        lock (_syncLock)
        {
            foreach (var geofence in _geofences.Values.Where(g => g.IsActive))
            {
                var isCurrentlyInside = IsPointInPolygon(latitude, longitude, geofence.Coordinates);
                var stateKey = (consignmentId, geofence.GeofenceId);
                var hasPreviousState = _presenceState.TryGetValue(stateKey, out var wasInside);

                string? transition = null;
                if (hasPreviousState)
                {
                    if (!wasInside && isCurrentlyInside)
                    {
                        transition = nameof(GeofenceTransitionType.Entered);
                    }
                    else if (wasInside && !isCurrentlyInside)
                    {
                        transition = nameof(GeofenceTransitionType.Exited);
                    }
                }
                else
                {
                    if (isCurrentlyInside)
                    {
                        transition = nameof(GeofenceTransitionType.Entered);
                    }
                }

                _presenceState[stateKey] = isCurrentlyInside;

                if (transition is not null)
                {
                    var proof = GenerateNotarizationProof(
                        consignmentId,
                        geofence.GeofenceId,
                        transition,
                        latitude,
                        longitude,
                        timestampUtc,
                        keyToUse);

                    var transitionEvent = new GeofenceTransitionEvent(
                        EventId: $"GEO-EVT-{Guid.NewGuid():N}",
                        ConsignmentId: consignmentId,
                        GeofenceId: geofence.GeofenceId,
                        GeofenceName: geofence.Name,
                        TransitionType: transition,
                        TimestampUtc: timestampUtc,
                        CryptographicNotarizationProof: proof,
                        Latitude: latitude,
                        Longitude: longitude);

                    if (!_transitionHistory.TryGetValue(consignmentId, out var history))
                    {
                        history = [];
                        _transitionHistory[consignmentId] = history;
                    }

                    history.Add(transitionEvent);
                    events.Add(transitionEvent);
                }
            }
        }

        return events;
    }

    /// <inheritdoc />
    public IReadOnlyList<PolygonalGeofence> GetActiveGeofences()
    {
        lock (_syncLock)
        {
            return _geofences.Values.Where(g => g.IsActive).ToList();
        }
    }

    /// <inheritdoc />
    public PolygonalGeofence? GetGeofence(string geofenceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(geofenceId);

        lock (_syncLock)
        {
            return _geofences.GetValueOrDefault(geofenceId);
        }
    }

    /// <inheritdoc />
    public bool IsInsideGeofence(string geofenceId, double latitude, double longitude)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(geofenceId);

        lock (_syncLock)
        {
            if (!_geofences.TryGetValue(geofenceId, out var geofence) || !geofence.IsActive)
            {
                return false;
            }

            return IsPointInPolygon(latitude, longitude, geofence.Coordinates);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<GeofenceTransitionEvent> GetTransitionHistory(string consignmentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consignmentId);

        lock (_syncLock)
        {
            if (!_transitionHistory.TryGetValue(consignmentId, out var history))
            {
                return [];
            }

            return history.OrderBy(e => e.TimestampUtc).ToList();
        }
    }

    /// <inheritdoc />
    public bool VerifyNotarizationProof(GeofenceTransitionEvent transitionEvent, string? secretKey = null)
    {
        ArgumentNullException.ThrowIfNull(transitionEvent);

        var keyToUse = string.IsNullOrWhiteSpace(secretKey) ? DefaultNotaryKey : secretKey;
        var expectedProof = GenerateNotarizationProof(
            transitionEvent.ConsignmentId,
            transitionEvent.GeofenceId,
            transitionEvent.TransitionType,
            transitionEvent.Latitude,
            transitionEvent.Longitude,
            transitionEvent.TimestampUtc,
            keyToUse);

        var expectedBytes = Encoding.UTF8.GetBytes(expectedProof);
        var actualBytes = Encoding.UTF8.GetBytes(transitionEvent.CryptographicNotarizationProof);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    /// <inheritdoc />
    public void Clear(string? consignmentId = null)
    {
        lock (_syncLock)
        {
            if (string.IsNullOrWhiteSpace(consignmentId))
            {
                _geofences.Clear();
                _presenceState.Clear();
                _transitionHistory.Clear();
            }
            else
            {
                _transitionHistory.Remove(consignmentId);
                var keysToRemove = _presenceState.Keys.Where(k => k.ConsignmentId == consignmentId).ToList();
                foreach (var k in keysToRemove)
                {
                    _presenceState.Remove(k);
                }
            }
        }
    }

    /// <summary>
    /// Executes the Jordan Curve Theorem (Ray-Casting) point-in-polygon algorithm.
    /// Also considers points on vertices or edges as inside.
    /// </summary>
    public static bool IsPointInPolygon(double lat, double lon, IReadOnlyList<GeoCoordinate> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return false;
        }

        bool inside = false;
        int count = vertices.Count;
        int j = count - 1;

        for (int i = 0; i < count; i++)
        {
            var pi = vertices[i];
            var pj = vertices[j];

            // Vertex coincidence check
            if (Math.Abs(pi.Latitude - lat) < 1e-9 && Math.Abs(pi.Longitude - lon) < 1e-9)
            {
                return true;
            }

            // Collinear segment boundary check
            double minLat = Math.Min(pi.Latitude, pj.Latitude);
            double maxLat = Math.Max(pi.Latitude, pj.Latitude);
            double minLon = Math.Min(pi.Longitude, pj.Longitude);
            double maxLon = Math.Max(pi.Longitude, pj.Longitude);

            if (lat >= minLat - 1e-9 && lat <= maxLat + 1e-9 && lon >= minLon - 1e-9 && lon <= maxLon + 1e-9)
            {
                double crossProduct = (lat - pi.Latitude) * (pj.Longitude - pi.Longitude) - (lon - pi.Longitude) * (pj.Latitude - pi.Latitude);
                if (Math.Abs(crossProduct) < 1e-9)
                {
                    return true;
                }
            }

            // Horizontal ray casting test
            if (((pi.Latitude > lat) != (pj.Latitude > lat)) &&
                (lon < (pj.Longitude - pi.Longitude) * (lat - pi.Latitude) / (pj.Latitude - pi.Latitude) + pi.Longitude))
            {
                inside = !inside;
            }

            j = i;
        }

        return inside;
    }

    private static string GenerateNotarizationProof(
        string consignmentId,
        string geofenceId,
        string transition,
        double latitude,
        double longitude,
        DateTime timestampUtc,
        string secretKey)
    {
        var payload = $"CONSIGNMENT:{consignmentId}|GEOFENCE:{geofenceId}|TRANSITION:{transition}|LAT:{latitude:F6}|LON:{longitude:F6}|TIME:{timestampUtc:O}";
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
