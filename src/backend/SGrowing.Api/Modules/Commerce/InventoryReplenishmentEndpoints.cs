using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Commerce;

#region Request Payloads

public record RecalculateInventoryApiRequest(
    Guid? TenantId,
    double? ServiceLevelPercent = 95.0,
    decimal? OrderCostOverride = null,
    decimal? AnnualHoldingRateOverride = null);

public record GenerateAutonomousPoApiRequest(
    Guid? TenantId,
    string? TargetWarehouseId = null,
    bool IncludeAtRiskItems = false,
    string? InitiatedBy = "Autonomous Inventory Engine");

public record CreateManualPoApiRequest(
    Guid? TenantId,
    string VendorId,
    string DestinationWarehouseId,
    string Sku,
    int Quantity,
    decimal? UnitCostOverride = null,
    string? Notes = null,
    string CreatedBy = "Inventory Manager");

public record UpdatePoStatusApiRequest(
    Guid? TenantId,
    string NextStatus,
    string? TrackingNumber = null,
    string? Notes = null,
    string UpdatedBy = "System");

public record ExecuteTransferApiRequest(
    Guid? TenantId,
    Guid ProposalId,
    int? OverrideQuantity = null,
    string ApprovedBy = "Warehouse Logistics Lead");

#endregion

public static class InventoryReplenishmentEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapInventoryReplenishmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/inventory-replenishment")
            .WithTags("Autonomous Inventory Replenishment & Vendor Purchase Orders");

        // 1. GET /items - List and search inventory across all warehouses with health status filters
        group.MapGet("/items", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? q,
            [FromQuery] string? category,
            [FromQuery] string? healthTier,
            [FromQuery] string? warehouseId,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            var items = await service.GetInventoryItemsAsync(effectiveTenantId, q, category, healthTier, warehouseId);
            return Results.Ok(new { success = true, count = items.Count, items });
        })
        .WithName("ListInventoryItems")
        .WithSummary("List and search inventory items with velocity, DOIR, ROP, EOQ and health badges");

        // 2. GET /items/{sku} - Get single SKU details with multi-warehouse breakdown
        group.MapGet("/items/{sku}", async (
            [FromRoute] string sku,
            [FromQuery] Guid? tenantId,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            var item = await service.GetItemBySkuAsync(effectiveTenantId, sku);
            return item is not null
                ? Results.Ok(new { success = true, item })
                : Results.NotFound(new { success = false, message = $"SKU '{sku}' not found." });
        })
        .WithName("GetInventoryItemBySku")
        .WithSummary("Get item details, multi-warehouse stock allocations, and supplier lead times");

        // 3. GET /summary - Stock health summary KPIs (Critical, Breach, AtRisk, Overstocked, Total Assets)
        group.MapGet("/summary", async (
            [FromQuery] Guid? tenantId,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            var summary = await service.GetHealthSummaryAsync(effectiveTenantId);
            return Results.Ok(new { success = true, summary });
        })
        .WithName("GetStockHealthSummary")
        .WithSummary("Get inventory health KPI metrics, stockout risk counts, and total asset valuation");

        // 4. POST /recalculate - Recalculate velocity, safety stock, ROP, EOQ across inventory
        group.MapPost("/recalculate", async (
            [FromBody] RecalculateInventoryApiRequest request,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            var domainReq = new RecalculateInventoryRequest(
                request.ServiceLevelPercent,
                request.OrderCostOverride,
                request.AnnualHoldingRateOverride);

            var result = await service.RecalculateMetricsAsync(effectiveTenantId, domainReq);
            return Results.Ok(new { success = true, result });
        })
        .WithName("RecalculateInventoryMetrics")
        .WithSummary("Trigger dynamic replenishment recalculation with configurable Z-factor and holding cost");

        // 5. POST /purchase-orders/generate-autonomous - Automatically generate POs for breached items
        group.MapPost("/purchase-orders/generate-autonomous", async (
            [FromBody] GenerateAutonomousPoApiRequest request,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            var domainReq = new GenerateAutonomousPoRequest(
                request.TargetWarehouseId,
                request.IncludeAtRiskItems,
                request.InitiatedBy);

            var generatedPos = await service.GenerateAutonomousPurchaseOrdersAsync(effectiveTenantId, domainReq);
            return Results.Ok(new { success = true, count = generatedPos.Count, purchaseOrders = generatedPos });
        })
        .WithName("GenerateAutonomousPurchaseOrders")
        .WithSummary("Scan ROP breaches and automatically generate purchase orders respecting vendor MOQ and batch multipliers");

        // 6. POST /purchase-orders/create-manual - Manually create purchase order
        group.MapPost("/purchase-orders/create-manual", async (
            [FromBody] CreateManualPoApiRequest request,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            var domainReq = new CreateManualPoRequest(
                request.VendorId,
                request.DestinationWarehouseId,
                request.Sku,
                request.Quantity,
                request.UnitCostOverride,
                request.Notes,
                request.CreatedBy);

            var createdPo = await service.CreateManualPurchaseOrderAsync(effectiveTenantId, domainReq);
            return Results.Created($"/api/v1/commerce/inventory-replenishment/purchase-orders/{createdPo.Id}",
                new { success = true, purchaseOrder = createdPo });
        })
        .WithName("CreateManualPurchaseOrder")
        .WithSummary("Manually create a purchase order with MOQ and batch multiplier enforcement");

        // 7. GET /purchase-orders - List purchase orders
        group.MapGet("/purchase-orders", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? status,
            [FromQuery] string? vendorId,
            [FromQuery] string? warehouseId,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            var orders = await service.GetPurchaseOrdersAsync(effectiveTenantId, status, vendorId, warehouseId);
            return Results.Ok(new { success = true, count = orders.Count, purchaseOrders = orders });
        })
        .WithName("ListPurchaseOrders")
        .WithSummary("List all vendor purchase orders with status filtering");

        // 8. GET /purchase-orders/{id} - Get single PO details
        group.MapGet("/purchase-orders/{id:guid}", async (
            [FromRoute] Guid id,
            [FromQuery] Guid? tenantId,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            var po = await service.GetPurchaseOrderByIdAsync(effectiveTenantId, id);
            return po is not null
                ? Results.Ok(new { success = true, purchaseOrder = po })
                : Results.NotFound(new { success = false, message = $"Purchase Order '{id}' was not found." });
        })
        .WithName("GetPurchaseOrderById")
        .WithSummary("Get full purchase order summary with line items, tax, freight, and tracking");

        // 9. PATCH /purchase-orders/{id}/status - Transition PO state
        group.MapPatch("/purchase-orders/{id:guid}/status", async (
            [FromRoute] Guid id,
            [FromBody] UpdatePoStatusApiRequest request,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            try
            {
                var domainReq = new UpdatePoStatusRequest(
                    request.NextStatus,
                    request.TrackingNumber,
                    request.Notes,
                    request.UpdatedBy);

                var updated = await service.UpdatePurchaseOrderStatusAsync(effectiveTenantId, id, domainReq);
                return Results.Ok(new { success = true, purchaseOrder = updated });
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { success = false, message = knf.Message });
            }
            catch (InvalidOperationException ioe)
            {
                return Results.Conflict(new { success = false, message = ioe.Message });
            }
            catch (ArgumentException ae)
            {
                return Results.BadRequest(new { success = false, message = ae.Message });
            }
        })
        .WithName("UpdatePurchaseOrderStatus")
        .WithSummary("Transition PO state (Draft -> Submitted -> Acknowledged -> InTransit -> Received -> Cancelled). Receiving increments warehouse stock.");

        // 10. GET /transfers/recommendations - Multi-warehouse rebalancing transfer proposals
        group.MapGet("/transfers/recommendations", async (
            [FromQuery] Guid? tenantId,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            var recommendations = await service.GetTransferRecommendationsAsync(effectiveTenantId);
            return Results.Ok(new { success = true, count = recommendations.Count, recommendations });
        })
        .WithName("GetTransferRecommendations")
        .WithSummary("Evaluate stock disparities between regional hubs and propose rebalancing transfers");

        // 11. POST /transfers/execute - Execute/approve transfer proposal
        group.MapPost("/transfers/execute", async (
            [FromBody] ExecuteTransferApiRequest request,
            [FromServices] IServiceProvider sp,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = request.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var service = sp.GetService(typeof(IInventoryReplenishmentService)) as IInventoryReplenishmentService
                          ?? InventoryReplenishmentService.Instance;

            try
            {
                var domainReq = new ExecuteTransferRequest(
                    request.ProposalId,
                    request.OverrideQuantity,
                    request.ApprovedBy);

                var transferResult = await service.ExecuteTransferAsync(effectiveTenantId, domainReq);
                return Results.Ok(new { success = true, result = transferResult });
            }
            catch (KeyNotFoundException knf)
            {
                return Results.NotFound(new { success = false, message = knf.Message });
            }
            catch (InvalidOperationException ioe)
            {
                return Results.Conflict(new { success = false, message = ioe.Message });
            }
            catch (ArgumentException ae)
            {
                return Results.BadRequest(new { success = false, message = ae.Message });
            }
        })
        .WithName("ExecuteWarehouseTransfer")
        .WithSummary("Approve and execute inter-warehouse transfer, instantly adjusting physical stock records");

        return group;
    }
}
