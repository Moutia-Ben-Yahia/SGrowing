using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Modules.Commerce.Fulfillment;

public static class SplitShipmentRoutingEndpoints
{
    public static IEndpointRouteBuilder MapSplitShipmentRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/fulfillment-routing")
            .WithTags("SplitShipmentRouting");

        group.MapPost("/warehouses", async (
            [FromBody] CreateWarehouseNodeRequest request,
            [FromServices] ISplitShipmentRoutingService service,
            CancellationToken ct) =>
        {
            try
            {
                var node = await service.CreateWarehouseNodeAsync(request, ct);
                return Results.Created($"/api/v1/commerce/fulfillment-routing/warehouses/{node.WarehouseId}", node);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapGet("/warehouses", async (
            [FromQuery] Guid tenantId,
            [FromServices] ISplitShipmentRoutingService service,
            CancellationToken ct) =>
        {
            var nodes = await service.GetWarehouseNodesAsync(tenantId, ct);
            return Results.Ok(nodes);
        });

        group.MapPost("/stock", async (
            [FromBody] SetWarehouseStockRequest request,
            [FromServices] ISplitShipmentRoutingService service,
            CancellationToken ct) =>
        {
            var success = await service.SetWarehouseStockAsync(request, ct);
            return success ? Results.Ok(new { success = true }) : Results.NotFound();
        });

        group.MapPost("/route", async (
            [FromBody] RouteOrderShipmentRequest request,
            [FromServices] ISplitShipmentRoutingService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.RouteOrderShipmentAsync(request, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/rebalance", async (
            [FromQuery] Guid tenantId,
            [FromQuery] int safetyThreshold,
            [FromServices] ISplitShipmentRoutingService service,
            CancellationToken ct) =>
        {
            var manifests = await service.EvaluateStockRebalanceAsync(tenantId, safetyThreshold > 0 ? safetyThreshold : 10, ct);
            return Results.Ok(manifests);
        });

        group.MapGet("/metrics", async (
            [FromQuery] Guid tenantId,
            [FromServices] ISplitShipmentRoutingService service,
            CancellationToken ct) =>
        {
            var metrics = await service.GetMetricsAsync(tenantId, ct);
            return Results.Ok(metrics);
        });

        return app;
    }
}
