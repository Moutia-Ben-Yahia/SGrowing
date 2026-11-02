using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.NeutrinoGeotomographyCoreDensity;

public static class NeutrinoGeotomographyEndpoints
{
    public static IServiceCollection AddNeutrinoGeotomographyServices(this IServiceCollection services)
    {
        services.AddScoped<INeutrinoGeotomographyCoreDensityEngine, NeutrinoGeotomographyCoreDensityEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNeutrinoGeotomographyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/neutrino-geotomography")
            .WithTags("Energy - Deep-Earth Neutrino Tomography & Geo-Reactor");

        group.MapPost("/analyze-chord", (
            NeutrinoTomographyTelemetry telemetry,
            INeutrinoGeotomographyCoreDensityEngine engine) =>
        {
            var result = engine.AnalyzePlanetaryInterior(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeNeutrinoChord")
        .WithSummary("Analyzes atmospheric neutrino Earth-absorption tomography, core-mantle boundary density, and radiogenic geo-reactor heat.");

        return app;
    }
}
