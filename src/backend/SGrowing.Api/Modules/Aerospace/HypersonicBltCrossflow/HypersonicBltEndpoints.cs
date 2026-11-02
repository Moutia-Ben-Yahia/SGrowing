namespace SGrowing.Api.Modules.Aerospace.HypersonicBltCrossflow;

public static class HypersonicBltEndpoints
{
    public static IServiceCollection AddHypersonicBltServices(this IServiceCollection services)
    {
        services.AddScoped<HypersonicBltEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHypersonicBltEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/aerospace/hypersonic-blt")
            .WithTags("Hypersonic Boundary Layer Transition & Crossflow Arbiter");

        group.MapPost("/evaluate", (HypersonicBltTelemetry telemetry, HypersonicBltEngine engine) =>
        {
            var result = engine.EvaluateBoundaryLayer(telemetry);
            return Results.Ok(result);
        })
        .WithName("EvaluateHypersonicBlt")
        .WithSummary("Evaluates hypersonic boundary layer transition onset, Mack 2nd mode acoustic fluctuations, and crossflow vortex aerothermal heating.");

        return endpoints;
    }
}
