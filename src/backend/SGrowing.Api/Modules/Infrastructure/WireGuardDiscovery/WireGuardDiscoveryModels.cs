namespace SGrowing.Api.Modules.Infrastructure.WireGuardDiscovery;

public record WireGuardDiscoveredPeer(
    string PeerId,
    string NodeName,
    string CloudProvider, // AWS_US_EAST, GCP_EU_WEST, AZURE_SOUTHEAST_ASIA, ON_PREM_EQUINIX
    string PublicEndpointIpPort,
    string InternalMeshIp,
    string NatTraversalMethod, // DIRECT_PUNCH_STUN, ICE_TURN_RELAY, STATIC_PORT_FORWARD
    double RttLatencyMs,
    bool FallbackToIpsecActive,
    string Status // CONNECTED_DIRECT, RELAYED, FALLBACK_IPSEC
);

public record IpsecFallbackTunnelRecord(
    string TunnelId,
    string PrimaryPeerId,
    string RemoteGatewayIp,
    string EncryptionCipher, // AES_GCM_256, CHACHA20_POLY1305
    int EspPort, // UDP 4500
    string FallbackTriggerReason, // UDP_51820_DROPPED_BY_FIREWALL, SYMMETRIC_NAT_RESTRICTION
    DateTime EstablishedAt
);

public record WireGuardDiscoveryOverview(
    int TotalDiscoveredPeersCount,
    int DirectPunchSuccessPct, // e.g. 92.4% direct P2P NAT punch
    double AverageMeshRttMs,
    int ActiveIpsecFallbackTunnelsCount,
    List<WireGuardDiscoveredPeer> ActivePeers,
    List<IpsecFallbackTunnelRecord> FallbackTunnels
);

public record DiscoverPeersRequest(
    string NodeName,
    string CloudProvider,
    string PublicEndpointIpPort,
    string InternalMeshIp,
    string NatTraversalMethod
);

public record TriggerIpsecFallbackRequest(
    string PeerId,
    string FallbackTriggerReason
);
