namespace SGrowing.Api.Modules.Logistics.Fleet;

/// <summary>
/// High-throughput, thread-safe ingestion engine for real-time OBD2 CAN bus vehicle telemetry streams.
/// </summary>
public interface IObd2TelemetryStreamerEngine
{
    /// <summary>
    /// Ingests a single OBD2 telemetry packet, applying deadband filtering and real-time health evaluations.
    /// </summary>
    IngestTelemetryResponse IngestTelemetry(Obd2TelemetryPacket packet);

    /// <summary>
    /// Ingests a batch of OBD2 telemetry packets in temporal order.
    /// </summary>
    IReadOnlyList<IngestTelemetryResponse> IngestBatch(IEnumerable<Obd2TelemetryPacket> packets);

    /// <summary>
    /// Retrieves the latest materialized telematics and health state for a vehicle.
    /// </summary>
    VehicleState? GetLatestVehicleState(string vehicleId);

    /// <summary>
    /// Retrieves all currently active Diagnostic Trouble Codes (DTCs) logged for a vehicle.
    /// </summary>
    IReadOnlyList<DiagnosticTroubleCode> GetActiveTroubleCodes(string vehicleId);

    /// <summary>
    /// Retrieves recent chronological telemetry history for a vehicle.
    /// </summary>
    IReadOnlyList<Obd2TelemetryPacket> GetTelemetryHistory(string vehicleId, int limit = 1000);

    /// <summary>
    /// Clears or acknowledges active Diagnostic Trouble Codes for a vehicle after maintenance.
    /// </summary>
    void ClearTroubleCodes(string vehicleId);
}
