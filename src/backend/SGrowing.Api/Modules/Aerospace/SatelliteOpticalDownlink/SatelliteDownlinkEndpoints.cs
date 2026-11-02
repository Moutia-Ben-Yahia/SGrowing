namespace SGrowing.Api.Modules.Aerospace.SatelliteOpticalDownlink;

public static class SatelliteDownlinkEndpoints
{
    public static IServiceCollection AddSatelliteDownlinkServices(this IServiceCollection services)
    {
        services.AddSingleton<SatelliteOpticalDownlinkSchedulerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapSatelliteDownlinkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aerospace/satellite-optical-downlink")
            .WithTags("Satellite Optical Downlink");

        group.MapPost("/optimize-schedule", (DownlinkScheduleRequest request, SatelliteOpticalDownlinkSchedulerEngine engine) =>
        {
            var result = engine.OptimizeSchedule(request);
            return Results.Ok(result);
        })
        .WithName("OptimizeSatelliteDownlinkSchedule")
        .WithSummary("Optimizes optical laser downlink contact scheduling for LEO satellite constellations");

        return app;
    }
}
