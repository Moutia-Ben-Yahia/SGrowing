using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Infrastructure.BgpGeoSteering;

public static class BgpGeoSteeringEndpoints
{
    public static IServiceCollection AddBgpGeoSteeringServices(this IServiceCollection services)
    {
        services.AddSingleton<IBgpGeoSteeringEngine, BgpGeoSteeringEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapBgpGeoSteeringEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/infrastructure/bgp-geo-steering")
            .WithTags("Infrastructure - BGP Anycast & Geo-Steering DNS");

        group.MapGet("/overview", (IBgpGeoSteeringEngine engine) =>
        {
            var overview = engine.GetOverview();
            return Results.Ok(overview);
        })
        .WithName("GetBgpGeoSteeringOverview")
        .WithSummary("POP health matrix, synthetic probe telemetry, and active DNS steering policies");

        group.MapPost("/probe", (IngestProbeCommand command, IBgpGeoSteeringEngine engine) =>
        {
            var result = engine.IngestProbe(command);
            return Results.Ok(result);
        })
        .WithName("IngestBgpProbe")
        .WithSummary("Ingests latency, jitter, and packet loss metrics from synthetic global probes");

        group.MapPost("/override-steering", (OverrideSteeringCommand command, IBgpGeoSteeringEngine engine) =>
        {
            var result = engine.OverrideSteering(command);
            return Results.Ok(result);
        })
        .WithName("OverrideBgpDnsSteering")
        .WithSummary("Triggers automated or manual DNS record failover with route flap dampening protection");

        return app;
    }
}
