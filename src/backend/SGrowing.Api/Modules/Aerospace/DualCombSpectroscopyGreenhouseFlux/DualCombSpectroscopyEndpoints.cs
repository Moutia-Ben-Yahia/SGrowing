using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.DualCombSpectroscopyGreenhouseFlux;

public static class DualCombSpectroscopyEndpoints
{
    public static IServiceCollection AddDualCombSpectroscopyServices(this IServiceCollection services)
    {
        services.AddScoped<IDualCombSpectroscopyEngine, DualCombSpectroscopyEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDualCombSpectroscopyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/dual-comb-spectroscopy-greenhouse-flux")
            .WithTags("Aerospace Dual-Comb Spectroscopy Greenhouse Flux");

        group.MapPost("/analyze", (DualCombSpectroscopyTelemetry telemetry, IDualCombSpectroscopyEngine engine) =>
        {
            var result = engine.AnalyzeAtmosphericFlux(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDualCombSpectroscopy")
        .WithSummary("Analyzes airborne and satellite dual-comb laser spectroscopy, multi-heterodyne downconversion, column-averaged greenhouse gas mole fractions, and fugitive point emissions.");

        return endpoints;
    }
}
