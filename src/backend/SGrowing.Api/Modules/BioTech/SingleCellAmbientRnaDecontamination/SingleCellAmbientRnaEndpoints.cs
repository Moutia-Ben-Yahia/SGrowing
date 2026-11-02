using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.SingleCellAmbientRnaDecontamination;

public static class SingleCellAmbientRnaEndpoints
{
    public static IServiceCollection AddSingleCellAmbientRnaServices(this IServiceCollection services)
    {
        services.AddScoped<ISingleCellAmbientRnaEngine, SingleCellAmbientRnaEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSingleCellAmbientRnaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/single-cell-ambient-rna-decontamination")
            .WithTags("BioTech Single-Cell Ambient RNA Decontamination");

        group.MapPost("/analyze", (SingleCellAmbientRnaTelemetry telemetry, ISingleCellAmbientRnaEngine engine) =>
        {
            var result = engine.AnalyzeAmbientDecontamination(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeSingleCellAmbientRna")
        .WithSummary("Analyzes microfluidic single-cell RNA-seq ambient soup contamination, Poisson-Gamma background subtraction, and mitochondrial membrane integrity.");

        return endpoints;
    }
}
