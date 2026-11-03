using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.EvpnVxlan;

public interface IEvpnVxlanEngine
{
    EvpnVxlanOverview GetOverview();
    VtepNodeEntity RegisterVtep(string nodeName, string cloudRegion, string loopbackIp);
    TenantVniConfiguration ConfigureTenantVni(int vni, string tenantId, string subnetCidr, string anycastGatewayIp);
    EvpnRouteAdvertisement AdvertiseEvpnRoute(EvpnRouteType routeType, int vni, string mac, string ipOrPrefix, string vtepIp);
}

public sealed class EvpnVxlanEngine : IEvpnVxlanEngine
{
    private readonly ConcurrentDictionary<string, VtepNodeEntity> _vteps = new();
    private readonly ConcurrentDictionary<int, TenantVniConfiguration> _vnis = new();
    private readonly ConcurrentDictionary<string, EvpnRouteAdvertisement> _routes = new();

    public EvpnVxlanEngine()
    {
        SeedVteps();
        SeedInitialVnis();
        SeedInitialRoutes();
    }

    private void SeedVteps()
    {
        var v1 = new VtepNodeEntity(
            "VTEP-AWS-US-EAST",
            "aws-us-east-vtep-01",
            "AWS_US_EAST_1",
            "10.250.0.1",
            4789,
            true,
            DateTime.UtcNow.AddDays(-10)
        );
        _vteps.TryAdd(v1.VtepId, v1);

        var v2 = new VtepNodeEntity(
            "VTEP-EQUINIX-METAL",
            "equinix-sv10-metal-01",
            "EQUINIX_SV10",
            "10.250.0.2",
            4789,
            true,
            DateTime.UtcNow.AddDays(-10)
        );
        _vteps.TryAdd(v2.VtepId, v2);
    }

    private void SeedInitialVnis()
    {
        var vni1 = new TenantVniConfiguration(
            "VNI-CONF-1001",
            10010,
            "TENANT-STRATEGIC-GLOBAL",
            "172.24.10.0/24",
            "172.24.10.1",
            2
        );
        _vnis.TryAdd(vni1.VxlanNetworkIdentifier, vni1);
    }

    private void SeedInitialRoutes()
    {
        var r1 = new EvpnRouteAdvertisement(
            "EVPN-RT-001",
            EvpnRouteType.Type2MacIpAdvertisement,
            10010,
            "00:50:56:b3:18:a4",
            "172.24.10.45",
            "10.250.0.1",
            DateTime.UtcNow.AddMinutes(-14)
        );
        _routes.TryAdd(r1.RouteId, r1);
    }

    public EvpnVxlanOverview GetOverview()
    {
        var vteps = _vteps.Values.ToList();
        var vnis = _vnis.Values.ToList();
        var routes = _routes.Values.OrderByDescending(r => r.AdvertisedAtUtc).Take(10).ToList();

        return new EvpnVxlanOverview(
            ActiveVtepNodesCount: vteps.Count,
            TotalTenantVnisConfigured: vnis.Count,
            TotalEvpnRoutesAdvertised: 4280,
            MeshPacketForwardingThroughputGbps: 184.5,
            VtepNodes: vteps,
            TenantVnis: vnis,
            RecentRoutes: routes
        );
    }

    public VtepNodeEntity RegisterVtep(string nodeName, string cloudRegion, string loopbackIp)
    {
        var vtep = new VtepNodeEntity(
            $"VTEP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            nodeName,
            cloudRegion,
            loopbackIp,
            VxlanPort: 4789,
            IsBgpPeered: true,
            DateTime.UtcNow
        );
        _vteps.TryAdd(vtep.VtepId, vtep);
        return vtep;
    }

    public TenantVniConfiguration ConfigureTenantVni(int vni, string tenantId, string subnetCidr, string anycastGatewayIp)
    {
        var config = new TenantVniConfiguration(
            $"VNI-CONF-{vni}",
            vni,
            tenantId,
            subnetCidr,
            anycastGatewayIp,
            AssociatedVtepCount: _vteps.Count
        );
        _vnis[vni] = config;
        return config;
    }

    public EvpnRouteAdvertisement AdvertiseEvpnRoute(EvpnRouteType routeType, int vni, string mac, string ipOrPrefix, string vtepIp)
    {
        var route = new EvpnRouteAdvertisement(
            $"EVPN-RT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            routeType,
            vni,
            mac,
            ipOrPrefix,
            vtepIp,
            DateTime.UtcNow
        );
        _routes.TryAdd(route.RouteId, route);
        return route;
    }
}
