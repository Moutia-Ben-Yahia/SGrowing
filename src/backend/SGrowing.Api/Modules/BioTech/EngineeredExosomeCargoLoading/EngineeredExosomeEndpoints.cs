using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.EngineeredExosomeCargoLoading;

public static class EngineeredExosomeEndpoints
{
    public static IServiceCollection AddEngineeredExosomeCargoLoadingServices(this IServiceCollection services)
    {
        services.AddScoped<IEngineeredExosomeCargoLoadingEngine, EngineeredExosomeCargoLoadingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapEngineeredExosomeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/engineered-exosome-cargo-loading")
            .WithTags("BioTech Engineered Exosome Cargo Loading");

        group.MapPost("/analyze", (EngineeredExosomeTelemetry telemetry, IEngineeredExosomeCargoLoadingEngine engine) =>
        {
            var result = engine.AnalyzeExosomeBatch(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeEngineeredExosomeBatch")
        .WithSummary("Analyzes engineered extracellular vesicles (EVs / exosomes) surface VHH nanobody display, RNA encapsulation efficiency, and colloidal zeta stability.");

        return endpoints;
    }
}
