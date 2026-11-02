using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Defense.AdaptiveOpticsPhaseConjugation;

public static class AdaptiveOpticsEndpoints
{
    public static IServiceCollection AddAdaptiveOpticsServices(this IServiceCollection services)
    {
        services.AddScoped<IAdaptiveOpticsPhaseConjugationEngine, AdaptiveOpticsPhaseConjugationEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAdaptiveOpticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/defense/adaptive-optics")
            .WithTags("Defense - Directed Energy Adaptive Optics Phase Conjugation");

        group.MapPost("/analyze", (
            AtmosphericAdaptiveOpticsTelemetry telemetry,
            IAdaptiveOpticsPhaseConjugationEngine engine) =>
        {
            var result = engine.AnalyzeAtmosphericWavefront(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAdaptiveOpticsWavefront")
        .WithSummary("Analyzes atmospheric turbulence, Greenwood frequency, and deformable mirror Strehl ratio for HEL beams.");

        return app;
    }
}
