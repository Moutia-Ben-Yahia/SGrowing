namespace SGrowing.Api.Modules.Infrastructure.CiliumEbpf;

public static class CiliumEbpfEndpoints
{
    public static IServiceCollection AddCiliumEbpfServices(this IServiceCollection services)
    {
        services.AddSingleton<CiliumEbpfMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCiliumEbpfEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/cilium-ebpf")
            .WithTags("Infrastructure - Cilium eBPF Service Mesh & WireGuard Transit");

        group.MapGet("/overview", (CiliumEbpfMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        });

        group.MapPost("/apply-policy", (CiliumEbpfMeshEngine engine, ApplyCiliumPolicyRequest request) =>
        {
            var policy = engine.ApplyPolicy(request);
            return Results.Created($"/api/v1/infrastructure/cilium-ebpf/policies/{policy.PolicyId}", policy);
        });

        group.MapPost("/rotate-tunnel-key", (CiliumEbpfMeshEngine engine, RotateTunnelKeyRequest request) =>
        {
            var node = engine.RotateTunnelKey(request);
            return Results.Ok(node);
        });

        return app;
    }
}
