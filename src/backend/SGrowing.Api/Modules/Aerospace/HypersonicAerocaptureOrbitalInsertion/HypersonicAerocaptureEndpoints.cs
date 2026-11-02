using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.HypersonicAerocaptureOrbitalInsertion;

public static class HypersonicAerocaptureEndpoints
{
    public static IServiceCollection AddHypersonicAerocaptureServices(this IServiceCollection services)
    {
        services.AddScoped<IHypersonicAerocaptureOrbitalInsertionEngine, HypersonicAerocaptureOrbitalInsertionEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHypersonicAerocaptureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/hypersonic-aerocapture-orbital-insertion")
            .WithTags("Aerospace Hypersonic Aerocapture Orbital Insertion");

        group.MapPost("/analyze", (HypersonicAerocaptureTelemetry telemetry, IHypersonicAerocaptureOrbitalInsertionEngine engine) =>
        {
            var result = engine.AnalyzeAerocaptureTrajectory(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeHypersonicAerocapture")
        .WithSummary("Analyzes planetary aerocapture atmospheric entry corridors, peak aerodynamic stagnation heat flux, and delta-V propellant savings.");

        return endpoints;
    }
}
