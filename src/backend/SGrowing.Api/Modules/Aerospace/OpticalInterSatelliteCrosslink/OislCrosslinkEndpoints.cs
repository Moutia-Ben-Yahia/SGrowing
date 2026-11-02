using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.OpticalInterSatelliteCrosslink;

public static class OislCrosslinkEndpoints
{
    public static IServiceCollection AddOislCrosslinkServices(this IServiceCollection services)
    {
        services.AddScoped<IOislCrosslinkEngine, OislCrosslinkEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapOislCrosslinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/optical-inter-satellite-crosslink")
            .WithTags("Aerospace Optical Inter-Satellite Links (OISL) Pointing, Acquisition & Tracking");

        group.MapPost("/analyze", (OislCrosslinkTelemetry telemetry, IOislCrosslinkEngine engine) =>
        {
            var evaluation = engine.AnalyzeCrosslink(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeOislCrosslink")
        .WithSummary("Analyzes laser crosslinks, fast steering mirror tracking jitter, link budget margins, and Doppler frequency tracking");

        return endpoints;
    }
}
