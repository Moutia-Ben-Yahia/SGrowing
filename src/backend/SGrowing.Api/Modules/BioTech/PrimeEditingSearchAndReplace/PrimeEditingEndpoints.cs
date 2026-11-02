using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.PrimeEditingSearchAndReplace;

public static class PrimeEditingEndpoints
{
    public static IServiceCollection AddPrimeEditingSearchAndReplaceServices(this IServiceCollection services)
    {
        services.AddScoped<IPrimeEditingSearchAndReplaceEngine, PrimeEditingSearchAndReplaceEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPrimeEditingSearchAndReplaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/prime-editing-search-replace")
            .WithTags("BioTech Prime Editing Precision Search and Replace");

        group.MapPost("/analyze", (PrimeEditingTelemetry telemetry, IPrimeEditingSearchAndReplaceEngine engine) =>
        {
            var result = engine.AnalyzePrimeEditing(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzePrimeEditing")
        .WithSummary("Analyzes prime editing pegRNA PBS/RTT kinetics, PE2 vs PE3 on-target efficiency, and edit-to-indel precision ratio.");

        return endpoints;
    }
}
