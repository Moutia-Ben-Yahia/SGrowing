namespace SGrowing.Api.Modules.Logistics.ColdChain;

/// <summary>
/// Satellite telemetry and polygonal geo-fence boundary tracking service.
/// Provides Ray-Casting collision detection and cryptographic HMAC-SHA256 notarization tokens.
/// </summary>
public interface ISatelliteGeofenceTrackerService
{
    /// <summary>
    /// Registers a polygonal satellite geo-fence zone.
    /// </summary>
    void RegisterGeofence(PolygonalGeofence geofence);

    /// <summary>
    /// Evaluates a geographic coordinate for a consignment, detecting perimeter transitions (Entered/Exited)
    /// and generating tamper-proof cryptographic notarization proofs.
    /// </summary>
    IReadOnlyList<GeofenceTransitionEvent> EvaluateCoordinate(
        string consignmentId,
        double latitude,
        double longitude,
        DateTime timestampUtc,
        string? secretKey = null);

    /// <summary>
    /// Returns all registered active satellite geo-fences.
    /// </summary>
    IReadOnlyList<PolygonalGeofence> GetActiveGeofences();

    /// <summary>
    /// Retrieves a specific geo-fence by its identifier.
    /// </summary>
    PolygonalGeofence? GetGeofence(string geofenceId);

    /// <summary>
    /// Evaluates whether a coordinate point lies inside a specific geo-fence.
    /// </summary>
    bool IsInsideGeofence(string geofenceId, double latitude, double longitude);

    /// <summary>
    /// Retrieves chronological geo-fence transition events for a consignment.
    /// </summary>
    IReadOnlyList<GeofenceTransitionEvent> GetTransitionHistory(string consignmentId);

    /// <summary>
    /// Cryptographically verifies a transition notarization token against a secret key.
    /// </summary>
    bool VerifyNotarizationProof(GeofenceTransitionEvent transitionEvent, string? secretKey = null);

    /// <summary>
    /// Clears registered geo-fences and consignment states for testing or maintenance.
    /// </summary>
    void Clear(string? consignmentId = null);
}
