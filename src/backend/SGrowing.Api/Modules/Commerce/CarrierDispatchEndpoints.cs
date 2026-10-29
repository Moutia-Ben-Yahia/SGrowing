using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Commerce;

#region Request Contracts

public record CarrierAutoRouteRequest(
    Guid? TenantId,
    Guid? OrderId,
    string? Governorate,
    decimal? CodAmount,
    string? CountryCode,
    decimal? PackageWeightKg);

public record GenerateManifestRequest(
    Guid? TenantId,
    Guid CourierProviderId,
    List<Guid> OrderIds,
    string? CreatedBy);

public record ReconcileRemittanceRequest(
    Guid? TenantId,
    Guid CourierProviderId,
    string StatementReference,
    string? CsvContent,
    List<ReportedParcelRowDto>? ParsedRows,
    string? ReconciledBy);

public record ResolveDiscrepancyApiRequest(
    Guid? TenantId,
    string ReasonCode,
    string Notes,
    string? ResolvedBy);

#endregion

public static class CarrierDispatchEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapCarrierDispatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/carriers")
            .WithTags("Regional Carrier Auto-Dispatch & COD Cash Reconciliation");

        // 1. GET /matrix: Get carrier performance matrix across all governorates/regions
        group.MapGet("/matrix", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? countryCode,
            [FromQuery] string? governorate,
            [FromServices] ICodCarrierAutoDispatchService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var matrix = await service.GetCarrierRoutingMatrixAsync(effectiveTenantId, countryCode, governorate);
            return Results.Ok(matrix);
        })
        .WithName("GetCarrierRoutingMatrix")
        .WithSummary("Retrieve regional courier performance and expected net margin matrix across governorates");

        // 2. POST /auto-route: Evaluate best carrier for an order and auto-assign
        group.MapPost("/auto-route", async (
            [FromBody] CarrierAutoRouteRequest req,
            [FromServices] ICodCarrierAutoDispatchService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);

            if (req.OrderId.HasValue && req.OrderId.Value != Guid.Empty)
            {
                var assigned = await service.AutoRouteAndAssignOrderAsync(effectiveTenantId, req.OrderId.Value, req.PackageWeightKg);
                return Results.Ok(assigned);
            }

            var gov = string.IsNullOrWhiteSpace(req.Governorate) ? "Tunis" : req.Governorate;
            var cod = req.CodAmount ?? 85.0m;
            var country = req.CountryCode ?? "TN";
            var weight = req.PackageWeightKg ?? 1.0m;

            var ranking = await service.EvaluateCarrierRankingAsync(effectiveTenantId, gov, cod, country, weight);
            return Results.Ok(new
            {
                Governorate = gov,
                CodAmount = cod,
                CountryCode = country,
                RankedCarriers = ranking,
                TopRecommendation = ranking.FirstOrDefault()
            });
        })
        .WithName("AutoRouteCarrier")
        .WithSummary("Evaluate optimal regional courier based on historical delivery success rates and return penalties");

        // 3. POST /manifests/generate: Create locked shipping manifest batch for pickup
        group.MapPost("/manifests/generate", async (
            [FromBody] GenerateManifestRequest req,
            [FromServices] ICodCarrierAutoDispatchService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var manifest = await service.GenerateManifestBatchAsync(effectiveTenantId, req.CourierProviderId, req.OrderIds, req.CreatedBy);
            return Results.Created($"/api/v1/commerce/carriers/manifests/{manifest.ManifestId}", manifest);
        })
        .WithName("GenerateShippingManifest")
        .WithSummary("Aggregate confirmed orders into a locked courier manifest batch with sequential AWB barcodes");

        // 4. GET /manifests: List shipping manifests with status
        group.MapGet("/manifests", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? status,
            [FromServices] ICodCarrierAutoDispatchService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var manifests = await service.ListManifestsAsync(effectiveTenantId, status);
            return Results.Ok(manifests);
        })
        .WithName("ListShippingManifests")
        .WithSummary("List shipping manifests with filtering by status (PendingPickup, Dispatched, Settled)");

        // 5. GET /manifests/{manifestId:guid}: Get detailed manifest with parcels
        group.MapGet("/manifests/{manifestId:guid}", async (
            Guid manifestId,
            [FromQuery] Guid? tenantId,
            [FromServices] ICodCarrierAutoDispatchService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var manifest = await service.GetManifestByIdAsync(effectiveTenantId, manifestId);
            return Results.Ok(manifest);
        })
        .WithName("GetShippingManifestById")
        .WithSummary("Get shipping manifest details, barcode representation, and parcel run sheet");

        // 6. POST /remittance/reconcile: Upload/process carrier statement and compute match audit
        group.MapPost("/remittance/reconcile", async (
            [FromBody] ReconcileRemittanceRequest req,
            [FromServices] ICodCarrierAutoDispatchService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var auditReport = await service.ReconcileRemittanceStatementAsync(
                effectiveTenantId,
                req.CourierProviderId,
                req.StatementReference,
                req.CsvContent,
                req.ParsedRows,
                req.ReconciledBy ?? "FinanceManager");

            return Results.Ok(auditReport);
        })
        .WithName("ReconcileCarrierRemittance")
        .WithSummary("Ingest courier CSV statement, cross-reference shipments, detect phantom returns, and calculate net payout");

        // 7. POST /remittance/{matchId:guid}/resolve: Resolve discrepancy with audit notes
        group.MapPost("/remittance/{matchId:guid}/resolve", async (
            Guid matchId,
            [FromBody] ResolveDiscrepancyApiRequest req,
            [FromServices] ICodCarrierAutoDispatchService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = req.TenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var resolvedMatch = await service.ResolveDiscrepancyAsync(
                effectiveTenantId,
                matchId,
                new DiscrepancyResolutionRequest(req.ReasonCode, req.Notes, req.ResolvedBy ?? "FinanceManager"));

            return Results.Ok(resolvedMatch);
        })
        .WithName("ResolveRemittanceDiscrepancy")
        .WithSummary("Resolve a remittance discrepancy with reason code and audit notes, adjusting holdback");

        // 8. GET /kpis: Get financial reconciliation KPIs
        group.MapGet("/kpis", async (
            [FromQuery] Guid? tenantId,
            [FromServices] ICodCarrierAutoDispatchService service,
            [FromServices] ITenantContext tenant,
            CancellationToken ct) =>
        {
            var effectiveTenantId = tenantId ?? (tenant.HasTenant ? tenant.TenantId : FallbackTenantId);
            var kpis = await service.GetReconciliationKpisAsync(effectiveTenantId);
            return Results.Ok(kpis);
        })
        .WithName("GetCarrierReconciliationKpis")
        .WithSummary("Retrieve high-level financial KPIs: COD in transit, collected cash, cleared settlement, and discrepancy holdbacks");

        return app;
    }
}
