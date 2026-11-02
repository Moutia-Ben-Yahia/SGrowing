using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Aerospace.SolarCmeShockArrival;

public static class SolarCmeShockEndpoints
{
    public static IServiceCollection AddSolarCmeShockServices(this IServiceCollection services)
    {
        services.AddScoped<ISolarCmeShockEngine, SolarCmeShockEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSolarCmeShockEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/solar-cme-shock-arrival")
            .WithTags("Aerospace Solar EUV Spectrometer CME Shock Arrival");

        group.MapPost("/analyze", (SolarCmeShockTelemetry telemetry, ISolarCmeShockEngine engine) =>
        {
            var evaluation = engine.AnalyzeShock(telemetry);
            return Results.Ok(evaluation);
        })
        .WithName("AnalyzeSolarCmeShock")
        .WithSummary("Analyzes solar extreme ultraviolet spectrometer CME lift-off, drag-based interplanetary shock deceleration, and arrival times at 1 AU");

        return endpoints;
    }
}
