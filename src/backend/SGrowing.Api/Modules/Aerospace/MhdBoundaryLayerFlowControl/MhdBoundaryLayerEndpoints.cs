using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.MhdBoundaryLayerFlowControl;

public static class MhdBoundaryLayerEndpoints
{
    public static IServiceCollection AddMhdBoundaryLayerServices(this IServiceCollection services)
    {
        services.AddScoped<IMhdBoundaryLayerFlowControlEngine, MhdBoundaryLayerFlowControlEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapMhdBoundaryLayerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/mhd-boundary-layer")
            .WithTags("Aerospace Hypersonic MHD Boundary Layer Flow Control");

        group.MapPost("/analyze", (MhdBoundaryLayerTelemetry telemetry, IMhdBoundaryLayerFlowControlEngine engine) =>
        {
            var result = engine.AnalyzeMhdBoundaryLayer(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeMhdBoundaryLayerFlowControl")
        .WithSummary("Analyzes hypersonic MHD interaction parameter (Stuart number), skin friction drag reduction, and onboard electric power extraction.");

        return endpoints;
    }
}
