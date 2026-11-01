namespace SGrowing.Api.Modules.Logistics.Satellite;

/// <summary>
/// Interface for satellite short-burst telemetry compression, priority ring buffering, and decompression.
/// </summary>
public interface ICompressiveTelemetryBurstBuffer
{
    /// <summary>
    /// Ring buffer capacity limit.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Compresses a single sensor reading into an ultra-dense satellite frame using Delta-RLE + Deflate.
    /// </summary>
    CompressedTelemetryFrame CompressReading(SensorTelemetryReading reading, TimeSpan? customTtl = null);

    /// <summary>
    /// Compresses a batch of sensor readings into an ultra-dense satellite frame.
    /// </summary>
    CompressedTelemetryFrame CompressBatch(
        IReadOnlyList<SensorTelemetryReading> readings,
        TelemetryPriority? overridePriority = null,
        TimeSpan? customTtl = null);

    /// <summary>
    /// Decompresses a compressed frame back into sensor telemetry readings (round-trip lossless verification).
    /// </summary>
    IReadOnlyList<SensorTelemetryReading> DecompressFrame(CompressedTelemetryFrame frame);

    /// <summary>
    /// Enqueues a telemetry reading into the priority ring buffer with head-of-line preemption for critical alerts.
    /// </summary>
    CompressedTelemetryFrame Enqueue(SensorTelemetryReading reading, TimeSpan? customTtl = null);

    /// <summary>
    /// Enqueues an already compressed frame into the buffer.
    /// </summary>
    bool EnqueueFrame(CompressedTelemetryFrame frame);

    /// <summary>
    /// Dequeues the highest priority head-of-line frame (EmergencySos > CriticalAlarmThermalBreach > HighValueAsset > RoutinePeriodic).
    /// </summary>
    bool TryDequeue(out CompressedTelemetryFrame? frame);

    /// <summary>
    /// Peeks at the highest priority head-of-line frame without removing it.
    /// </summary>
    bool TryPeek(out CompressedTelemetryFrame? frame);

    /// <summary>
    /// Returns current queue metrics and priority breakdown.
    /// </summary>
    SatelliteQueueStatus GetStatus();

    /// <summary>
    /// Purges all expired frames from the buffer based on TTL.
    /// </summary>
    int PurgeExpired(DateTime? currentUtc = null);

    /// <summary>
    /// Clears all frames from the buffer and resets queues.
    /// </summary>
    void Clear();
}
