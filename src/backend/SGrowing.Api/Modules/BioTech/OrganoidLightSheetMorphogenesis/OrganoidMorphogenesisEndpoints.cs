using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.OrganoidLightSheetMorphogenesis;

public static class OrganoidMorphogenesisEndpoints
{
    public static IServiceCollection AddOrganoidMorphogenesisServices(this IServiceCollection services)
    {
        services.AddScoped<IOrganoidMorphogenesisEngine, OrganoidMorphogenesisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOrganoidMorphogenesisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/organoid-light-sheet-morphogenesis")
            .WithTags("BioTech 3D Organoid Morphogenesis & Light-Sheet High-Content Screening");

        group.MapPost("/analyze", (OrganoidMorphogenesisTelemetry telemetry, IOrganoidMorphogenesisEngine engine) =>
        {
            var evaluation = engine.AnalyzeMorphogenesis(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeOrganoidMorphogenesis")
        .WithSummary("Analyzes 3D organoid spatiotemporal branching morphogenesis, necrotic core diffusion boundaries, and light-sheet viability");

        return endpoints;
    }
}
