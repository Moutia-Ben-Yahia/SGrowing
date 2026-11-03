using SGrowing.Api.Modules.Infrastructure.EvpnVxlan;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public sealed class EvpnVxlanTests
{
    private readonly EvpnVxlanEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededVtepsAndVnis()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveVtepNodesCount >= 2);
        Assert.True(overview.MeshPacketForwardingThroughputGbps > 100.0);
        Assert.NotEmpty(overview.VtepNodes);
        Assert.NotEmpty(overview.TenantVnis);
        Assert.NotEmpty(overview.RecentRoutes);
    }

    [Fact]
    public void RegisterVtep_BgpPeered_RegistersVtepWithVxlanPort()
    {
        var vtep = _engine.RegisterVtep("gcp-europe-vtep-01", "GCP_EU_WEST1", "10.250.0.3");

        Assert.NotNull(vtep);
        Assert.StartsWith("VTEP-", vtep.VtepId);
        Assert.Equal(4789, vtep.VxlanPort);
        Assert.True(vtep.IsBgpPeered);
    }

    [Fact]
    public void AdvertiseEvpnRoute_Type2MacIp_StoresAndAdvertisesRoute()
    {
        var route = _engine.AdvertiseEvpnRoute(
            EvpnRouteType.Type2MacIpAdvertisement,
            10020,
            "00:1a:2b:3c:4d:5e",
            "172.24.20.15",
            "10.250.0.1"
        );

        Assert.NotNull(route);
        Assert.StartsWith("EVPN-RT-", route.RouteId);
        Assert.Equal(10020, route.Vni);
        Assert.Equal("172.24.20.15", route.IpAddressOrPrefix);
    }
}
