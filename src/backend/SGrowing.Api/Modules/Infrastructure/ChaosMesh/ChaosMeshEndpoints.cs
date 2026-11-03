using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.ChaosMesh;

public static class ChaosMeshEndpoints
{
    public static IServiceCollection AddChaosMeshServices(this IServiceCollection services)
    {
        services.AddSingleton<IChaosMeshEngine, ChaosMeshEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapChaosMeshEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/infrastructure/chaos-mesh")
            .WithTags("Infrastructure - Multi-Region Chaos Engineering & Resilience Injection Mesh");

        group.MapGet("/experiments", (IChaosMeshEngine engine) =>
        {
            var experiments = engine.GetExperiments();
            return Results.Ok(experiments);
        })
        .WithName("GetChaosExperiments")
        .WithSummary("Retrieves all active and historical chaos resilience injection experiments");

        group.MapPost("/inject", (InjectFaultRequest request, IChaosMeshEngine engine) =>
        {
            var experiment = engine.InjectFault(request);
            return Results.Ok(experiment);
        })
        .WithName("InjectChaosFault")
        .WithSummary("Injects controlled chaos fault (latency, packet loss, region failover) into target service");

        group.MapPost("/halt", (HaltExperimentRequest request, IChaosMeshEngine engine) =>
        {
            try
            {
                var result = engine.HaltExperiment(request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Experiment {request.ExperimentId} not found." });
            }
        })
        .WithName("HaltChaosExperiment")
        .WithSummary("Emergency kill switch halting chaos fault injection and restoring steady-state");

        return endpoints;
    }
}
