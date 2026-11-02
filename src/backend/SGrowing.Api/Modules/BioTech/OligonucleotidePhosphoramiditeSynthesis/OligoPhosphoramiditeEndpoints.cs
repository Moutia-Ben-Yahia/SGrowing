using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.OligonucleotidePhosphoramiditeSynthesis;

public static class OligoPhosphoramiditeEndpoints
{
    public static IServiceCollection AddOligoPhosphoramiditeSynthesisServices(this IServiceCollection services)
    {
        services.AddScoped<IOligoPhosphoramiditeSynthesisEngine, OligoPhosphoramiditeSynthesisEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOligoPhosphoramiditeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/oligo-phosphoramidite-synthesis")
            .WithTags("BioTech Oligonucleotide Phosphoramidite Synthesis");

        group.MapPost("/analyze", (OligoPhosphoramiditeTelemetry telemetry, IOligoPhosphoramiditeSynthesisEngine engine) =>
        {
            var result = engine.AnalyzeSynthesis(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeOligoPhosphoramiditeSynthesis")
        .WithSummary("Analyzes solid-phase oligonucleotide chemical synthesis, stepwise trityl coupling yield, solvent moisture limits, and capping efficiency.");

        return endpoints;
    }
}
