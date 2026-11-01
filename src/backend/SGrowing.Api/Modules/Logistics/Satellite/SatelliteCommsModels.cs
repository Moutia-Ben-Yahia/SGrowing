namespace SGrowing.Api.Modules.Logistics.Satellite;

/// <summary>
/// Supported Low Earth Orbit (LEO) satellite communications constellations.
/// </summary>
public enum ConstellationType
{
    /// <summary>
    /// Iridium NEXT 66-satellite polar mesh constellation (Short Burst Data 340-byte payloads).
    /// </summary>
    IridiumShortBurstData = 1,

    /// <summary>
    /// Starlink Direct-to-Cell Low-Earth-Orbit NTN constellation (eNodeB payload over LTE B25/T-Mobile).
    /// </summary>
    StarlinkDirectToCell = 2,

    /// <summary>
    /// AST SpaceMobile 3GPP Rel-17 Non-Terrestrial Network (phased array BlueBird / BlueWalker).
    /// </summary>
    AstSpaceMobile3GppNtn = 3
}

/// <summary>
/// Primary transmission medium utilized for telemetry ingestion or egress.
/// </summary>
public enum TransmissionMedium
{
    /// <summary>
    /// Standard 4G/5G terrestrial cellular network (baseline).
    /// </summary>
    TerrestrialCellular = 1,

    /// <summary>
    /// Direct LEO satellite uplink (Iridium SBD, Starlink D2C, AST NTN).
    /// </summary>
    SatelliteUplink = 2,

    /// <summary>
    /// Sub-GHz multi-hop ad-hoc LoRaWAN mesh fallback (868/915 MHz).
    /// </summary>
    LoRaWanMeshHop = 3
}

/// <summary>
/// Telemetry priority governing Head-Of-Line preemption and buffer eviction policies.
/// </summary>
public enum TelemetryPriority
{
    /// <summary>
    /// Routine periodic heartbeat (GPS, battery, normal conditions). Lowest priority.
    /// </summary>
    RoutinePeriodic = 0,

    /// <summary>
    /// High-value pharmaceutical, biologic, or high-capital asset tracking.
    /// </summary>
    HighValueAsset = 1,

    /// <summary>
    /// Critical cold-chain thermal breach or out-of-spec environmental alert. Preempts routine telemetry.
    /// </summary>
    CriticalAlarmThermalBreach = 2,

    /// <summary>
    /// Extreme emergency, SOS distress beacon, or severe cargo collision/shock. Absolute highest priority.
    /// </summary>
    EmergencySos = 3
}

/// <summary>
/// Sensor telemetry reading representing an asset environmental and spatial state.
/// </summary>
public sealed record SensorTelemetryReading(
    string ReadingId,
    string AssetId,
    DateTime TimestampUtc,
    double Latitude,
    double Longitude,
    double AltitudeMeters,
    double TemperatureCelsius,
    double HumidityPct,
    double BatteryPct,
    double ShockGForce,
    TelemetryPriority Priority = TelemetryPriority.RoutinePeriodic,
    Dictionary<string, string>? Metadata = null
);

/// <summary>
/// High-density compressed telemetry frame packaged for satellite burst transmission.
/// </summary>
public sealed record CompressedTelemetryFrame(
    string FrameId,
    string AssetId,
    DateTime TimestampUtc,
    int OriginalBytesLength,
    int CompressedBytesLength,
    double CompressionRatioPercent,
    byte[] CompressedData,
    string PayloadBase64,
    TelemetryPriority Priority,
    string EncodingType,
    string ChecksumSha256,
    DateTime ExpiresAtUtc
);

/// <summary>
/// Calculated satellite orbital pass window for line-of-sight ground communications.
/// </summary>
public sealed record OrbitalPassWindow(
    string SatelliteId,
    ConstellationType Constellation,
    DateTime StartUtc,
    DateTime EndUtc,
    double MaxElevationDegrees,
    double DurationSeconds,
    bool IsCurrentlyVisible,
    double AzimuthDegrees,
    double CurrentElevationDegrees
);

/// <summary>
/// Ad-hoc multi-hop LoRa mesh packet with hop count and deduplication tracking.
/// </summary>
public sealed record LoRaMeshPacket(
    string PacketId,
    string OriginNodeId,
    string? TargetSinkId,
    int CurrentHop,
    int MaxHops,
    byte[] PayloadBytes,
    TelemetryPriority Priority,
    DateTime CreationTimestampUtc,
    DateTime ExpiryTimestampUtc,
    List<string> RelayPath,
    bool HasReachedSink = false,
    TransmissionMedium? UplinkMediumUsed = null
);

/// <summary>
/// Status summary of the satellite burst queue and ring buffer telemetry depth.
/// </summary>
public sealed record SatelliteQueueStatus(
    int TotalQueued,
    int EmergencyCount,
    int CriticalThermalBreachCount,
    int HighValueCount,
    int RoutineCount,
    int RingBufferCapacity,
    long EvictedFramesCount,
    DateTime? OldestQueuedUtc,
    DateTime? NextScheduledPassUtc,
    IReadOnlyList<ConstellationType> ActiveConstellations,
    double AverageCompressionRatioPct
);

/// <summary>
/// Request payload to queue a sensor reading for satellite burst or immediate uplink.
/// </summary>
public sealed record QueueTelemetryRequest(
    SensorTelemetryReading Reading,
    bool ImmediateBurstIfVisible = true
);

/// <summary>
/// Response payload after queuing or transmitting a telemetry frame.
/// </summary>
public sealed record QueueTelemetryResponse(
    bool Queued,
    bool BurstTransmitted,
    string FrameId,
    TelemetryPriority Priority,
    TransmissionMedium MediumUsed,
    int BufferQueueDepth,
    string Message
);

/// <summary>
/// Response containing active and upcoming satellite orbital passes for given coordinates.
/// </summary>
public sealed record OrbitalPassResponse(
    double Latitude,
    double Longitude,
    DateTime EvaluatedAtUtc,
    bool HasLineOfSight,
    List<OrbitalPassWindow> UpcomingPasses,
    string RecommendedConstellation
);

/// <summary>
/// Request to relay a packet through a local LoRa mesh node.
/// </summary>
public sealed record LoRaRelayRequest(
    LoRaMeshPacket Packet,
    string ReportingNodeId,
    bool HasDirectUplink = false,
    TransmissionMedium? DirectUplinkMedium = null
);

/// <summary>
/// Response from a LoRa mesh node forwarding or sinking a relayed packet.
/// </summary>
public sealed record LoRaRelayResponse(
    bool Accepted,
    bool Forwarded,
    bool ReachedSink,
    int CurrentHop,
    string Message,
    TransmissionMedium? EgressMedium
);

/// <summary>
/// Response returning the current satellite telemetry buffer status.
/// </summary>
public sealed record BufferStatusResponse(
    SatelliteQueueStatus Status
);
