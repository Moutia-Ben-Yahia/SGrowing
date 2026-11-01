namespace SGrowing.Api.Modules.Logistics.Satellite;

/// <summary>
/// Store-and-forward epidemic multi-hop routing protocol implementation for 868MHz / 915MHz LoRa mesh nodes.
/// Provides loop/duplicate storm suppression and gateway sink detection for satellite/cellular backhaul.
/// </summary>
public sealed class LoRaWanAdHocMeshFallbackService : ILoRaWanAdHocMeshFallbackService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, DateTime> _seenPackets = [];
    private readonly Dictionary<string, TransmissionMedium> _knownSinks = [];
    private readonly List<LoRaMeshPacket> _ingestedPackets = [];
    private readonly TimeSpan _dedupWindow = TimeSpan.FromHours(2);

    public int SeenPacketCount
    {
        get
        {
            lock (_lock)
            {
                return _seenPackets.Count;
            }
        }
    }

    public LoRaWanAdHocMeshFallbackService()
    {
        // Seed default gateway sinks (e.g. base station with satellite terminal and cellular hub)
        _knownSinks["SINK-SAT-BASE-01"] = TransmissionMedium.SatelliteUplink;
        _knownSinks["SINK-CELL-HUB-02"] = TransmissionMedium.TerrestrialCellular;
    }

    /// <summary>
    /// Processes an incoming LoRa mesh packet relay request.
    /// </summary>
    public LoRaRelayResponse ProcessRelay(LoRaRelayRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Packet);

        if (string.IsNullOrWhiteSpace(request.ReportingNodeId))
        {
            throw new ArgumentException("ReportingNodeId cannot be null or empty.", nameof(request));
        }

        var packet = request.Packet;

        lock (_lock)
        {
            PruneExpiredSeenPackets(DateTime.UtcNow);

            // 1. Check packet TTL expiration
            if (DateTime.UtcNow > packet.ExpiryTimestampUtc)
            {
                return new LoRaRelayResponse(
                    Accepted: false,
                    Forwarded: false,
                    ReachedSink: false,
                    CurrentHop: packet.CurrentHop,
                    Message: "Packet dropped: TTL expired.",
                    EgressMedium: null
                );
            }

            // 2. Check deduplication / broadcast storm prevention
            if (_seenPackets.ContainsKey(packet.PacketId))
            {
                return new LoRaRelayResponse(
                    Accepted: false,
                    Forwarded: false,
                    ReachedSink: false,
                    CurrentHop: packet.CurrentHop,
                    Message: "Duplicate packet suppressed by epidemic seen filter.",
                    EgressMedium: null
                );
            }

            // 3. Mark packet as seen
            _seenPackets[packet.PacketId] = DateTime.UtcNow;

            // 4. Check hop count bounds
            if (packet.CurrentHop >= packet.MaxHops)
            {
                return new LoRaRelayResponse(
                    Accepted: false,
                    Forwarded: false,
                    ReachedSink: false,
                    CurrentHop: packet.CurrentHop,
                    Message: $"Max hops reached ({packet.MaxHops}). Packet dropped to prevent infinite relay.",
                    EgressMedium: null
                );
            }

            // Increment hop & update path
            var newHop = packet.CurrentHop + 1;
            var newPath = new List<string>(packet.RelayPath) { request.ReportingNodeId };

            // 5. Gateway sink detection
            var isSink = request.HasDirectUplink ||
                         _knownSinks.ContainsKey(request.ReportingNodeId) ||
                         string.Equals(packet.TargetSinkId, request.ReportingNodeId, StringComparison.OrdinalIgnoreCase);

            if (isSink)
            {
                var egressMedium = request.DirectUplinkMedium ??
                    (_knownSinks.TryGetValue(request.ReportingNodeId, out var registered)
                        ? registered
                        : TransmissionMedium.SatelliteUplink);

                var sinkPacket = packet with
                {
                    CurrentHop = newHop,
                    RelayPath = newPath,
                    HasReachedSink = true,
                    UplinkMediumUsed = egressMedium
                };

                _ingestedPackets.Add(sinkPacket);

                return new LoRaRelayResponse(
                    Accepted: true,
                    Forwarded: false,
                    ReachedSink: true,
                    CurrentHop: newHop,
                    Message: $"Packet ingested at gateway sink '{request.ReportingNodeId}' via {egressMedium}.",
                    EgressMedium: egressMedium
                );
            }

            // 6. Intermediate relay node
            var forwardedPacket = packet with
            {
                CurrentHop = newHop,
                RelayPath = newPath,
                HasReachedSink = false
            };

            if (newHop >= packet.MaxHops)
            {
                return new LoRaRelayResponse(
                    Accepted: true,
                    Forwarded: false,
                    ReachedSink: false,
                    CurrentHop: newHop,
                    Message: $"Packet reached terminal hop ({newHop}/{packet.MaxHops}). Dropped without forward.",
                    EgressMedium: null
                );
            }

            return new LoRaRelayResponse(
                Accepted: true,
                Forwarded: true,
                ReachedSink: false,
                CurrentHop: newHop,
                Message: $"Packet forwarded to mesh peers via LoRaWAN (Hop {newHop}/{packet.MaxHops}).",
                EgressMedium: TransmissionMedium.LoRaWanMeshHop
            );
        }
    }

    /// <summary>
    /// Checks if a packet ID is known in the deduplication filter.
    /// </summary>
    public bool IsPacketDuplicate(string packetId)
    {
        if (string.IsNullOrWhiteSpace(packetId)) return false;

        lock (_lock)
        {
            return _seenPackets.ContainsKey(packetId);
        }
    }

    /// <summary>
    /// Registers a known gateway sink node.
    /// </summary>
    public void RegisterKnownSink(string nodeId, TransmissionMedium medium = TransmissionMedium.SatelliteUplink)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        lock (_lock)
        {
            _knownSinks[nodeId] = medium;
        }
    }

    /// <summary>
    /// Unregisters a known gateway sink node.
    /// </summary>
    public void UnregisterKnownSink(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return;

        lock (_lock)
        {
            _knownSinks.Remove(nodeId);
        }
    }

    /// <summary>
    /// Returns the set of all currently registered gateway sink node IDs.
    /// </summary>
    public IReadOnlyCollection<string> GetKnownSinks()
    {
        lock (_lock)
        {
            return _knownSinks.Keys.ToList();
        }
    }

    /// <summary>
    /// Returns all packets successfully ingested into gateway sinks.
    /// </summary>
    public IReadOnlyList<LoRaMeshPacket> GetSinkIngestedPackets()
    {
        lock (_lock)
        {
            return [.. _ingestedPackets];
        }
    }

    /// <summary>
    /// Clears the seen packet deduplication filter.
    /// </summary>
    public void ClearSeenFilter()
    {
        lock (_lock)
        {
            _seenPackets.Clear();
        }
    }

    private void PruneExpiredSeenPackets(DateTime nowUtc)
    {
        var cutoff = nowUtc - _dedupWindow;
        var stale = _seenPackets.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
        foreach (var key in stale)
        {
            _seenPackets.Remove(key);
        }
    }
}
