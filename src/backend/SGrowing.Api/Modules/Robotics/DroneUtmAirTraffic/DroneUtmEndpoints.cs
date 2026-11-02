namespace SGrowing.Api.Modules.Robotics.DroneUtmAirTraffic;

public static class DroneUtmEndpoints
{
    public static IServiceCollection AddDroneUtmServices(this IServiceCollection services)
    {
        services.AddSingleton<DroneUtmAirTrafficEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapDroneUtmEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/robotics/drone-utm-air-traffic")
            .WithTags("Drone UTM Air Traffic Management");

        group.MapPost("/deconflict-airspace", (DroneUtmDeconflictionRequest request, DroneUtmAirTrafficEngine engine) =>
        {
            var result = engine.DeconflictAirspace(request);
            return Results.Ok(result);
        })
        .WithName("DeconflictDroneAirspace")
        .WithSummary("Audits 4D trajectory space-time prisms, detects drone-to-drone separation loss, geofence breaches, and issues tactical avoidance maneuvers");

        return app;
    }
}
