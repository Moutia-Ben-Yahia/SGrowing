using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Attribution;

public static class ServerSideAttributionEndpoints
{
    public static IEndpointRouteBuilder MapServerSideAttributionEndpoints(this IEndpointRouteBuilder app)
    {
        var attributionGroup = app.MapGroup("/api/v1/attribution")
            .WithTags("Phase 10 • Server-Side CDP, Deterministic Identity Stitching & Multi-Model Attribution");

        // 1. POST /api/v1/attribution/identity/stitch
        attributionGroup.MapPost("/identity/stitch", async (
            [FromBody] StitchIdentityEventRequest request,
            IDeterministicIdentityStitchingService stitchingService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await stitchingService.StitchIdentityAsync(request, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("StitchIdentity")
        .WithSummary("Unify first-party anonymous visitor UUID with canonical contact, merge fingerprints/IPs, and retroactively stitch touchpoints.");

        // 2. POST /api/v1/attribution/calculate-multi-model
        attributionGroup.MapPost("/calculate-multi-model", async (
            [FromBody] CalculateMultiModelRequest request,
            IMultiModelRevenueAttributionService attributionService,
            CancellationToken ct) =>
        {
            try
            {
                var matrix = await attributionService.CalculateMultiModelMatrixAsync(request, ct);
                return Results.Ok(matrix);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("CalculateMultiModelAttribution")
        .WithSummary("Compute full 6-model attribution matrix (First Touch, Last Touch, Linear, Time-Decay, U-Shape, Markov Chain / Shapley) for deal or cohort.");

        // 3. POST /api/v1/attribution/capi/dispatch
        attributionGroup.MapPost("/capi/dispatch", async (
            [FromBody] CapiDispatchEventRequest request,
            IServerSideCapiFanoutService fanoutService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await fanoutService.DispatchAsync(request, ct);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("DispatchCapiFanout")
        .WithSummary("High-throughput server-side fanout to Meta CAPI, Google Ads Enhanced Conversions, and TikTok Events API with circuit breakers.");

        // 4. GET /api/v1/attribution/funnel/slippage-report
        attributionGroup.MapGet("/funnel/slippage-report", async (
            [FromQuery] Guid? tenantId,
            IFunnelSlippageMonitorService monitorService,
            CancellationToken ct) =>
        {
            var report = await monitorService.GetSlippageReportAsync(tenantId ?? Guid.Empty, ct);
            return Results.Ok(report);
        })
        .WithName("GetFunnelSlippageReport")
        .WithSummary("Real-time funnel conversion metrics, 7-day moving averages, and statistical slippage alerts (>2 std dev drops).");

        // Existing CAPI group endpoints preserved
        var capiGroup = app.MapGroup("/api/v1/attribution/capi")
            .WithTags("Horizon 47 • Server-Side Ad Attribution Postback Engine & CAPI Gateway");

        // Existing 1. POST /api/v1/attribution/capi/track-event
        capiGroup.MapPost("/track-event", async (
            [FromBody] TrackConversionEventRequest request,
            IServerSideAttributionService service) =>
        {
            try
            {
                var result = await service.TrackConversionEventAsync(request);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("TrackCapiConversionEvent")
        .WithSummary("Track standard or custom conversion event, hash PII via SHA-256, score EMQ, and enqueue multi-platform CAPI postbacks.");

        // Existing 2. POST /api/v1/attribution/capi/dispatch-postbacks
        capiGroup.MapPost("/dispatch-postbacks", async (
            [FromQuery] int? batchSize,
            IServerSideAttributionService service) =>
        {
            try
            {
                var result = await service.DispatchPostbacksAsync(batchSize ?? 50);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("DispatchCapiPostbacks")
        .WithSummary("Trigger batch dispatch of pending CAPI outbox postbacks to Meta, TikTok, and Google Ads.");

        // Existing 3. GET /api/v1/attribution/capi/metrics
        capiGroup.MapGet("/metrics", async (
            [FromQuery] Guid? tenantId,
            IServerSideAttributionService service) =>
        {
            var metrics = await service.GetMetricsAsync(tenantId);
            return Results.Ok(metrics);
        })
        .WithName("GetCapiAttributionMetrics")
        .WithSummary("Get composite EMQ scores by platform, ad-blocker revenue recovery statistics, and outbox throughput.");

        // Existing 4. GET /api/v1/attribution/capi/events
        capiGroup.MapGet("/events", async (
            [FromQuery] int? limit,
            [FromQuery] Guid? tenantId,
            IServerSideAttributionService service) =>
        {
            var events = await service.GetRecentEventsAsync(limit ?? 50, tenantId);
            return Results.Ok(events);
        })
        .WithName("GetCapiRecentEvents")
        .WithSummary("List recently tracked server-side conversion events with EMQ scores and hashed PII metadata.");

        // Existing 5. GET /api/v1/attribution/capi/outbox
        capiGroup.MapGet("/outbox", async (
            [FromQuery] CapiPlatform? platform,
            [FromQuery] PostbackDeliveryStatus? status,
            [FromQuery] int? limit,
            IServerSideAttributionService service) =>
        {
            var items = await service.GetOutboxItemsAsync(platform, status, limit ?? 50);
            return Results.Ok(items);
        })
        .WithName("GetCapiOutboxItems")
        .WithSummary("List queued and dispatched postback payloads across Meta CAPI, TikTok Events API, and Google Ads.");

        // Existing 6. POST /api/v1/attribution/capi/test-pixel
        capiGroup.MapPost("/test-pixel", async (
            [FromBody] TestPixelRequest request,
            IServerSideAttributionService service) =>
        {
            try
            {
                var result = await service.TestPixelAsync(request);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("TestCapiPixelGateway")
        .WithSummary("Simulate test conversion event with platform test codes and evaluate payload structure and EMQ score.");

        // Existing 7. POST /api/v1/attribution/capi/outbox/retry/{postbackId}
        capiGroup.MapPost("/outbox/retry/{postbackId}", async (
            string postbackId,
            IServerSideAttributionService service) =>
        {
            var retried = await service.RetryPostbackAsync(postbackId);
            return retried
                ? Results.Ok(new { success = true, postbackId, status = "Pending" })
                : Results.NotFound(new { error = $"Postback with ID '{postbackId}' not found." });
        })
        .WithName("RetryCapiPostback")
        .WithSummary("Reset a failed or retrying postback to pending for immediate re-dispatch.");

        return app;
    }
}
