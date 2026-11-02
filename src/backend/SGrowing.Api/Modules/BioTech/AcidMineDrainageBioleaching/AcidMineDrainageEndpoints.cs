using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.AcidMineDrainageBioleaching;

public static class AcidMineDrainageEndpoints
{
    public static IServiceCollection AddAcidMineDrainageBioleachingServices(this IServiceCollection services)
    {
        services.AddScoped<IAcidMineDrainageBioleachingEngine, AcidMineDrainageBioleachingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAcidMineDrainageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/acid-mine-drainage-bioleaching")
            .WithTags("BioTech - Deep-Subsurface Bio-Leaching Microorganism Metal Biorecovery & AMD");

        group.MapPost("/analyze-bioleaching", (
            BioleachingTelemetry telemetry,
            IAcidMineDrainageBioleachingEngine engine) =>
        {
            var result = engine.AnalyzeBioleachingProcess(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeAcidMineDrainageBioleaching")
        .WithSummary("Analyzes chemolithoautotrophic bacterial bio-leaching, metal recovery kinetics, jarosite passivation risk, and acid mine drainage remediation.");

        return app;
    }
}
