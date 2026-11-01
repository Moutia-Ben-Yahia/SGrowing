namespace SGrowing.Api.Modules.Logistics.ColdChain;

/// <summary>
/// Identifies the sensitive commodity category requiring cold-chain monitoring.
/// </summary>
public enum CargoType
{
    VaccinesPharma,
    FrozenFood,
    ChilledFreshProduce,
    BiotechReagents,
    LuxuryCosmetics
}

/// <summary>
/// Real-time thermal integrity and regulatory compliance status of a monitored cargo shipment.
/// </summary>
public enum ThermalComplianceStatus
{
    Optimal,
    MinorThermalExcursion,
    SevereExcursionBreached,
    Spoiled
}

/// <summary>
/// Operational and regulatory classification of satellite geo-fenced zones.
/// </summary>
public enum GeofenceType
{
    CustomsBondedCorridor,
    FreeTradePort,
    WarehouseDock,
    RestrictedBorderZone
}

/// <summary>
/// Lifecycle status of an automated smart parametric cargo insurance claim.
/// </summary>
public enum InsuranceClaimStatus
{
    PendingInvestigation,
    AutoApprovedLiquidated,
    Rejected,
    Settled
}

/// <summary>
/// Type of parametric policy breach detected by IoT or satellite oracle feeds.
/// </summary>
public enum BreachType
{
    ThermalExcursion,
    ShockImpact,
    UnscheduledDelay
}

/// <summary>
/// Boundary transition type when crossing satellite geo-fence perimeters.
/// </summary>
public enum GeofenceTransitionType
{
    Entered,
    Exited
}

/// <summary>
/// Geographic coordinate pair (Latitude, Longitude) in decimal degrees.
/// </summary>
public sealed record GeoCoordinate(double Latitude, double Longitude)
{
    public static implicit operator GeoCoordinate((double Lat, double Lon) point) => new(point.Lat, point.Lon);
    public static implicit operator (double Lat, double Lon)(GeoCoordinate coord) => (coord.Latitude, coord.Longitude);
}

/// <summary>
/// High-frequency environmental and spatial telemetry point streamed from cargo container IoT loggers.
/// </summary>
public sealed record ColdChainTelemetryPoint(
    string ConsignmentId,
    string SensorId,
    DateTime TimestampUtc,
    double TemperatureCelsius,
    double RelativeHumidityPct,
    double ShockGForce,
    double LightLux,
    double BatteryPct,
    double Latitude,
    double Longitude);

/// <summary>
/// Thermal safe-operating envelope and chemical activation energy profile for a cargo type.
/// </summary>
public sealed record CargoThermalProfile(
    CargoType CargoType,
    double MinTempCelsius,
    double MaxTempCelsius,
    double MaxExcursionMinutes,
    double ActivationEnergyJPerMol = 83144.0)
{
    /// <summary>
    /// Factory providing standard WHO and ISO cold-chain thermal guidelines for regulated cargo categories.
    /// </summary>
    public static CargoThermalProfile ForCargoType(CargoType type) => type switch
    {
        CargoType.VaccinesPharma => new(CargoType.VaccinesPharma, 2.0, 8.0, 30.0, 83144.0),
        CargoType.FrozenFood => new(CargoType.FrozenFood, -25.0, -18.0, 60.0, 83144.0),
        CargoType.ChilledFreshProduce => new(CargoType.ChilledFreshProduce, 0.0, 4.0, 120.0, 83144.0),
        CargoType.BiotechReagents => new(CargoType.BiotechReagents, -80.0, -20.0, 15.0, 83144.0),
        CargoType.LuxuryCosmetics => new(CargoType.LuxuryCosmetics, 15.0, 25.0, 180.0, 83144.0),
        _ => new(type, 2.0, 8.0, 30.0, 83144.0)
    };
}

/// <summary>
/// Statistical and regulatory Mean Kinetic Temperature (MKT) evaluation result.
/// </summary>
public sealed record MeanKineticTemperatureResult(
    string ConsignmentId,
    double MktCelsius,
    int TotalPoints,
    double ExcursionDurationMinutes,
    ThermalComplianceStatus ComplianceStatus,
    double MinRecordedTempCelsius = 0.0,
    double MaxRecordedTempCelsius = 0.0);

/// <summary>
/// Polygonal satellite geo-fence boundary enclosing customs, ports, or logistics facilities.
/// </summary>
public sealed record PolygonalGeofence(
    string GeofenceId,
    string Name,
    GeofenceType Type,
    IReadOnlyList<GeoCoordinate> Coordinates,
    bool IsActive = true)
{
    public PolygonalGeofence(
        string geofenceId,
        string name,
        GeofenceType type,
        IEnumerable<(double Lat, double Lon)> coordinates,
        bool isActive = true)
        : this(geofenceId, name, type, coordinates.Select(c => new GeoCoordinate(c.Lat, c.Lon)).ToList(), isActive)
    {
    }
}

/// <summary>
/// Satellite notarization event generated upon crossing a designated polygonal geo-fence boundary.
/// </summary>
public sealed record GeofenceTransitionEvent(
    string EventId,
    string ConsignmentId,
    string GeofenceId,
    string GeofenceName,
    string TransitionType,
    DateTime TimestampUtc,
    string CryptographicNotarizationProof,
    double Latitude = 0.0,
    double Longitude = 0.0);

/// <summary>
/// Immutable parametric cargo insurance claim generated autonomously upon contract breach.
/// </summary>
public sealed record ParametricInsuranceClaim(
    string ClaimId,
    string ConsignmentId,
    string PolicyNumber,
    string BreachType,
    double BreachedValue,
    double ThresholdValue,
    decimal LiquidationAmountDzd,
    string PayoutRecipientId,
    InsuranceClaimStatus Status,
    DateTime? LiquidatedAtUtc,
    bool BuyerCodObligationVoided = false,
    string? LiquidationCertificate = null,
    string? Reason = null);

/// <summary>
/// Ingestion confirmation response for IoT environmental telemetry.
/// </summary>
public sealed record TelemetryIngestResponse(
    bool Success,
    string ConsignmentId,
    string SensorId,
    int TotalPointsRecorded,
    ThermalComplianceStatus CurrentCompliance,
    string? Message = null,
    bool DuplicateFiltered = false);

/// <summary>
/// Comprehensive cold-chain consignment status report.
/// </summary>
public sealed record ConsignmentColdChainStatusResponse(
    string ConsignmentId,
    int TotalTelemetryPoints,
    ColdChainTelemetryPoint? LatestPoint,
    MeanKineticTemperatureResult? MktResult,
    IReadOnlyList<GeofenceTransitionEvent> Transitions,
    IReadOnlyList<ParametricInsuranceClaim> Claims,
    bool HasLightBreach = false,
    bool HasShockBreach = false);

// API DTO Requests & Responses

public sealed record IngestColdChainTelemetryRequest(ColdChainTelemetryPoint Point);

public sealed record BatchIngestColdChainTelemetryRequest(IReadOnlyList<ColdChainTelemetryPoint> Points);

public sealed record RegisterGeofenceRequest(
    string GeofenceId,
    string Name,
    GeofenceType Type,
    IReadOnlyList<GeoCoordinate> Coordinates,
    bool IsActive = true);

public sealed record EvaluateCoordinateRequest(
    string ConsignmentId,
    double Latitude,
    double Longitude,
    DateTime? TimestampUtc = null,
    string? SecretKey = null);

public sealed record EvaluateComplianceResponse(
    string ConsignmentId,
    CargoType CargoType,
    MeanKineticTemperatureResult MktResult,
    bool LightBreachDetected,
    bool ShockBreachDetected,
    int ActiveAlertCount);

public sealed record FileParametricClaimRequest(
    string ConsignmentId,
    CargoType CargoType,
    decimal PolicyPayoutDzd,
    string RecipientId,
    string PolicyNumber,
    string? SecretKey = null);
