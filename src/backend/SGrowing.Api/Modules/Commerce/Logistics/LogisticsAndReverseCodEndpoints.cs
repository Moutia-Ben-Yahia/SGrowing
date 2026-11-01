using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Commerce.Logistics;

#region API Contracts

public record CodFraudCheckApiRequest(
    Guid? TenantId,
    Guid? OrderId,
    string PhoneNumber,
    string DeliveryAddress,
    string? CustomerName = null,
    string? DestinationCity = null,
    decimal? OrderAmount = null,
    int? TotalPastOrders = null,
    int? PastRefusalsCount = null,
    int? PastUncollectedCount = null,
    int? SuccessfulDeliveriesCount = null);

public record CodVerifyApiRequest(
    Guid? TenantId,
    Guid OrderId,
    string VerificationCode,
    string Channel = "WhatsApp");

public record SmartRouteApiRequest(
    Guid? TenantId,
    Guid? OrderId,
    string DestinationCity,
    decimal? PackageWeightKg = null,
    decimal? CodAmount = null,
    int? MaxSlaDays = null);

public record CreateReverseExchangeApiRequest(
    Guid? TenantId,
    Guid? OriginalOrderId,
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    string City,
    string OriginalSku,
    decimal OriginalSkuPrice,
    string ReplacementSku,
    decimal ReplacementSkuPrice,
    string ExchangeReason,
    string? CourierName = null);

public record DoorstepSwapApiRequest(
    Guid? TenantId,
    Guid ExchangeId,
    string ScannedReverseWaybill,
    decimal CashCollected,
    bool ReplacementHandedOver,
    string? CourierAgentId = null);

public record QuarantineInspectApiRequest(
    Guid? TenantId,
    Guid ExchangeId,
    string ScannedReturnBarcode,
    QuarantineItemCondition Condition,
    string InspectorNotes,
    string InspectorId);

public record ReconcileBatchApiRequest(
    Guid? TenantId,
    string StatementReference,
    string CourierName,
    DateTime? StatementDateUtc,
    List<CourierRemittanceBatchStatementRow> Rows);

#endregion

public static class LogisticsAndReverseCodEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapLogisticsAndReverseCodEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics")
            .WithTags("Autonomous Multi-Carrier Logistics & Reverse COD Orchestration");

        // 1. POST /api/v1/logistics/fraud-check
        group.MapPost("/fraud-check", async (
            [FromBody] CodFraudCheckApiRequest req,
            [FromServices] ICodFraudPredictorService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var orderId = req.OrderId ?? Guid.NewGuid();

            var evaluationReq = new CodFraudEvaluationRequest(
                effectiveTenantId,
                orderId,
                req.PhoneNumber,
                req.DeliveryAddress,
                req.CustomerName,
                req.DestinationCity,
                req.OrderAmount ?? 0m,
                req.TotalPastOrders ?? 0,
                req.PastRefusalsCount ?? 0,
                req.PastUncollectedCount ?? 0,
                req.SuccessfulDeliveriesCount ?? 0);

            var assessment = await service.EvaluateRiskAsync(effectiveTenantId, evaluationReq, ct);
            return Results.Ok(assessment);
        })
        .WithName("EvaluateCodFraudRisk")
        .WithSummary("Evaluate COD order fraud risk with Shannon address entropy, refusal rates, and high-risk token gating");

        // 2. POST /api/v1/logistics/verify-cod
        group.MapPost("/verify-cod", async (
            [FromBody] CodVerifyApiRequest req,
            [FromServices] ICodFraudPredictorService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var result = await service.VerifyOrderAsync(effectiveTenantId, req.OrderId, req.VerificationCode, req.Channel, ct);

            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .WithName("VerifyCodOrder")
        .WithSummary("Verify high-risk COD order token via WhatsApp 1-click confirmation or IVR voice token");

        // 3. POST /api/v1/logistics/smart-route
        group.MapPost("/smart-route", async (
            [FromBody] SmartRouteApiRequest req,
            [FromServices] ICarrierSmartRouterService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var orderId = req.OrderId ?? Guid.NewGuid();

            var recommendation = await service.RouteOrderAsync(
                effectiveTenantId,
                orderId,
                req.DestinationCity,
                req.PackageWeightKg ?? 1.0m,
                req.CodAmount ?? 0m,
                req.MaxSlaDays ?? 3,
                ct);

            return Results.Ok(recommendation);
        })
        .WithName("SmartRouteOrder")
        .WithSummary("Smart 3PL courier route optimization with on-time probability and net yield fallback ranking");

        // 4. POST /api/v1/logistics/reverse-exchange/create
        group.MapPost("/reverse-exchange/create", async (
            [FromBody] CreateReverseExchangeApiRequest req,
            [FromServices] IReverseExchangeService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var orderId = req.OriginalOrderId ?? Guid.NewGuid();

            var initiationReq = new ReverseExchangeInitiationRequest(
                effectiveTenantId,
                orderId,
                req.CustomerName,
                req.CustomerPhone,
                req.DeliveryAddress,
                req.City,
                req.OriginalSku,
                req.OriginalSkuPrice,
                req.ReplacementSku,
                req.ReplacementSkuPrice,
                req.ExchangeReason,
                req.CourierName ?? "Aramex");

            var manifest = await service.InitiateReverseExchangeAsync(effectiveTenantId, initiationReq, ct);
            return Results.Ok(manifest);
        })
        .WithName("CreateReverseExchange")
        .WithSummary("Initiate doorstep reverse swap delivery and generate dual linked waybills");

        // 5. POST /api/v1/logistics/reverse-exchange/quarantine-inspect
        group.MapPost("/reverse-exchange/quarantine-inspect", async (
            [FromBody] QuarantineInspectApiRequest req,
            [FromServices] IReverseExchangeService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);

            var inspectionReq = new QuarantineInspectionRequest(
                effectiveTenantId,
                req.ExchangeId,
                req.ScannedReturnBarcode,
                req.Condition,
                req.InspectorNotes,
                req.InspectorId);

            var result = await service.ProcessQuarantineInspectionAsync(effectiveTenantId, inspectionReq, ct);
            return Results.Ok(result);
        })
        .WithName("QuarantineInspectReverseItem")
        .WithSummary("Warehouse quarantine intake inspection: verify barcode, record grade, release refund or flag fraud");

        // 6. POST /api/v1/logistics/remittance/reconcile-batch
        group.MapPost("/remittance/reconcile-batch", async (
            [FromBody] ReconcileBatchApiRequest req,
            [FromServices] ICourierRemittanceService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);

            var batchReq = new CourierRemittanceBatchRequest(
                effectiveTenantId,
                req.StatementReference,
                req.CourierName,
                req.StatementDateUtc ?? DateTime.UtcNow,
                req.Rows ?? []);

            var report = await service.ReconcileBatchStatementAsync(effectiveTenantId, batchReq, ct);
            return Results.Ok(report);
        })
        .WithName("ReconcileCourierRemittanceBatch")
        .WithSummary("Ingest courier remittance statement and automatically detect shortfalls, fee disputes, or missing waybills");

        // 7. GET /api/v1/logistics/remittance/summary
        group.MapGet("/remittance/summary", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? courierName,
            [FromServices] ICourierRemittanceService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var summary = await service.GetRemittanceSummaryAsync(effectiveTenantId, courierName, ct);
            return Results.Ok(summary);
        })
        .WithName("GetCourierRemittanceSummary")
        .WithSummary("Multi-stage COD cash ledger reconciliation summary across all stages and carriers");

        return app;
    }
}
