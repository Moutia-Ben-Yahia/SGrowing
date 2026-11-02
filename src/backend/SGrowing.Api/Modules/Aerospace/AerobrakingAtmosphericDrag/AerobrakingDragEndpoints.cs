using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.AerobrakingAtmosphericDrag;

public static class AerobrakingDragEndpoints
{
    public static IServiceCollection AddAerobrakingDragServices(this IServiceCollection services)
    {
        services.AddScoped<IAerobrakingDragEngine, AerobrakingDragEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAerobrakingDragEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/aerobraking-atmospheric-drag")
            .WithTags("Aerospace Planetary Aerobraking Atmospheric Drag");

        group.MapPost("/analyze", (AerobrakingDragTelemetry telemetry, IAerobrakingDragEngine engine) =>
        {
            var evaluation = engine.AnalyzeDragPass(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeAerobrakingDrag")
        .WithSummary("Analyzes planetary aerobraking atmospheric drag passes, peak convective stagnation heat flux, and solar array aero-torques");

        return endpoints;
    }
}
