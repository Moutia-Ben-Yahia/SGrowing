namespace SGrowing.Api.Modules.Aviation.AircraftTurnaroundOptimizer;

public static class AircraftTurnaroundEndpoints
{
    public static IServiceCollection AddAircraftTurnaroundServices(this IServiceCollection services)
    {
        services.AddSingleton<AircraftTurnaroundOptimizerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapAircraftTurnaroundEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/aviation/aircraft-turnaround")
            .WithTags("Aircraft Turnaround Optimizer");

        group.MapPost("/optimize-turnaround", (FlightTurnaroundRequest request, AircraftTurnaroundOptimizerEngine engine) =>
        {
            var result = engine.OptimizeTurnaround(request);
            return Results.Ok(result);
        })
        .WithName("OptimizeAircraftTurnaround")
        .WithSummary("Optimizes A-CDM ramp turnaround task graphs, TOBT predictions, and departure slot conformance for commercial flights");

        return app;
    }
}
