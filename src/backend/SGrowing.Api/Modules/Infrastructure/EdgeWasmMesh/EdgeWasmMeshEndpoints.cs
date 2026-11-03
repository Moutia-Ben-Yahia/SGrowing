using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.EdgeWasmMesh;

public static class EdgeWasmMeshEndpoints
{
    public static IServiceCollection AddEdgeWasmMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<EdgeWasmMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEdgeWasmMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/edge-wasm-mesh")
            .WithTags("Infrastructure - Multi-Cloud Edge CDN WebAssembly (Wasm) Micro-Compute");

        group.MapGet("/overview", (EdgeWasmMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetEdgeWasmMeshOverview")
        .WithSummary("Retrieve globally deployed WebAssembly edge modules, execution latencies, and invocation volume");

        group.MapPost("/deploy-module", (DeployWasmModuleRequest request, EdgeWasmMeshEngine engine) =>
        {
            var module = engine.DeployModule(request);
            return Results.Ok(module);
        })
        .WithName("DeployEdgeWasmModule")
        .WithSummary("Compiles and distributes Wasm bytecode to 300+ global edge CDN Points of Presence");

        group.MapPost("/invoke-edge", (InvokeEdgeFunctionRequest request, EdgeWasmMeshEngine engine) =>
        {
            var metric = engine.InvokeEdge(request);
            return Results.Ok(metric);
        })
        .WithName("InvokeEdgeWasmFunction")
        .WithSummary("Executes serverless Wasm micro-compute at the nearest edge PoP with sub-5ms latency");

        return endpoints;
    }
}
