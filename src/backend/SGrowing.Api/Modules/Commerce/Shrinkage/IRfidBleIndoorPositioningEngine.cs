namespace SGrowing.Api.Modules.Commerce.Shrinkage;

/// <summary>
/// Service interface for real-time indoor positioning of RFID/BLE asset tags
/// using log-distance path loss propagation and linear least-squares 2D trilateration.
/// </summary>
public interface IRfidBleIndoorPositioningEngine
{
    /// <summary>
    /// Registers or updates a fixed anchor node gateway in the positioning mesh.
    /// </summary>
    void RegisterAnchor(FixedAnchorNode anchor);

    /// <summary>
    /// Retrieves a registered anchor node by its unique identifier.
    /// </summary>
    FixedAnchorNode? GetAnchor(string anchorId);

    /// <summary>
    /// Retrieves all currently registered anchor nodes.
    /// </summary>
    IReadOnlyList<FixedAnchorNode> GetAllAnchors();

    /// <summary>
    /// Estimates the current 2D/3D physical indoor location of an asset from incoming multi-anchor RSSI telemetry.
    /// </summary>
    IndoorLocationPoint EstimateLocation(RfidBleTelemetryBeacon beacon);

    /// <summary>
    /// Retrieves the last recorded location point for a specific asset tag.
    /// </summary>
    IndoorLocationPoint? GetAssetLocation(string tagId);

    /// <summary>
    /// Retrieves all currently tracked assets, their latest location points, SKU, and battery levels.
    /// </summary>
    IReadOnlyDictionary<string, (IndoorLocationPoint Location, string Sku, double BatteryPct)> GetAllTrackedAssets();

    /// <summary>
    /// Converts a received signal strength indication (RSSI in dBm) to estimated physical distance in meters
    /// using the log-distance path loss propagation model.
    /// </summary>
    double CalculateDistanceMeters(double rssiDb, double txPowerA = -59.0, double pathLossExponent = 2.2);

    /// <summary>
    /// Resolves the warehouse operational zone containing the given 2D coordinates.
    /// </summary>
    WarehouseZone ResolveZone(double x, double y);
}
