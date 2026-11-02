namespace SGrowing.Api.Modules.Logistics.RailHumpYardScheduler;

public static class RailHumpYardEndpoints
{
    public static IServiceCollection AddRailHumpYardServices(this IServiceCollection services)
    {
        services.AddSingleton<RailHumpYardSchedulerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapRailHumpYardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/rail-hump-yard")
            .WithTags("Rail Hump Yard Scheduler");

        group.MapPost("/schedule-shunting", (HumpShuntingRequest request, RailHumpYardSchedulerEngine engine) =>
        {
            var result = engine.ScheduleHumpShunting(request);
            return Results.Ok(result);
        })
        .WithName("ScheduleHumpShunting")
        .WithSummary("Schedules multi-modal freight rail gravity hump sorting, HAZMAT separation, and classification track assignments");

        return app;
    }
}
