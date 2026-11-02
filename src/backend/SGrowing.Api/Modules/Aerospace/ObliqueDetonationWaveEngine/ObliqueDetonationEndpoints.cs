using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.ObliqueDetonationWaveEngine;

public static class ObliqueDetonationEndpoints
{
    public static IServiceCollection AddObliqueDetonationServices(this IServiceCollection services)
    {
        services.AddScoped<IObliqueDetonationWaveEngine, ObliqueDetonationWaveEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapObliqueDetonationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/oblique-detonation")
            .WithTags("Aerospace - Hypersonic Oblique Detonation Wave Engine");

        group.MapPost("/analyze-wave", (
            ObliqueDetonationTelemetry telemetry,
            IObliqueDetonationWaveEngine engine) =>
        {
            var result = engine.AnalyzeDetonationWaveState(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeObliqueDetonation")
        .WithSummary("Analyzes Mach 7-12 wedge-induced standing oblique detonation wave (ODW), induction length, and cowl ramp stability.");

        return app;
    }
}
