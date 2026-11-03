using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.EdgeMesh;

public static class EdgeMeshEndpoints
{
    public static IServiceCollection AddEdgeMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<IEdgeMeshEngine, EdgeMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEdgeMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/edge-mesh")
            .WithTags("Infrastructure - Global Edge Compute Mesh & Serverless Execution");

        group.MapGet("/overview", (IEdgeMeshEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetEdgeMeshOverview")
        .WithSummary("Retrieves global edge PoP node statuses, sub-18ms p95 latencies, and serverless isolate telemetry");

        group.MapPost("/deploy", (DeployEdgeFunctionCommand command, IEdgeMeshEngine engine) =>
        {
            var result = engine.DeployFunction(command);
            return Results.Ok(result);
        })
        .WithName("DeployEdgeFunction")
        .WithSummary("Deploys a serverless function across all global edge PoP isolates");

        group.MapPost("/canary", (UpdateCanarySplitCommand command, IEdgeMeshEngine engine) =>
        {
            try
            {
                var result = engine.UpdateCanarySplit(command);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Function {command.FunctionId} not found." });
            }
        })
        .WithName("UpdateEdgeCanarySplit")
        .WithSummary("Updates canary traffic percentage across global edge PoP clusters");

        group.MapPost("/pop/{popCode}/toggle-health", (string popCode, IEdgeMeshEngine engine) =>
        {
            try
            {
                var result = engine.TogglePoPHealth(popCode);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Edge PoP {popCode} not found." });
            }
        })
        .WithName("ToggleEdgePoPHealth")
        .WithSummary("Simulates a health failure or recovery for a global edge PoP");

        return endpoints;
    }
}
