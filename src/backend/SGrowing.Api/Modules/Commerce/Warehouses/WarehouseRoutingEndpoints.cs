using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce.Warehouses;

public static class WarehouseRoutingEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Guid ResolveTenantId(Guid? tenantId, Guid? headerTenantId, Guid? fallback = null)
    {
        if (tenantId.HasValue && tenantId.Value != Guid.Empty) return tenantId.Value;
        if (headerTenantId.HasValue && headerTenantId.Value != Guid.Empty) return headerTenantId.Value;
        if (fallback.HasValue && fallback.Value != Guid.Empty) return fallback.Value;
        return DefaultTenantId;
    }

    public static IEndpointRouteBuilder MapWarehouseRoutingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/warehouses")
            .WithTags("Multi-Warehouse Allocation & Geo-Routing");

        group.MapPost("/route", async (
            [FromBody] RoutingRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWarehouseRoutingService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId, req.TenantId);
            var result = await service.RouteOrderAsync(req with { TenantId = effectiveTenantId }, ct);
            return Results.Ok(result);
        })
        .WithName("RouteWarehouseOrder")
        .WithSummary("Evaluate customer proximity, allocate stock, and auto-assign fulfillment to optimal warehouse");

        group.MapGet("/stock", async (
            [FromQuery] Guid? warehouseId,
            [FromQuery] string? sku,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWarehouseRoutingService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var stock = await service.GetStockLevelsAsync(effectiveTenantId, warehouseId, sku, ct);
            return Results.Ok(stock);
        })
        .WithName("GetWarehouseStockLevels")
        .WithSummary("Get inventory stock levels across all multi-warehouse hubs");

        group.MapPost("/stock/transfer", async (
            [FromBody] CreateStockTransferRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWarehouseRoutingService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var manifest = await service.CreateStockTransferAsync(effectiveTenantId, req, ct);
            return Results.Ok(manifest);
        })
        .WithName("CreateInterWarehouseStockTransfer")
        .WithSummary("Generate inter-warehouse stock transfer manifest");

        group.MapGet("/transfers", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWarehouseRoutingService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var transfers = await service.GetTransferManifestsAsync(effectiveTenantId, ct);
            return Results.Ok(transfers);
        })
        .WithName("GetStockTransferManifests")
        .WithSummary("List stock transfer manifests");

        group.MapGet("/", async (
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWarehouseRoutingService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var warehouses = await service.GetWarehousesAsync(effectiveTenantId, ct);
            return Results.Ok(warehouses);
        })
        .WithName("ListWarehouses")
        .WithSummary("List active regional warehouses");

        group.MapPost("/", async (
            [FromBody] CreateWarehouseRequest req,
            [FromHeader(Name = "X-Tenant-Id")] Guid? headerTenantId,
            [FromQuery] Guid? tenantId,
            IWarehouseRoutingService service,
            CancellationToken ct) =>
        {
            var effectiveTenantId = ResolveTenantId(tenantId, headerTenantId);
            var warehouse = await service.CreateWarehouseAsync(effectiveTenantId, req, ct);
            return Results.Ok(warehouse);
        })
        .WithName("CreateWarehouse")
        .WithSummary("Register a new regional warehouse hub");

        return app;
    }
}
