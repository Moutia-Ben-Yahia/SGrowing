namespace SGrowing.Api.Modules.Infrastructure.CiliumEbpf;

public record WireGuardTunnelNode(
    string TunnelId,
    string NodeName,
    string CloudProvider, // AWS_EKS_EUCENTRAL1, GCP_GKE_EUROPEWEST1, AZURE_AKS_WESTEUROPE
    string NodeInternalIp,
    string WireGuardPublicKey,
    int TunnelPort,
    double HandshakeLatencyMs,
    double ThroughputGbps,
    string Status // TUNNEL_ACTIVE, REKEYING, DEGRADED
);

public record CiliumNetworkPolicyRule(
    string PolicyId,
    string PolicyName,
    string IngressNamespace,
    string EgressNamespace,
    string L7ProtocolRule, // HTTP_REST, GRPC_SPIFFE, KAFKA_TLS
    string EnforcementMode, // IN_KERNEL_BPF_DROP, IN_KERNEL_BPF_ALLOW
    DateTime AppliedAt
);

public record CiliumEbpfOverview(
    int ActiveEncryptedTunnelsCount,
    double AverageHandshakeLatencyMs,
    double InKernelThroughputGbps,
    int ActiveEbpfPoliciesCount,
    List<WireGuardTunnelNode> ActiveTunnels,
    List<CiliumNetworkPolicyRule> ActivePolicies
);

public record ApplyCiliumPolicyRequest(
    string PolicyName,
    string IngressNamespace,
    string EgressNamespace,
    string L7ProtocolRule,
    string EnforcementMode
);

public record RotateTunnelKeyRequest(
    string TunnelId
);
