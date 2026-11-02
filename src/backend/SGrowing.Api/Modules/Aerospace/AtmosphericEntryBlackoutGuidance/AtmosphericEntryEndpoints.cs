using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.AtmosphericEntryBlackoutGuidance;

public static class AtmosphericEntryEndpoints
{
    public static IServiceCollection AddAtmosphericEntryServices(this IServiceCollection services)
    {
        services.AddScoped<IAtmosphericEntryEngine, AtmosphericEntryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAtmosphericEntryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/atmospheric-entry-blackout")
            .WithTags("Aerospace Atmospheric Entry Guidance & Re-Entry Blackout");

        group.MapPost("/analyze", (AtmosphericEntryTelemetry telemetry, IAtmosphericEntryEngine engine) =>
        {
            var evaluation = engine.AnalyzeEntry(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeAtmosphericEntry")
        .WithSummary("Analyzes planetary hypersonic lifting atmospheric entry corridors, plasma sheath cutoff frequencies, and blackout penetration");

        return endpoints;
    }
}
