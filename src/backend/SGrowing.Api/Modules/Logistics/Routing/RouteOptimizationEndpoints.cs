using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Logistics.Routing;

public static class RouteOptimizationEndpoints
{
    public static IServiceCollection AddRouteOptimizationServices(this IServiceCollection services)
    {
        services.AddSingleton<DynamicWeatherHazardRadarService>();
        services.AddSingleton<MultiModalRouteOptimizationEngine>();
        services.AddSingleton<EvFleetEnergyRoutingService>();
        return services;
    }

    public static IEndpointRouteBuilder MapRouteOptimizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/routing")
            .WithTags("Multi-Modal Route Optimization & Weather Radar");

        group.MapPost("/calculate", (
            CalculateRouteRequest request,
            MultiModalRouteOptimizationEngine routingEngine) =>
        {
            var route = routingEngine.CalculateRoute(request);
            return Results.Ok(route);
        })
        .WithName("CalculateMultiModalRoute")
        .WithSummary("Calculates the optimal multi-modal freight route across road, rail, and maritime corridors.");

        group.MapPost("/ev/energy-plan", (
            CalculateEvEnergyRouteRequest request,
            EvFleetEnergyRoutingService evService) =>
        {
            var plan = evService.CalculateEvEnergyPlan(request);
            return Results.Ok(plan);
        })
        .WithName("CalculateEvEnergyPlan")
        .WithSummary("Simulates EV freight battery consumption, regenerative capture, and schedules fast-charging stops.");

        group.MapPost("/hazards", (
            WeatherHazardAlert alert,
            DynamicWeatherHazardRadarService hazardRadar) =>
        {
            hazardRadar.RegisterHazard(alert);
            return Results.Created($"/api/v1/logistics/routing/hazards/active", alert);
        })
        .WithName("RegisterWeatherHazard")
        .WithSummary("Registers a new meteorological hazard alert (sandstorm, flood, extreme heat, snow).");

        group.MapGet("/hazards/active", (
            DynamicWeatherHazardRadarService hazardRadar) =>
        {
            var active = hazardRadar.GetActiveHazards();
            return Results.Ok(active);
        })
        .WithName("GetActiveWeatherHazards")
        .WithSummary("Retrieves all active unexpired weather alerts impacting transport corridors.");

        group.MapDelete("/hazards/{hazardId}", (
            string hazardId,
            DynamicWeatherHazardRadarService hazardRadar) =>
        {
            var removed = hazardRadar.RevokeHazard(hazardId);
            return removed ? Results.NoContent() : Results.NotFound();
        })
        .WithName("RevokeWeatherHazard")
        .WithSummary("Revokes or marks as resolved an active weather hazard.");

        return app;
    }
}
