namespace SGrowing.Api.Modules.Infrastructure.WireGuardDiscovery;

public static class WireGuardDiscoveryEndpoints
{
    public static IServiceCollection AddWireGuardDiscoveryServices(this IServiceCollection services)
    {
        services.AddSingleton<WireGuardDiscoveryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapWireGuardDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/wireguard-discovery")
            .WithTags("Infrastructure - WireGuard Peer Discovery & IPsec Fallback Mesh");

        group.MapGet("/overview", (WireGuardDiscoveryEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/discover-peers", (WireGuardDiscoveryEngine engine, DiscoverPeersRequest request) =>
        {
            var peer = engine.DiscoverPeers(request);
            return Results.Created($"/api/v1/infrastructure/wireguard-discovery/peers/{peer.PeerId}", peer);
        });

        group.MapPost("/fallback-ipsec", (WireGuardDiscoveryEngine engine, TriggerIpsecFallbackRequest request) =>
        {
            var tunnel = engine.TriggerIpsecFallback(request);
            return Results.Created($"/api/v1/infrastructure/wireguard-discovery/tunnels/{tunnel.TunnelId}", tunnel);
        });

        return app;
    }
}
