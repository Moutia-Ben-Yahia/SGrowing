using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Attribution;

public static class ServerSideAttributionEndpoints
{
    public static IEndpointRouteBuilder MapServerSideAttributionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/attribution/capi")
            .WithTags("Horizon 47 • Server-Side Ad Attribution Postback Engine & CAPI Gateway");

        // 1. POST /api/v1/attribution/capi/track-event
        group.MapPost("/track-event", async (
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

        // 2. POST /api/v1/attribution/capi/dispatch-postbacks
        group.MapPost("/dispatch-postbacks", async (
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

        // 3. GET /api/v1/attribution/capi/metrics
        group.MapGet("/metrics", async (
            [FromQuery] Guid? tenantId,
            IServerSideAttributionService service) =>
        {
            var metrics = await service.GetMetricsAsync(tenantId);
            return Results.Ok(metrics);
        })
        .WithName("GetCapiAttributionMetrics")
        .WithSummary("Get composite EMQ scores by platform, ad-blocker revenue recovery statistics, and outbox throughput.");

        // 4. GET /api/v1/attribution/capi/events
        group.MapGet("/events", async (
            [FromQuery] int? limit,
            [FromQuery] Guid? tenantId,
            IServerSideAttributionService service) =>
        {
            var events = await service.GetRecentEventsAsync(limit ?? 50, tenantId);
            return Results.Ok(events);
        })
        .WithName("GetCapiRecentEvents")
        .WithSummary("List recently tracked server-side conversion events with EMQ scores and hashed PII metadata.");

        // 5. GET /api/v1/attribution/capi/outbox
        group.MapGet("/outbox", async (
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

        // 6. POST /api/v1/attribution/capi/test-pixel
        group.MapPost("/test-pixel", async (
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

        // 7. POST /api/v1/attribution/capi/outbox/retry/{postbackId}
        group.MapPost("/outbox/retry/{postbackId}", async (
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
