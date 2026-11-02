using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.HighMolecularWeightDnaExtraction;

public static class HmwDnaExtractionEndpoints
{
    public static IServiceCollection AddHmwDnaExtractionServices(this IServiceCollection services)
    {
        services.AddScoped<IHmwDnaExtractionEngine, HmwDnaExtractionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHmwDnaExtractionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/hmw-dna-extraction")
            .WithTags("BioTech HMW DNA Extraction & Long-Read Nanopore");

        group.MapPost("/analyze", (HmwDnaExtractionTelemetry telemetry, IHmwDnaExtractionEngine engine) =>
        {
            var evaluation = engine.AnalyzeExtraction(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeHmwDnaExtraction")
        .WithSummary("Analyzes high-molecular-weight DNA integrity, shearing rates, and Oxford Nanopore N50 readiness");

        return endpoints;
    }
}
