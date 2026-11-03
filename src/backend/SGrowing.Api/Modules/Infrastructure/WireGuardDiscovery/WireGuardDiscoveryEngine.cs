using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.WireGuardDiscovery;

public class WireGuardDiscoveryEngine
{
    private readonly ConcurrentDictionary<string, WireGuardDiscoveredPeer> _peers = new();
    private readonly ConcurrentBag<IpsecFallbackTunnelRecord> _fallbackTunnels = new();

    public WireGuardDiscoveryEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var p1 = new WireGuardDiscoveredPeer(
            "PEER-AWS-001",
            "aws-us-east-worker-01",
            "AWS_US_EAST",
            "54.210.12.99:51820",
            "10.244.1.10",
            "DIRECT_PUNCH_STUN",
            12.4,
            false,
            "CONNECTED_DIRECT"
        );

        var p2 = new WireGuardDiscoveredPeer(
            "PEER-ONPREM-002",
            "equinix-chicago-core-01",
            "ON_PREM_EQUINIX",
            "198.51.100.42:4500",
            "10.244.2.20",
            "ICE_TURN_RELAY",
            24.8,
            true,
            "FALLBACK_IPSEC"
        );

        _peers[p1.PeerId] = p1;
        _peers[p2.PeerId] = p2;

        _fallbackTunnels.Add(new IpsecFallbackTunnelRecord(
            "IPSEC-TUN-001",
            "PEER-ONPREM-002",
            "198.51.100.42",
            "AES_GCM_256",
            4500,
            "UDP_51820_DROPPED_BY_FIREWALL",
            DateTime.UtcNow.AddMinutes(-35)
        ));
    }

    public WireGuardDiscoveryOverview GetOverview()
    {
        var peersList = _peers.Values.ToList();
        var tunnelsList = _fallbackTunnels.ToList();

        double avgRtt = peersList.Count > 0 ? peersList.Average(p => p.RttLatencyMs) : 18.0;

        return new WireGuardDiscoveryOverview(
            peersList.Count,
            92, // 92% direct punch success
            Math.Round(avgRtt, 1),
            tunnelsList.Count,
            peersList,
            tunnelsList
        );
    }

    public WireGuardDiscoveredPeer DiscoverPeers(DiscoverPeersRequest request)
    {
        var peerId = $"PEER-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var peer = new WireGuardDiscoveredPeer(
            peerId,
            request.NodeName,
            request.CloudProvider,
            request.PublicEndpointIpPort,
            request.InternalMeshIp,
            request.NatTraversalMethod,
            14.2,
            false,
            "CONNECTED_DIRECT"
        );

        _peers[peerId] = peer;
        return peer;
    }

    public IpsecFallbackTunnelRecord TriggerIpsecFallback(TriggerIpsecFallbackRequest request)
    {
        var tunId = $"IPSEC-TUN-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        if (_peers.TryGetValue(request.PeerId, out var peer))
        {
            var updated = peer with
            {
                FallbackToIpsecActive = true,
                Status = "FALLBACK_IPSEC"
            };
            _peers[request.PeerId] = updated;
        }

        var tunnel = new IpsecFallbackTunnelRecord(
            tunId,
            request.PeerId,
            "198.51.100.99",
            "AES_GCM_256",
            4500,
            request.FallbackTriggerReason,
            DateTime.UtcNow
        );

        _fallbackTunnels.Add(tunnel);
        return tunnel;
    }
}
