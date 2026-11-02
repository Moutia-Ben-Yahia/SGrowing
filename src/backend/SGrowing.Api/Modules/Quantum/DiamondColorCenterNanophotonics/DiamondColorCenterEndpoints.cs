using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Quantum.DiamondColorCenterNanophotonics;

public static class DiamondColorCenterEndpoints
{
    public static IServiceCollection AddDiamondColorCenterNanophotonicsServices(this IServiceCollection services)
    {
        services.AddScoped<IDiamondColorCenterNanophotonicsEngine, DiamondColorCenterNanophotonicsEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDiamondColorCenterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/quantum/diamond-color-center-nanophotonics")
            .WithTags("Quantum Diamond Color Center Nanophotonics");

        group.MapPost("/analyze", (DiamondColorCenterTelemetry telemetry, IDiamondColorCenterNanophotonicsEngine engine) =>
        {
            var result = engine.AnalyzeNanophotonicInterface(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeDiamondColorCenter")
        .WithSummary("Analyzes diamond color center (SiV-/SnV-) nanophotonic cavity Purcell enhancement, ZPL branching ratios, and phonon orbital dephasing rates.");

        return endpoints;
    }
}
