namespace SGrowing.Api.Modules.Infrastructure.EvpnVxlan;

public enum EvpnRouteType
{
    Type2MacIpAdvertisement,
    Type3InclusiveMulticastEthernetTag,
    Type5IpPrefixRoute
}

public sealed record VtepNodeEntity(
    string VtepId,
    string NodeName,
    string CloudProviderRegion,
    string UnderlayLoopbackIp,
    int VxlanPort,
    bool IsBgpPeered,
    DateTime RegisteredAtUtc
);

public sealed record TenantVniConfiguration(
    string VniId,
    int VxlanNetworkIdentifier,
    string TenantId,
    string SubnetCidr,
    string AnycastGatewayIp,
    int AssociatedVtepCount
);

public sealed record EvpnRouteAdvertisement(
    string RouteId,
    EvpnRouteType RouteType,
    int Vni,
    string MacAddress,
    string IpAddressOrPrefix,
    string OriginatorVtepIp,
    DateTime AdvertisedAtUtc
);

public sealed record EvpnVxlanOverview(
    int ActiveVtepNodesCount,
    int TotalTenantVnisConfigured,
    int TotalEvpnRoutesAdvertised,
    double MeshPacketForwardingThroughputGbps,
    IReadOnlyList<VtepNodeEntity> VtepNodes,
    IReadOnlyList<TenantVniConfiguration> TenantVnis,
    IReadOnlyList<EvpnRouteAdvertisement> RecentRoutes
);
