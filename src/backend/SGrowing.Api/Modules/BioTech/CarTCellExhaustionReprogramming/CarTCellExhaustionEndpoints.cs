using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.BioTech.CarTCellExhaustionReprogramming;

public static class CarTCellExhaustionEndpoints
{
    public static IServiceCollection AddCarTCellExhaustionReprogrammingServices(this IServiceCollection services)
    {
        services.AddScoped<ICarTCellExhaustionReprogrammingEngine, CarTCellExhaustionReprogrammingEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapCarTCellExhaustionReprogrammingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/biotech/cart-exhaustion-reprogramming")
            .WithTags("BioTech CAR-T Cell Exhaustion & Reprogramming");

        group.MapPost("/analyze", (CarTCellExhaustionTelemetry telemetry, ICarTCellExhaustionReprogrammingEngine engine) =>
        {
            var result = engine.AnalyzeCarTExhaustion(telemetry);
            return Results.Ok(result);
        })
        .WithName("AnalyzeCarTCellExhaustion")
        .WithSummary("Analyzes CAR-T cell exhaustion transcriptional markers, immune checkpoint density, and stem cell memory retention.");

        return endpoints;
    }
}
