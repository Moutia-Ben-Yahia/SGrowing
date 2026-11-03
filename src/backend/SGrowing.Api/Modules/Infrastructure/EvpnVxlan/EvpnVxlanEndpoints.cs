namespace SGrowing.Api.Modules.Infrastructure.EvpnVxlan;

public static class EvpnVxlanEndpoints
{
    public static IServiceCollection AddEvpnVxlanServices(this IServiceCollection services)
    {
        services.AddSingleton<IEvpnVxlanEngine, EvpnVxlanEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEvpnVxlanEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/evpn-vxlan")
            .WithTags("Infrastructure - Multi-Cloud eBGP EVPN VXLAN Overlay");

        group.MapGet("/overview", (IEvpnVxlanEngine engine) =>
            Results.Ok(engine.GetOverview()));

        group.MapPost("/register-vtep", (RegisterVtepRequest req, IEvpnVxlanEngine engine) =>
        {
            var vtep = engine.RegisterVtep(req.NodeName, req.CloudRegion, req.LoopbackIp);
            return Results.Ok(vtep);
        });

        group.MapPost("/configure-vni", (ConfigureVniRequest req, IEvpnVxlanEngine engine) =>
        {
            var vni = engine.ConfigureTenantVni(req.VxlanNetworkIdentifier, req.TenantId, req.SubnetCidr, req.AnycastGatewayIp);
            return Results.Ok(vni);
        });

        group.MapPost("/advertise-route", (AdvertiseRouteRequest req, IEvpnVxlanEngine engine) =>
        {
            var route = engine.AdvertiseEvpnRoute(req.RouteType, req.Vni, req.MacAddress, req.IpAddressOrPrefix, req.OriginatorVtepIp);
            return Results.Ok(route);
        });

        return endpoints;
    }
}

public sealed record RegisterVtepRequest(string NodeName, string CloudRegion, string LoopbackIp);
public sealed record ConfigureVniRequest(int VxlanNetworkIdentifier, string TenantId, string SubnetCidr, string AnycastGatewayIp);
public sealed record AdvertiseRouteRequest(EvpnRouteType RouteType, int Vni, string MacAddress, string IpAddressOrPrefix, string OriginatorVtepIp);
