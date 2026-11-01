namespace SGrowing.Api.Modules.Commerce.Shrinkage;

/// <summary>
/// Type of physical asset tag affixed to inventory items.
/// </summary>
public enum AssetTagType
{
    ActiveBleBeacon = 0,
    PassiveRfidUhf = 1
}

/// <summary>
/// Logical and physical warehouse operational zones.
/// </summary>
public enum WarehouseZone
{
    HighValueAisle = 0,
    BulkStorageRack = 1,
    PackingStation = 2,
    DockExitGate = 3,
    QuarantineZone = 4
}

/// <summary>
/// Severity rating of shrinkage and inventory discrepancy incidents.
/// </summary>
public enum ShrinkageAlertSeverity
{
    InformationalMisplacement = 0,
    SuspiciousMovement = 1,
    CriticalTheftAlert = 2
}

/// <summary>
/// Lifecycle and interception dispatch status for dock perimeter security alerts.
/// </summary>
public enum SecurityInterceptStatus
{
    ActiveAlarm = 0,
    SecurityDispatched = 1,
    FalsePositiveCleared = 2,
    Recovered = 3
}

/// <summary>
/// Estimated physical coordinates of an asset inside the warehouse facility.
/// </summary>
public record IndoorLocationPoint(
    double X,
    double Y,
    double Z,
    int Floor,
    WarehouseZone Zone,
    double AccuracyMeters,
    DateTime TimestampUtc)
{
    /// <summary>
    /// Computes 2D Euclidean distance to another indoor location point.
    /// </summary>
    public double DistanceTo2D(IndoorLocationPoint other) =>
        Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));

    /// <summary>
    /// Computes 3D Euclidean distance to another indoor location point.
    /// </summary>
    public double DistanceTo3D(IndoorLocationPoint other) =>
        Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2) + Math.Pow(Z - other.Z, 2));

    /// <summary>
    /// Determines whether the coordinates fall within the dock exit gate perimeter boundary (X >= 90m and Y >= 90m).
    /// </summary>
    public bool IsInDockExitPerimeter() => X >= 90.0 && Y >= 90.0;
}

/// <summary>
/// Received Signal Strength Indication (RSSI) in dBm recorded by a fixed anchor receiver.
/// </summary>
public record RssiAnchorReading(
    string AnchorId,
    double RssiDb);

/// <summary>
/// Real-time multi-anchor telemetry payload broadcast by an RFID/BLE asset tag.
/// </summary>
public record RfidBleTelemetryBeacon(
    string TagId,
    string Sku,
    IReadOnlyList<RssiAnchorReading> RssiReadings,
    double BatteryPct,
    DateTime TimestampUtc,
    AssetTagType TagType = AssetTagType.ActiveBleBeacon);

/// <summary>
/// Calibrated reference location of a fixed ceiling or pillar anchor gateway.
/// </summary>
public record FixedAnchorNode(
    string AnchorId,
    double X,
    double Y,
    double Z,
    int Floor,
    WarehouseZone Zone,
    string? Model = null,
    bool IsActive = true);

/// <summary>
/// Discrepancy incident emitted when inventory deviates from planogram or moves without authorization.
/// </summary>
public record AisleDiscrepancyIncident(
    string IncidentId,
    string TagId,
    string Sku,
    WarehouseZone ExpectedZone,
    WarehouseZone ActualZone,
    double DistanceDeviationMeters,
    bool HasActivePickTask,
    DateTime DetectedAtUtc,
    ShrinkageAlertSeverity Severity = ShrinkageAlertSeverity.SuspiciousMovement,
    bool IsResolved = false,
    DateTime? ResolvedAtUtc = null,
    string? ResolutionNotes = null);

/// <summary>
/// Real-time high-priority security alarm emitted when high-value or unmanifested inventory enters dock exit perimeter.
/// </summary>
public record DockShrinkageSecurityAlert(
    string AlertId,
    string TagId,
    string Sku,
    decimal ItemValueDzd,
    string DockGateId,
    SecurityInterceptStatus Status,
    DateTime EmittedAtUtc,
    string InterceptNotes,
    ShrinkageAlertSeverity Severity = ShrinkageAlertSeverity.CriticalTheftAlert,
    IndoorLocationPoint? TriggerLocation = null);

// ==========================================
// API DTOs
// ==========================================

/// <summary>
/// Request payload for ingesting raw RSSI telemetry from IoT gateway receivers.
/// </summary>
public record IngestRssiRequest(
    string TagId,
    string Sku,
    IReadOnlyList<RssiAnchorReading> RssiReadings,
    double BatteryPct = 100.0,
    AssetTagType TagType = AssetTagType.ActiveBleBeacon,
    decimal? ItemValueDzd = null,
    WarehouseZone? ExpectedZone = null,
    bool HasActivePickTask = false,
    DateTime? TimestampUtc = null);

/// <summary>
/// Response payload after ingesting RSSI telemetry.
/// </summary>
public record IngestRssiResponse(
    bool Success,
    string TagId,
    IndoorLocationPoint Location,
    AisleDiscrepancyIncident? DiscrepancyIncident,
    DockShrinkageSecurityAlert? DockSecurityAlert,
    string Message);

/// <summary>
/// Request to register a new fixed anchor node in the positioning mesh.
/// </summary>
public record RegisterAnchorRequest(
    string AnchorId,
    double X,
    double Y,
    double Z,
    int Floor,
    WarehouseZone Zone,
    string? Model = null);

/// <summary>
/// Tracked asset location status response.
/// </summary>
public record AssetLocationResponse(
    string TagId,
    string Sku,
    IndoorLocationPoint Location,
    double BatteryPct,
    DateTime LastSeenUtc);

/// <summary>
/// Response payload auditing all active asset discrepancies against planograms.
/// </summary>
public record AuditDiscrepanciesResponse(
    bool HasDiscrepancies,
    IReadOnlyList<AisleDiscrepancyIncident> Incidents,
    int TotalAudited,
    DateTime TimestampUtc);

/// <summary>
/// Per-zone inventory distribution and risk metrics.
/// </summary>
public record WarehouseZoneShrinkageMetric(
    WarehouseZone Zone,
    int AssetCount,
    int DiscrepancyCount,
    int AlertCount);

/// <summary>
/// Warehouse-wide real-time shrinkage radar summary response.
/// </summary>
public record ShrinkageSummaryResponse(
    int TotalTrackedAssets,
    int ActiveDiscrepanciesCount,
    int ActiveDockAlertsCount,
    decimal TotalAtRiskValueDzd,
    IReadOnlyList<WarehouseZoneShrinkageMetric> ZoneMetrics,
    DateTime GeneratedAtUtc);

/// <summary>
/// Request to resolve an aisle discrepancy incident.
/// </summary>
public record ResolveDiscrepancyRequest(
    string? ResolutionNotes = null);

/// <summary>
/// Request to acknowledge or advance the security status of a dock alert.
/// </summary>
public record AcknowledgeAlertRequest(
    SecurityInterceptStatus NewStatus,
    string? Notes = null);
