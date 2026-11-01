namespace SGrowing.Api.Modules.Logistics.Fleet;

/// <summary>
/// Identifies the commercial and operational category of a fleet vehicle.
/// </summary>
public enum VehicleType
{
    Van,
    Motorcycle,
    RefrigeratedTruck,
    Sedan
}

/// <summary>
/// Operational health status of a fleet vehicle derived from real-time OBD2 telemetry and active trouble codes.
/// </summary>
public enum VehicleHealthStatus
{
    Optimal,
    Degraded,
    CriticalMaintenanceRequired,
    Grounded
}

/// <summary>
/// Severity level of an OBD2 Diagnostic Trouble Code (DTC).
/// </summary>
public enum DtcSeverity
{
    Warning,
    Critical
}

/// <summary>
/// Urgency classification for predictive or preventative vehicle component maintenance.
/// </summary>
public enum MaintenanceUrgency
{
    Routine,
    Upcoming,
    ImmediateActionRequired
}

/// <summary>
/// Lifecycle status of an automated maintenance work order.
/// </summary>
public enum WorkOrderStatus
{
    Pending,
    Scheduled,
    InProgress,
    Completed
}

/// <summary>
/// Standard OBD2 Diagnostic Trouble Code parsed from the vehicle electronic control unit (ECU).
/// </summary>
public sealed record DiagnosticTroubleCode
{
    public string Code { get; init; }
    public DtcSeverity Severity { get; init; }
    public string Description { get; init; }
    public string Component { get; init; }

    public DiagnosticTroubleCode(string code, DtcSeverity severity, string description, string component)
    {
        Code = code;
        Severity = severity;
        Description = description;
        Component = component;
    }

    public DiagnosticTroubleCode(string code, string severity, string description, string component)
    {
        Code = code;
        Severity = Enum.TryParse<DtcSeverity>(severity, ignoreCase: true, out var parsed) ? parsed : DtcSeverity.Warning;
        Description = description;
        Component = component;
    }
}

/// <summary>
/// High-frequency OBD2 telemetry packet streamed from vehicle CAN bus hardware.
/// </summary>
public sealed record Obd2TelemetryPacket(
    string VehicleId,
    string DriverId,
    DateTime TimestampUtc,
    double SpeedKmh,
    double EngineRpm,
    double InstantaneousFuelRateLitersPerHour,
    double CoolantTempCelsius,
    double BatteryVoltage,
    double ThrottlePositionPct,
    double Latitude,
    double Longitude,
    IReadOnlyList<DiagnosticTroubleCode>? DiagnosticTroubleCodes = null)
{
    public IReadOnlyList<DiagnosticTroubleCode> DiagnosticTroubleCodes { get; init; } = DiagnosticTroubleCodes ?? [];

    public Obd2TelemetryPacket(
        string vehicleId,
        string driverId,
        DateTime timestampUtc,
        double speedKmh,
        double engineRpm,
        double instantaneousFuelRateLitersPerHour,
        double coolantTempCelsius,
        double batteryVoltage,
        double throttlePositionPct,
        double latitude,
        double longitude)
        : this(vehicleId, driverId, timestampUtc, speedKmh, engineRpm, instantaneousFuelRateLitersPerHour, coolantTempCelsius, batteryVoltage, throttlePositionPct, latitude, longitude, [])
    {
    }
}

/// <summary>
/// Materialized state snapshot of a fleet vehicle based on latest ingested telemetry.
/// </summary>
public sealed record VehicleState(
    string VehicleId,
    string DriverId,
    DateTime LastUpdatedUtc,
    double SpeedKmh,
    double EngineRpm,
    double InstantaneousFuelRateLitersPerHour,
    double CoolantTempCelsius,
    double BatteryVoltage,
    double ThrottlePositionPct,
    double Latitude,
    double Longitude,
    VehicleHealthStatus HealthStatus,
    IReadOnlyList<DiagnosticTroubleCode> ActiveTroubleCodes,
    long TotalPacketsProcessed);

/// <summary>
/// Holistic driver behavioral eco-score and safety evaluation over a driving session.
/// </summary>
public sealed record DriverEcoScore(
    string DriverId,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    double TotalDistanceKm,
    double EcoScore,
    double SafetyScore,
    int HarshAccelerationsCount,
    int HarshBrakingsCount,
    int SharpTurnsCount,
    double IdlingDurationMinutes,
    double FuelWastedIdlingLiters,
    bool FuelBonusEligible);

/// <summary>
/// Predictive Remaining Useful Life (RUL) calculation based on a 2-parameter Weibull reliability distribution.
/// </summary>
public sealed record WeibullRulEstimate(
    string VehicleId,
    string ComponentName,
    double CurrentOdometerKm,
    double ExpectedRemainingKm,
    double FailureProbabilityPct,
    double BetaShape,
    double EtaScale,
    MaintenanceUrgency Urgency,
    string RecommendedAction);

/// <summary>
/// Predictive or corrective maintenance work order for fleet vehicles.
/// </summary>
public sealed record MaintenanceWorkOrder(
    string WorkOrderId,
    string VehicleId,
    string Component,
    MaintenanceUrgency Urgency,
    WorkOrderStatus Status,
    decimal EstimatedCostDzd,
    DateTime CreatedAtUtc,
    string? Notes = null);

/// <summary>
/// Request to ingest a single OBD2 telemetry packet.
/// </summary>
public sealed record IngestTelemetryRequest(Obd2TelemetryPacket Packet);

/// <summary>
/// Request to ingest a batch of OBD2 telemetry packets.
/// </summary>
public sealed record IngestTelemetryBatchRequest(IReadOnlyList<Obd2TelemetryPacket> Packets);

/// <summary>
/// Ingestion acknowledgement and real-time vehicle health evaluation.
/// </summary>
public sealed record IngestTelemetryResponse(
    bool Success,
    string VehicleId,
    VehicleHealthStatus HealthStatus,
    int ActiveDtcCount,
    string Message,
    bool FilteredByDeadband = false);

/// <summary>
/// Request to create a new maintenance work order.
/// </summary>
public sealed record CreateWorkOrderRequest(
    string VehicleId,
    string Component,
    MaintenanceUrgency Urgency,
    decimal EstimatedCostDzd,
    string? Notes = null);

/// <summary>
/// Consolidated vehicle diagnostics response payload.
/// </summary>
public sealed record VehicleDiagnosticsResponse(
    string VehicleId,
    VehicleState? State,
    int ActiveDtcCount,
    IReadOnlyList<DiagnosticTroubleCode> ActiveTroubleCodes);
