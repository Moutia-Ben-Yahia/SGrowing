namespace SGrowing.Api.Modules.Logistics.Satellite;

/// <summary>
/// Interface for store-and-forward epidemic LoRaWAN ad-hoc mesh fallback routing.
/// </summary>
public interface ILoRaWanAdHocMeshFallbackService
{
    /// <summary>
    /// Processes an incoming LoRa mesh packet relay request.
    /// Evaluates deduplication filter, hop count limit, and gateway sink detection.
    /// </summary>
    LoRaRelayResponse ProcessRelay(LoRaRelayRequest request);

    /// <summary>
    /// Checks if a packet ID has already been seen in the deduplication filter.
    /// </summary>
    bool IsPacketDuplicate(string packetId);

    /// <summary>
    /// Registers a known gateway sink node with its default uplink capability.
    /// </summary>
    void RegisterKnownSink(string nodeId, TransmissionMedium medium = TransmissionMedium.SatelliteUplink);

    /// <summary>
    /// Unregisters a known gateway sink node.
    /// </summary>
    void UnregisterKnownSink(string nodeId);

    /// <summary>
    /// Returns the set of all currently registered gateway sink node IDs.
    /// </summary>
    IReadOnlyCollection<string> GetKnownSinks();

    /// <summary>
    /// Returns all packets successfully ingested into gateway sinks.
    /// </summary>
    IReadOnlyList<LoRaMeshPacket> GetSinkIngestedPackets();

    /// <summary>
    /// Clears the seen packet deduplication filter.
    /// </summary>
    void ClearSeenFilter();

    /// <summary>
    /// Current count of distinct packet signatures tracked in the deduplication filter.
    /// </summary>
    int SeenPacketCount { get; }
}
