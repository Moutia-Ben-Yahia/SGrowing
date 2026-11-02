namespace SGrowing.Api.Modules.Energy.HvdcGridStabilizer;

public static class HvdcGridStabilizerEndpoints
{
    public static IServiceCollection AddHvdcGridStabilizerServices(this IServiceCollection services)
    {
        services.AddSingleton<HvdcGridStabilizerEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapHvdcGridStabilizerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/energy/hvdc-grid-stabilizer")
            .WithTags("HVDC Grid Stabilizer");

        group.MapPost("/stabilize", (GridStabilizationRequest request, HvdcGridStabilizerEngine engine) =>
        {
            var result = engine.StabilizeGrid(request);
            return Results.Ok(result);
        })
        .WithName("StabilizeHvdcGrid")
        .WithSummary("Ingests IEEE C37.118 PMU synchrophasor telemetry and injects autonomous synthetic inertia from HVDC converter substations");

        return app;
    }
}
