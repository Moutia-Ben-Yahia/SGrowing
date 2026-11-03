using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Intelligence.PipelineVelocity;

public static class PipelineVelocityEndpoints
{
    public static IServiceCollection AddPipelineVelocityServices(this IServiceCollection services)
    {
        services.AddSingleton<IPipelineVelocityEngine, PipelineVelocityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPipelineVelocityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/intelligence/pipeline-velocity")
            .WithTags("Intelligence - Sales Pipeline Velocity & Bottleneck Forecasting");

        group.MapGet("/overview", (IPipelineVelocityEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetPipelineVelocityOverview")
        .WithSummary("Retrieves aggregate pipeline velocity ($/day), stage dwell times, and bottleneck alerts");

        group.MapPost("/accelerate", (AccelerateStageRequest request, IPipelineVelocityEngine engine) =>
        {
            try
            {
                var result = engine.AccelerateStage(request);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Stage {request.StageId} not found." });
            }
        })
        .WithName("AcceleratePipelineStage")
        .WithSummary("Executes prescriptive playbook to eliminate stage dwell bottlenecks");

        return endpoints;
    }
}
