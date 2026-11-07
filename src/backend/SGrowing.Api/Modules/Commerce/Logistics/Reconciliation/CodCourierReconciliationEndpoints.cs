using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SGrowing.Api.Common;

namespace SGrowing.Api.Modules.Commerce.Logistics.Reconciliation;

/// <summary>
/// Minimal API endpoints for COD courier remittance and settlement reconciliation (PRD Domain G3 & MENA Regional E-Commerce).
/// </summary>
public class CodCourierReconciliationEndpoints : IEndpointModule
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/logistics/reconciliation")
            .WithTags("Commerce - COD Courier Remittance & Settlement Reconciliation");

        group.MapPost("/batches", async (
            IngestCourierRemittanceRequest request,
            ICodCourierReconciliationService service) =>
        {
            var tenantId = request.TenantId != Guid.Empty ? request.TenantId : FallbackTenantId;
            var normalizedRequest = request with { TenantId = tenantId };
            var batch = await service.IngestRemittanceBatchAsync(normalizedRequest);
            return Results.Created($"/api/v1/logistics/reconciliation/batches/{batch.BatchId}", batch);
        })
        .WithName("IngestCourierRemittanceBatch")
        .WithSummary("Ingests and reconciles an incoming courier remittance statement bordereau against internal COD orders.");

        group.MapGet("/batches", async (
            Guid? tenantId,
            ICodCourierReconciliationService service) =>
        {
            var effectiveTenantId = tenantId.GetValueOrDefault(FallbackTenantId);
            var batches = await service.GetBatchesAsync(effectiveTenantId);
            return Results.Ok(batches);
        })
        .WithName("GetCourierRemittanceBatches")
        .WithSummary("Lists all remittance batches processed for a merchant tenant.");

        group.MapGet("/batches/{id:guid}", async (
            Guid id,
            Guid? tenantId,
            ICodCourierReconciliationService service) =>
        {
            var effectiveTenantId = tenantId.GetValueOrDefault(FallbackTenantId);
            var batch = await service.GetBatchAsync(effectiveTenantId, id);
            return batch != null ? Results.Ok(batch) : Results.NotFound(new { message = $"Remittance batch {id} not found." });
        })
        .WithName("GetCourierRemittanceBatchById")
        .WithSummary("Retrieves detailed line item reconciliation for a specific courier remittance batch.");

        group.MapGet("/summary", async (
            Guid? tenantId,
            ICodCourierReconciliationService service) =>
        {
            var effectiveTenantId = tenantId.GetValueOrDefault(FallbackTenantId);
            var summary = await service.GetSummaryReportAsync(effectiveTenantId);
            return Results.Ok(summary);
        })
        .WithName("GetCourierReconciliationSummary")
        .WithSummary("Returns executive COD reconciliation KPIs, discrepancy breakdowns, and courier performance scorecards.");

        group.MapPost("/disputes/file", async (
            FileDisputeRequest request,
            ICodCourierReconciliationService service) =>
        {
            var tenantId = request.TenantId != Guid.Empty ? request.TenantId : FallbackTenantId;
            var dispute = await service.FileDisputeAsync(
                tenantId,
                request.BatchId,
                request.TrackingNumber,
                request.Reason
            );
            return Results.Ok(dispute);
        })
        .WithName("FileCourierDiscrepancyDispute")
        .WithSummary("Files an official settlement dispute against a courier for remittance shortfall, transit loss, or unjustified return fee.");

        group.MapGet("/disputes", async (
            Guid? tenantId,
            ICodCourierReconciliationService service) =>
        {
            var effectiveTenantId = tenantId.GetValueOrDefault(FallbackTenantId);
            var disputes = await service.GetDisputesAsync(effectiveTenantId);
            return Results.Ok(disputes);
        })
        .WithName("GetCourierDisputes")
        .WithSummary("Lists all courier remittance disputes filed by the merchant.");
    }
}
