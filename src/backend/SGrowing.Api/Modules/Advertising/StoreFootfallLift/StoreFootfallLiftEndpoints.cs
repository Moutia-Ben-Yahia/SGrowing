using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Advertising.StoreFootfallLift;

public static class StoreFootfallLiftEndpoints
{
    public static IServiceCollection AddStoreFootfallLiftServices(this IServiceCollection services)
    {
        services.AddSingleton<IStoreFootfallLiftEngine, StoreFootfallLiftEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapStoreFootfallLiftEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/advertising/store-footfall-lift")
            .WithTags("Store Footfall Lift & Retail Geofence Attribution Engine");

        group.MapGet("/stores", ([FromServices] IStoreFootfallLiftEngine engine) =>
        {
            var stores = engine.GetStores();
            return Results.Ok(stores);
        })
        .WithName("GetStoreGeofences")
        .WithSummary("Retrieves all configured retail store polygonal geofences");

        group.MapPost("/calculate-lift", ([FromBody] CalculateLiftRequest request, [FromServices] IStoreFootfallLiftEngine engine) =>
        {
            var analysis = engine.CalculateLift(request);
            return Results.Ok(analysis);
        })
        .WithName("CalculateStoreLift")
        .WithSummary("Computes statistical incremental footfall lift and CPIV for exposed vs control audiences");

        group.MapGet("/metrics", ([FromServices] IStoreFootfallLiftEngine engine) =>
        {
            var metrics = engine.GetMetrics();
            return Results.Ok(metrics);
        })
        .WithName("GetStoreFootfallMetrics")
        .WithSummary("Retrieves aggregate store footfall attribution and revenue metrics");

        return app;
    }
}
