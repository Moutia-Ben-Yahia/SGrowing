using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Energy.PerovskiteSiliconTandemRecombination;

public static class PerovskiteSiliconTandemEndpoints
{
    public static IServiceCollection AddPerovskiteSiliconTandemServices(this IServiceCollection services)
    {
        services.AddScoped<IPerovskiteSiliconTandemEngine, PerovskiteSiliconTandemEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapPerovskiteSiliconTandemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/energy/perovskite-silicon-tandem")
            .WithTags("Energy Perovskite-Silicon Tandem Recombination");

        group.MapPost("/analyze", (PerovskiteSiliconTandemTelemetry telemetry, IPerovskiteSiliconTandemEngine engine) =>
        {
            var result = engine.AnalyzeTandemCell(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzePerovskiteSiliconTandem")
        .WithSummary("Analyzes monolithic two-terminal (2T) perovskite/silicon tandem solar cell current matching, tunnel junction ohmic recombination, and record power conversion efficiency.");

        return endpoints;
    }
}
