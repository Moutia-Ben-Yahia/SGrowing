using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SGrowing.Api.Modules.Infrastructure.CiliumEbpf;

public class CiliumEbpfMeshEngine
{
    private readonly ConcurrentDictionary<string, WireGuardTunnelNode> _tunnels = new();
    private readonly ConcurrentBag<CiliumNetworkPolicyRule> _policies = new();

    public CiliumEbpfMeshEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var t1 = new WireGuardTunnelNode(
            "WG-TUN-001",
            "ip-10-0-12-88.eu-central-1.compute.internal",
            "AWS_EKS_EUCENTRAL1",
            "10.0.12.88",
            "pubkey_wg_aws_48ba109f21c=",
            51820,
            1.8,
            38.4,
            "TUNNEL_ACTIVE"
        );

        var t2 = new WireGuardTunnelNode(
            "WG-TUN-002",
            "gke-cluster-node-91.europe-west1-b.c.internal",
            "GCP_GKE_EUROPEWEST1",
            "10.142.0.44",
            "pubkey_wg_gcp_99df11a008e=",
            51820,
            2.1,
            39.2,
            "TUNNEL_ACTIVE"
        );

        _tunnels[t1.TunnelId] = t1;
        _tunnels[t2.TunnelId] = t2;

        _policies.Add(new CiliumNetworkPolicyRule(
            "POL-001",
            "enforce-zero-trust-spiffe-grpc",
            "growth-mesh",
            "finance-core",
            "GRPC_SPIFFE",
            "IN_KERNEL_BPF_ALLOW",
            DateTime.UtcNow.AddHours(-14)
        ));
    }

    public CiliumEbpfOverview GetOverview()
    {
        var tunnelsList = _tunnels.Values.ToList();
        var policiesList = _policies.ToList();

        double avgLatency = tunnelsList.Count > 0
            ? Math.Round(tunnelsList.Average(t => t.HandshakeLatencyMs), 1)
            : 2.0;

        double totalThroughput = Math.Round(tunnelsList.Sum(t => t.ThroughputGbps), 1);

        return new CiliumEbpfOverview(
            tunnelsList.Count,
            avgLatency,
            totalThroughput,
            policiesList.Count,
            tunnelsList,
            policiesList
        );
    }

    public CiliumNetworkPolicyRule ApplyPolicy(ApplyCiliumPolicyRequest request)
    {
        var polId = $"POL-{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var policy = new CiliumNetworkPolicyRule(
            polId,
            request.PolicyName,
            request.IngressNamespace,
            request.EgressNamespace,
            request.L7ProtocolRule,
            request.EnforcementMode,
            DateTime.UtcNow
        );

        _policies.Add(policy);
        return policy;
    }

    public WireGuardTunnelNode RotateTunnelKey(RotateTunnelKeyRequest request)
    {
        var newKey = $"pubkey_wg_{Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant()}=";
        if (_tunnels.TryGetValue(request.TunnelId, out var tunnel))
        {
            var updated = tunnel with
            {
                WireGuardPublicKey = newKey,
                Status = "TUNNEL_ACTIVE"
            };
            _tunnels[request.TunnelId] = updated;
            return updated;
        }

        var defaultNode = new WireGuardTunnelNode(
            request.TunnelId,
            "ephemeral-transit-node.internal",
            "AWS_EKS_EUCENTRAL1",
            "10.0.99.12",
            newKey,
            51820,
            1.5,
            40.0,
            "TUNNEL_ACTIVE"
        );
        _tunnels[request.TunnelId] = defaultNode;
        return defaultNode;
    }
}
