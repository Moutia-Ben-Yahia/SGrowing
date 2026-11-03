using SGrowing.Api.Modules.Infrastructure.WireGuardDiscovery;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class WireGuardDiscoveryTests
{
    [Fact]
    public void GetOverview_ReportsPeersDirectPunchAndFallbackTunnels()
    {
        var engine = new WireGuardDiscoveryEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalDiscoveredPeersCount >= 2);
        Assert.True(overview.DirectPunchSuccessPct > 80);
        Assert.True(overview.AverageMeshRttMs < 30.0);
        Assert.True(overview.ActiveIpsecFallbackTunnelsCount >= 1);
        Assert.NotEmpty(overview.ActivePeers);
        Assert.NotEmpty(overview.FallbackTunnels);
    }

    [Fact]
    public void DiscoverPeers_RegistersNewPeerNode()
    {
        var engine = new WireGuardDiscoveryEngine();
        var request = new DiscoverPeersRequest(
            "azure-west-europe-01",
            "AZURE_WEST_EUROPE",
            "20.50.10.12:51820",
            "10.244.3.30",
            "DIRECT_PUNCH_STUN"
        );

        var peer = engine.DiscoverPeers(request);

        Assert.NotNull(peer);
        Assert.StartsWith("PEER-", peer.PeerId);
        Assert.Equal("azure-west-europe-01", peer.NodeName);
        Assert.Equal("CONNECTED_DIRECT", peer.Status);
        Assert.False(peer.FallbackToIpsecActive);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActivePeers, p => p.PeerId == peer.PeerId);
    }

    [Fact]
    public void TriggerIpsecFallback_EstablishesEspTunnelOnUdpBlockage()
    {
        var engine = new WireGuardDiscoveryEngine();
        var request = new TriggerIpsecFallbackRequest(
            "PEER-AWS-001",
            "UDP_51820_DROPPED_BY_FIREWALL"
        );

        var tunnel = engine.TriggerIpsecFallback(request);

        Assert.NotNull(tunnel);
        Assert.StartsWith("IPSEC-TUN-", tunnel.TunnelId);
        Assert.Equal("PEER-AWS-001", tunnel.PrimaryPeerId);
        Assert.Equal(4500, tunnel.EspPort);

        var overview = engine.GetOverview();
        Assert.Contains(overview.FallbackTunnels, t => t.TunnelId == tunnel.TunnelId);
    }
}
