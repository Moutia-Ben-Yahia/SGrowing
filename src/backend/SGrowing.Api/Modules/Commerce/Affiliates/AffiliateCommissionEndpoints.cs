using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Commerce.Affiliates;

public static class AffiliateCommissionEndpoints
{
    public static IEndpointRouteBuilder MapAffiliateCommissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/commerce/affiliates")
            .WithTags("Affiliate Commissions & Multi-Tier Payout Settlements");

        // 1. GET /partners - List all affiliate partners for tenant
        group.MapGet("/partners", async (
            [FromQuery] AffiliateTierLevel? tier,
            [FromQuery] AffiliateStatus? status,
            [FromQuery] string? search,
            [FromQuery] string? tenantId,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            var tid = tenantId ?? "tenant-default";
            var partners = await service.GetPartnersAsync(tid, tier, status, search, ct);
            return Results.Ok(partners);
        })
        .WithName("ListAffiliatePartners")
        .WithSummary("List all registered affiliate partners and their performance tier hierarchy.");

        // 2. POST /partners - Register new affiliate partner with tier and payout details
        group.MapPost("/partners", async (
            [FromBody] RegisterAffiliateRequest request,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            try
            {
                var created = await service.RegisterPartnerAsync(request, ct);
                return Results.Created($"/api/v1/commerce/affiliates/partners/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("RegisterAffiliatePartner")
        .WithSummary("Register a new affiliate partner with tier hierarchy, custom payout details, and commission rules.");

        // 3. GET /partners/{id:guid} - Get affiliate partner profile and referral codes
        group.MapGet("/partners/{id:guid}", async (
            Guid id,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            var partner = await service.GetPartnerByIdAsync(id, ct);
            if (partner == null)
                return Results.NotFound(new { error = $"Affiliate partner '{id}' not found." });

            return Results.Ok(partner);
        })
        .WithName("GetAffiliatePartnerById")
        .WithSummary("Retrieve partner profile, hierarchical upline, active referral codes, and balance stats.");

        // 4. POST /track-conversion - Attribute sale/conversion to affiliate hierarchy and calculate multi-tier commissions
        group.MapPost("/track-conversion", async (
            [FromBody] TrackConversionRequest request,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.TrackConversionAsync(request, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("TrackAffiliateConversion")
        .WithSummary("Attribute an order to an affiliate, check attribution window, and generate L1/L2/L3 multi-tier commission ledger records.");

        // 5. POST /reverse-commission - Reverse commission due to order refund or dispute
        group.MapPost("/reverse-commission", async (
            [FromBody] ReverseCommissionRequest request,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            try
            {
                var reversed = await service.ReverseCommissionAsync(request, ct);
                return Results.Ok(new
                {
                    message = $"Successfully reversed {reversed.Count} commission transaction(s).",
                    reversedTransactions = reversed
                });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ReverseAffiliateCommission")
        .WithSummary("Claw back unreleased commissions when an order is refunded, cancelled, or disputed.");

        // 6. GET /commissions - List commission records with status, tier, and date filters
        group.MapGet("/commissions", async (
            [FromQuery] CommissionState? status,
            [FromQuery] AffiliateTierLevel? tier,
            [FromQuery] Guid? affiliateId,
            [FromQuery] string? orderId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? tenantId,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            var tid = tenantId ?? "tenant-default";
            var commissions = await service.GetCommissionsAsync(tid, status, tier, affiliateId, orderId, fromDate, toDate, ct);
            return Results.Ok(commissions);
        })
        .WithName("ListAffiliateCommissions")
        .WithSummary("Query commission ledger records filtered by lifecycle state, tier, affiliate ID, or order.");

        // 7. POST /settlements/batch - Generate batch payout settlement for approved commissions
        group.MapPost("/settlements/batch", async (
            [FromBody] GenerateSettlementBatchRequest request,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            try
            {
                var batch = await service.GeneratePayoutBatchAsync(request, ct);
                return Results.Created($"/api/v1/commerce/affiliates/settlements/{batch.Id}", batch);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GenerateBatchSettlement")
        .WithSummary("Batch aggregate approved matured commissions, compute tax withholding, and generate SEPA XML / PayPal CSV manifests.");

        // 8. GET /settlements - List settlement batch archives
        group.MapGet("/settlements", async (
            [FromQuery] string? tenantId,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            var tid = tenantId ?? "tenant-default";
            var batches = await service.GetSettlementBatchesAsync(tid, ct);
            return Results.Ok(batches);
        })
        .WithName("ListSettlementBatches")
        .WithSummary("List archived settlement payout batches and their execution summaries.");

        // 8b. GET /settlements/{id:guid} - Get settlement batch detail
        group.MapGet("/settlements/{id:guid}", async (
            Guid id,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            var batch = await service.GetSettlementBatchByIdAsync(id, ct);
            if (batch == null)
                return Results.NotFound(new { error = $"Settlement batch '{id}' not found." });

            return Results.Ok(batch);
        })
        .WithName("GetSettlementBatchById")
        .WithSummary("Retrieve detailed line items and manifests for a specific settlement batch.");

        // 8c. GET /settlements/{id:guid}/manifest - Download or view manifest in specified format
        group.MapGet("/settlements/{id:guid}/manifest", async (
            Guid id,
            [FromQuery] string? format,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            var batch = await service.GetSettlementBatchByIdAsync(id, ct);
            if (batch == null)
                return Results.NotFound(new { error = $"Settlement batch '{id}' not found." });

            var manifest = await service.GetBatchManifestAsync(id, format, ct);
            if (manifest == null)
                return Results.NotFound(new { error = "No manifest found for requested format." });

            var fmt = (format ?? batch.PayoutMethod.ToString()).ToLowerInvariant();
            if (fmt.Contains("sepa") || fmt.Contains("xml"))
                return Results.Content(manifest, "application/xml; charset=utf-8");

            if (fmt.Contains("paypal") || fmt.Contains("csv"))
                return Results.Content(manifest, "text/csv; charset=utf-8");

            return Results.Content(manifest, "text/plain; charset=utf-8");
        })
        .WithName("DownloadSettlementManifest")
        .WithSummary("Download SEPA XML (pain.001.001.03) or PayPal batch CSV manifest for direct bank or payment gateway ingestion.");

        // 9. GET /metrics - Aggregate revenue, active partners, pending escrow, and payout totals
        group.MapGet("/metrics", async (
            [FromQuery] string? tenantId,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            var tid = tenantId ?? "tenant-default";
            var metrics = await service.GetMetricsAsync(tid, ct);
            return Results.Ok(metrics);
        })
        .WithName("GetAffiliateCommissionMetrics")
        .WithSummary("Get macro affiliate performance analytics: referred GMV, commissions accrued, escrow volume, and conversion stats.");

        // Additional utility endpoints for testing and operations:
        // POST /track-click
        group.MapPost("/track-click", async (
            [FromQuery] string code,
            [FromQuery] string? customerId,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            try
            {
                var cookie = await service.TrackClickAsync(code, customerId, ct);
                return Results.Ok(cookie);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("TrackAffiliateClick")
        .WithSummary("Simulate or track an affiliate link click, returning an attribution cookie token with TTL.");

        // POST /maturity-sweep
        group.MapPost("/maturity-sweep", async (
            [FromQuery] DateTime? asOfUtc,
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            var count = await service.ProcessMaturitySweepAsync(asOfUtc, ct);
            return Results.Ok(new { message = $"Processed maturity sweep. {count} transaction(s) transitioned to Approved." });
        })
        .WithName("AffiliateMaturitySweep")
        .WithSummary("Process escrow holdback maturity evaluation, graduating matured commissions from PendingEscrow to Approved.");

        // POST /reset
        group.MapPost("/reset", async (
            [FromServices] IAffiliateCommissionService service,
            CancellationToken ct) =>
        {
            await service.ResetToPreseededDataAsync(ct);
            return Results.Ok(new { message = "Affiliate store reset to pre-seeded state." });
        })
        .WithName("ResetAffiliateStore")
        .WithSummary("Reset in-memory affiliate records and test data to default seeded fixtures.");

        return app;
    }
}
