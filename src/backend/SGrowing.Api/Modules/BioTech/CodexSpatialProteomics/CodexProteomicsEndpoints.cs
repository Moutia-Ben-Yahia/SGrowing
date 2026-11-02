using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CodexSpatialProteomics;

public static class CodexProteomicsEndpoints
{
    public static IServiceCollection AddCodexProteomicsServices(this IServiceCollection services)
    {
        services.AddScoped<ICodexProteomicsEngine, CodexProteomicsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCodexProteomicsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/codex-spatial-proteomics")
            .WithTags("BioTech CODEX Spatial Proteomics & Cyclic Multiplex Imaging");

        group.MapPost("/analyze", (CodexProteomicsTelemetry telemetry, ICodexProteomicsEngine engine) =>
        {
            var evaluation = engine.AnalyzeCodexRun(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeCodexProteomics")
        .WithSummary("Analyzes cyclic CODEX spatial proteomics, reporter stripping efficiency, tissue detachment risk, and cell segmentation");

        return endpoints;
    }
}
