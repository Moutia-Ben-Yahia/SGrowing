using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SGrowing.Api.Modules.SupplyChain.Replenishment;

/// <summary>
/// Minimal API endpoints and dependency injection extensions for Phase 21:
/// Autonomous Supply Chain Demand Forecasting, Auto-Replenishment &amp; JIT Cross-Docking.
/// </summary>
public static class SupplyChainEndpoints
{
    /// <summary>
    /// Registers all Phase 21 Supply Chain replenishment, forecasting, and cross-docking services.
    /// </summary>
    public static IServiceCollection AddSupplyChainReplenishmentServices(this IServiceCollection services)
    {
        services.AddSingleton<ISupplyDemandForecastingEngine, SupplyDemandForecastingEngine>();
        services.AddSingleton<ISupplierAutoReplenishmentService, SupplierAutoReplenishmentService>();
        services.AddSingleton<ICrossDockingAllocationService, CrossDockingAllocationService>();

        return services;
    }

    /// <summary>
    /// Maps the Phase 21 Supply Chain Minimal API endpoints under /api/v1/supply-chain/replenishment.
    /// </summary>
    public static IEndpointRouteBuilder MapSupplyChainEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/supply-chain/replenishment")
            .WithTags("Supply Chain Demand Forecasting, Auto-Replenishment & JIT Cross-Docking");

        // 1. POST /api/v1/supply-chain/replenishment/forecast
        group.MapPost("/forecast", (
            [FromBody] CalculateForecastRequest request,
            [FromServices] ISupplyDemandForecastingEngine engine) =>
        {
            var response = engine.AnalyzeSkuDemand(request);
            return Results.Ok(response);
        })
        .WithName("CalculateDemandForecastAndSafetyStock")
        .WithSummary("Calculates Holt-Winters triple exponential demand forecast, dynamic safety stock, ROP, and Wilson EOQ.");

        // 2. POST /api/v1/supply-chain/replenishment/evaluate-stock
        group.MapPost("/evaluate-stock", async (
            [FromBody] EvaluateStockRequest request,
            [FromServices] ISupplierAutoReplenishmentService replenishmentService,
            CancellationToken ct) =>
        {
            var response = await replenishmentService.EvaluateWarehouseStockAsync(
                request.WarehouseId, request.SkuRecords, ct);
            return Results.Ok(response);
        })
        .WithName("EvaluateWarehouseStockAndReplenish")
        .WithSummary("Scans warehouse inventory positions against ROP and auto-generates sized PO replenishment proposals.");

        // 3. POST /api/v1/supply-chain/replenishment/po/{proposalId:guid}/dispatch
        group.MapPost("/po/{proposalId:guid}/dispatch", async (
            [FromRoute] Guid proposalId,
            [FromServices] ISupplierAutoReplenishmentService replenishmentService,
            CancellationToken ct) =>
        {
            try
            {
                var response = await replenishmentService.DispatchPurchaseOrderAsync(proposalId, ct);
                return response.Success ? Results.Ok(response) : Results.BadRequest(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { Message = ex.Message });
            }
        })
        .WithName("DispatchPurchaseOrderProposal")
        .WithSummary("Dispatches a purchase order to the supplier with structured ANSI X12 850 EDI/JSON payload.");

        // 4. POST /api/v1/supply-chain/replenishment/po/{proposalId:guid}/acknowledge
        group.MapPost("/po/{proposalId:guid}/acknowledge", async (
            [FromRoute] Guid proposalId,
            [FromBody] AcknowledgePoRequest request,
            [FromServices] ISupplierAutoReplenishmentService replenishmentService,
            CancellationToken ct) =>
        {
            try
            {
                var response = await replenishmentService.AcknowledgePurchaseOrderAsync(
                    proposalId, request.EstimatedDeliveryDate, request.SupplierNote, ct);
                return response.Success ? Results.Ok(response) : Results.BadRequest(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { Message = ex.Message });
            }
        })
        .WithName("AcknowledgePurchaseOrder")
        .WithSummary("Acknowledges a dispatched purchase order, updates estimated delivery date, and records on-order stock.");

        // 5. POST /api/v1/supply-chain/replenishment/cross-dock/allocate
        group.MapPost("/cross-dock/allocate", (
            [FromBody] CrossDockAllocateRequest request,
            [FromServices] ICrossDockingAllocationService crossDockService) =>
        {
            var response = crossDockService.AllocateInboundReceipt(
                request.Receipt, request.PendingOrders);
            return Results.Ok(response);
        })
        .WithName("AllocateCrossDockReceipt")
        .WithSummary("Directly allocates inbound shipments to outbound customer backorders by priority & FIFO, avoiding putaway storage.");

        // 6. GET /api/v1/supply-chain/replenishment/cross-dock/manifests
        group.MapGet("/cross-dock/manifests", (
            [FromServices] ICrossDockingAllocationService crossDockService) =>
        {
            var manifests = crossDockService.GetStagingManifests();
            return Results.Ok(manifests);
        })
        .WithName("GetCrossDockStagingManifests")
        .WithSummary("Returns all generated outbound staging manifests with courier dock bay allocations.");

        // 7. GET /api/v1/supply-chain/replenishment/health
        group.MapGet("/health", () =>
        {
            var health = new SupplyChainEngineHealth(
                Status: "Operational",
                Version: "1.0.0-phase21",
                TimestampUtc: DateTime.UtcNow,
                Capabilities: new Dictionary<string, string>
                {
                    ["Forecasting"] = "Holt-Winters Triple Exponential Smoothing (Additive)",
                    ["SafetyStockModel"] = "Dual-Variability Normal SLA (d_bar, sigma_d, L_bar, sigma_L)",
                    ["Replenishment"] = "ROP + SafetyStock Deficit / Wilson EOQ Dynamic Sizing",
                    ["EdiTransmission"] = "ANSI X12 850 Purchase Order JSON Representation",
                    ["CrossDocking"] = "JIT Inbound-to-Outbound Dock Transfer & Staging Manifests"
                });

            return Results.Ok(health);
        })
        .WithName("GetSupplyChainHealth")
        .WithSummary("Returns health check and capability metadata for Phase 21 Supply Chain Engine.");

        return app;
    }
}
