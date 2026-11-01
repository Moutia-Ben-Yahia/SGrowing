namespace SGrowing.Api.Modules.Logistics.ColdChain;

/// <summary>
/// High-throughput environmental and spatial oracle engine monitoring cargo cold-chain integrity.
/// </summary>
public interface IColdChainTelemetryOracleEngine
{
    /// <summary>
    /// Ingests a single real-time environmental telemetry point from an IoT sensor logger.
    /// Deduplicates duplicate readings and tracks thermal, shock, and light anomalies.
    /// </summary>
    TelemetryIngestResponse IngestTelemetry(ColdChainTelemetryPoint point);

    /// <summary>
    /// Ingests an ordered batch of environmental telemetry points.
    /// </summary>
    IReadOnlyList<TelemetryIngestResponse> IngestBatch(IEnumerable<ColdChainTelemetryPoint> points);

    /// <summary>
    /// Calculates the regulatory Mean Kinetic Temperature (MKT) and thermal compliance status
    /// for a consignment against its commodity thermal profile.
    /// </summary>
    MeanKineticTemperatureResult CalculateMkt(string consignmentId, CargoThermalProfile profile);

    /// <summary>
    /// Retrieves full chronological environmental telemetry history for a consignment.
    /// </summary>
    IReadOnlyList<ColdChainTelemetryPoint> GetTelemetryHistory(string consignmentId);

    /// <summary>
    /// Retrieves the most recent telemetry point received for a consignment.
    /// </summary>
    ColdChainTelemetryPoint? GetLatestTelemetryPoint(string consignmentId);

    /// <summary>
    /// Determines whether any reading exceeded the tamper / light breach threshold (> 50 Lux).
    /// </summary>
    bool HasLightExposureBreach(string consignmentId, double thresholdLux = 50.0);

    /// <summary>
    /// Determines whether any reading exceeded the shock threshold (> 3.5 Gs).
    /// </summary>
    bool HasShockBreach(string consignmentId, double thresholdG = 3.5);

    /// <summary>
    /// Gets the maximum recorded shock force in Gs for a consignment.
    /// </summary>
    double GetMaxRecordedShock(string consignmentId);

    /// <summary>
    /// Gets the maximum recorded light intensity in Lux for a consignment.
    /// </summary>
    double GetMaxRecordedLight(string consignmentId);

    /// <summary>
    /// Resets internal telemetry state for testing or memory reclamation.
    /// </summary>
    void Clear(string? consignmentId = null);
}
