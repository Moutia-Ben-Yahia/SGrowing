using SGrowing.Api.Modules.Infrastructure.CiliumEbpf;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class CiliumEbpfTests
{
    [Fact]
    public void GetOverview_ReportsMeshNodesPoliciesAndWireGuardTunnels()
    {
        var engine = new CiliumEbpfMeshEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveEncryptedTunnelsCount >= 2);
        Assert.True(overview.ActiveEbpfPoliciesCount >= 1);
        Assert.True(overview.AverageHandshakeLatencyMs < 10.0);
        Assert.True(overview.InKernelThroughputGbps > 50.0);
        Assert.NotEmpty(overview.ActiveTunnels);
        Assert.NotEmpty(overview.ActivePolicies);
    }

    [Fact]
    public void ApplyPolicy_AppliesCiliumNetworkPolicyRule()
    {
        var engine = new CiliumEbpfMeshEngine();
        var request = new ApplyCiliumPolicyRequest(
            "deny-all-egress-unauthorized",
            "finance-core",
            "external-internet",
            "HTTP_REST",
            "IN_KERNEL_BPF_DROP"
        );

        var policy = engine.ApplyPolicy(request);

        Assert.NotNull(policy);
        Assert.StartsWith("POL-", policy.PolicyId);
        Assert.Equal("deny-all-egress-unauthorized", policy.PolicyName);
        Assert.Equal("finance-core", policy.IngressNamespace);
        Assert.Equal("external-internet", policy.EgressNamespace);
        Assert.Equal("IN_KERNEL_BPF_DROP", policy.EnforcementMode);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActivePolicies, p => p.PolicyId == policy.PolicyId);
    }

    [Fact]
    public void RotateTunnelKey_UpdatesWireGuardPublicKey()
    {
        var engine = new CiliumEbpfMeshEngine();
        var request = new RotateTunnelKeyRequest("WG-TUN-001");

        var initialOverview = engine.GetOverview();
        var initialTunnel = initialOverview.ActiveTunnels.First(t => t.TunnelId == "WG-TUN-001");
        var initialKey = initialTunnel.WireGuardPublicKey;

        var updatedTunnel = engine.RotateTunnelKey(request);

        Assert.NotNull(updatedTunnel);
        Assert.Equal("WG-TUN-001", updatedTunnel.TunnelId);
        Assert.NotEqual(initialKey, updatedTunnel.WireGuardPublicKey);
        Assert.Equal("TUNNEL_ACTIVE", updatedTunnel.Status);
    }
}
