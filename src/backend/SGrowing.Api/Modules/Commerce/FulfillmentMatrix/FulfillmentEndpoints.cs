namespace SGrowing.Api.Modules.Commerce.FulfillmentMatrix;

public static class FulfillmentEndpoints
{
    public static IServiceCollection AddFulfillmentMatrixServices(this IServiceCollection services)
    {
        services.AddSingleton<IFulfillmentEngine, FulfillmentEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapFulfillmentMatrixEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/fulfillment")
            .WithTags("Multi-Warehouse Proximity Order Allocation & Split-Fulfillment Engine");

        group.MapGet("/warehouses", (IFulfillmentEngine engine) =>
        {
            var list = engine.ListWarehouses();
            return Results.Ok(list);
        })
        .WithName("ListWarehouseFacilities")
        .WithSummary("List all regional distribution hubs, geo coordinates, and safety buffer thresholds");

        group.MapPost("/plan", (IFulfillmentEngine engine, PlanFulfillmentRequest request) =>
        {
            var plan = engine.PlanOrderFulfillment(request);
            return Results.Ok(plan);
        })
        .WithName("PlanOrderFulfillment")
        .WithSummary("Calculate Haversine geodetic distance, select optimal warehouse, and minimize split-package shipping");

        group.MapGet("/transfers", (IFulfillmentEngine engine) =>
        {
            var transfers = engine.ListTransferRecommendations();
            return Results.Ok(transfers);
        })
        .WithName("ListStockTransferRecommendations")
        .WithSummary("Generate predictive inventory rebalancing transfer recommendations between hubs");

        return app;
    }
}
