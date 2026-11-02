namespace SGrowing.Api.Modules.Robotics.NeuromorphicOdometry;

public static class NeuromorphicOdometryEndpoints
{
    public static IServiceCollection AddNeuromorphicOdometryServices(this IServiceCollection services)
    {
        services.AddSingleton<NeuromorphicEventOdometryEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapNeuromorphicOdometryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/robotics/neuromorphic-odometry")
            .WithTags("Neuromorphic Odometry");

        group.MapPost("/process-stream", (NeuromorphicOdometryRequest request, NeuromorphicEventOdometryEngine engine) =>
        {
            var result = engine.ProcessEventStream(request);
            return Results.Ok(result);
        })
        .WithName("ProcessNeuromorphicEvents")
        .WithSummary("Processes asynchronous microsecond DVS event streams through Leaky Integrate-and-Fire neurons for robotic visual odometry");

        return app;
    }
}
