using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.Logistics.DroneCorridors;

public static class DroneCorridorEndpoints
{
    public static IServiceCollection AddDroneCorridorServices(this IServiceCollection services)
    {
        services.AddSingleton<GeofenceAirspaceManager>();
        services.AddSingleton<FlightCorridorPathfinderEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDroneCorridorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/drone-corridors")
            .WithTags("Dynamic Drone & Autonomous Delivery Fleet Flight Path Corridors & Geofencing");

        group.MapPost("/geofences", (
            GeofencePolygon zone,
            GeofenceAirspaceManager manager) =>
        {
            manager.RegisterZone(zone);
            return Results.Ok(new { Status = "Registered", zone.ZoneId, zone.Name });
        });

        group.MapGet("/geofences", (GeofenceAirspaceManager manager) =>
        {
            return Results.Ok(manager.GetAllZones());
        });

        group.MapPost("/missions/plan", (
            PlanFlightRequest request,
            FlightCorridorPathfinderEngine engine) =>
        {
            var plan = engine.PlanMission(request);
            return Results.Ok(plan);
        });

        return app;
    }
}
