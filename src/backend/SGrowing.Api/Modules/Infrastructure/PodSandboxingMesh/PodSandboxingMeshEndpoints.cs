using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.PodSandboxingMesh;

public static class PodSandboxingMeshEndpoints
{
    public static IServiceCollection AddPodSandboxingMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<PodSandboxingMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPodSandboxingMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/pod-sandboxing-mesh")
            .WithTags("Infrastructure - Zero-Trust Kubernetes Pod Sandboxing & Chaos Mesh");

        group.MapGet("/overview", (PodSandboxingMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetPodSandboxingMeshOverview")
        .WithSummary("Retrieve active isolated microVM pods, gVisor runtime stats, and chaos eviction metrics");

        group.MapPost("/provision-sandbox-pod", (ProvisionSandboxPodRequest request, PodSandboxingMeshEngine engine) =>
        {
            var result = engine.ProvisionSandboxPod(request);
            return Results.Ok(result);
        })
        .WithName("ProvisionSandboxPod")
        .WithSummary("Provision ephemeral, hardened sandboxed pod with strict kernel isolation profiles");

        group.MapPost("/trigger-chaos-eviction", (TriggerChaosEvictionRequest request, PodSandboxingMeshEngine engine) =>
        {
            var receipt = engine.TriggerChaosEviction(request);
            return Results.Ok(receipt);
        })
        .WithName("TriggerChaosPodEviction")
        .WithSummary("Simulate node degradation and execute zero-downtime container rescheduling");

        return endpoints;
    }
}
