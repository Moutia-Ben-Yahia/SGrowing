using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.OpticalCoherenceElastographyBiomechanics;

public static class OpticalCoherenceElastographyEndpoints
{
    public static IServiceCollection AddOpticalElastographyServices(this IServiceCollection services)
    {
        services.AddScoped<IOpticalCoherenceElastographyBiomechanicsEngine, OpticalCoherenceElastographyBiomechanicsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOpticalElastographyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/biotech/optical-elastography")
            .WithTags("BioTech - Optical Coherence Elastography (OCE) Biomechanics");

        group.MapPost("/analyze-scan", (
            OceScanTelemetry telemetry,
            IOpticalCoherenceElastographyBiomechanicsEngine engine) =>
        {
            var result = engine.AnalyzeTissueBiomechanics(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeTissueBiomechanics")
        .WithSummary("Analyzes in vivo phase-resolved Optical Coherence Elastography (PhR-OCE), shear wave velocity, and Young's modulus.");

        return app;
    }
}
