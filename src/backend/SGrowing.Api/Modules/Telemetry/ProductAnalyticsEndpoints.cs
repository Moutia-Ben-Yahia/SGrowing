using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Telemetry;

public record FastIngestRequest(
    string? EventId,
    Guid? TenantId,
    string? UserId,
    string? SessionId,
    string? EventType,
    string? EventName,
    DateTime? TimestampUtc,
    string? Url,
    string? Referrer,
    string? DeviceType,
    string? Os,
    string? Browser,
    string? AppVersion,
    Dictionary<string, object?>? Properties,
    CrashReportPayload? CrashData,
    List<TelemetryIngestEvent>? Batch
);

public record UpdateCrashStatusRequest(string Status);

public static class ProductAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapProductAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/telemetry/analytics")
            .WithTags("Horizon 51 • High-Velocity Product Telemetry & Cohort Retention Analytics Engine");

        // 1. POST /api/v1/telemetry/analytics/i - Fast Ingestion
        group.MapPost("/i", async ([FromBody] FastIngestRequest req, IProductAnalyticsService service) =>
        {
            if (req.Batch != null && req.Batch.Count > 0)
            {
                var batchResult = await service.IngestBatchAsync(req.Batch);
                return Results.Accepted(value: batchResult);
            }

            var evt = new TelemetryIngestEvent(
                EventId: req.EventId,
                TenantId: req.TenantId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
                UserId: string.IsNullOrWhiteSpace(req.UserId) ? "anonymous" : req.UserId,
                SessionId: string.IsNullOrWhiteSpace(req.SessionId) ? Guid.NewGuid().ToString("N") : req.SessionId,
                EventType: string.IsNullOrWhiteSpace(req.EventType) ? "pageview" : req.EventType,
                EventName: string.IsNullOrWhiteSpace(req.EventName) ? "page_view" : req.EventName,
                TimestampUtc: req.TimestampUtc ?? DateTime.UtcNow,
                Url: string.IsNullOrWhiteSpace(req.Url) ? "/" : req.Url,
                Referrer: req.Referrer,
                DeviceType: req.DeviceType ?? "Desktop",
                Os: req.Os ?? "Web",
                Browser: req.Browser ?? "Chrome",
                AppVersion: req.AppVersion ?? "1.0.0",
                Properties: req.Properties,
                CrashData: req.CrashData
            );

            var result = await service.IngestEventAsync(evt);
            return Results.Accepted(value: result);
        })
        .WithName("IngestProductTelemetryEvent")
        .WithSummary("High-velocity client telemetry ingestion endpoint with in-memory thread-safe buffer.");

        // 2. GET /api/v1/telemetry/analytics/metrics/active-users
        group.MapGet("/metrics/active-users", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] int? days,
            IProductAnalyticsService service) =>
        {
            var metrics = await service.GetActiveUsersMetricsAsync(tenantId, days ?? 30);
            return Results.Ok(metrics);
        })
        .WithName("GetActiveUsersMetrics")
        .WithSummary("Get DAU, WAU, MAU, stickiness ratio, and time-series rollup aggregates.");

        // 3. GET /api/v1/telemetry/analytics/cohorts/retention
        group.MapGet("/cohorts/retention", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? granularity,
            [FromQuery] int? cohortCount,
            IProductAnalyticsService service) =>
        {
            var matrix = await service.GetCohortRetentionMatrixAsync(tenantId, granularity ?? "weekly", cohortCount ?? 8);
            return Results.Ok(matrix);
        })
        .WithName("GetCohortRetentionMatrix")
        .WithSummary("Get Day 0 to Day 30 retention percentages for user acquisition cohorts.");

        // 4. GET /api/v1/telemetry/analytics/crashes/clusters
        group.MapGet("/crashes/clusters", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] string? severity,
            [FromQuery] string? status,
            IProductAnalyticsService service) =>
        {
            var clusters = await service.GetCrashClustersAsync(tenantId, severity, status);
            return Results.Ok(clusters);
        })
        .WithName("GetCrashClusters")
        .WithSummary("Get application crash diagnostics grouped by deterministic SHA-256 error signatures.");

        // 5. POST /api/v1/telemetry/analytics/funnels/evaluate
        group.MapPost("/funnels/evaluate", async (
            [FromBody] EvaluateFunnelRequestDto req,
            IProductAnalyticsService service) =>
        {
            var evaluation = await service.EvaluateFunnelAsync(req);
            return Results.Ok(evaluation);
        })
        .WithName("EvaluateFunnelConversion")
        .WithSummary("Evaluate conversion rates and drop-off percentages across multi-step user journeys.");

        // 6. GET /api/v1/telemetry/analytics/recent-events
        group.MapGet("/recent-events", async (
            [FromQuery] Guid? tenantId,
            [FromQuery] int? limit,
            [FromQuery] string? eventType,
            [FromQuery] string? userId,
            IProductAnalyticsService service) =>
        {
            var events = await service.GetRecentEventsAsync(tenantId, limit ?? 50, eventType, userId);
            return Results.Ok(events);
        })
        .WithName("GetRecentTelemetryEvents")
        .WithSummary("Inspect recent buffered and stored telemetry events.");

        // Optional convenience management endpoints
        group.MapPost("/buffer/flush", async (
            [FromQuery] int? maxBatchSize,
            IProductAnalyticsService service) =>
        {
            var count = await service.FlushBufferAsync(maxBatchSize ?? 1000);
            return Results.Ok(new
            {
                flushedCount = count,
                pendingRemaining = service.GetPendingBufferCount(),
                totalStored = service.GetTotalStoredEventsCount()
            });
        })
        .WithName("FlushProductTelemetryBuffer")
        .WithSummary("Manually trigger batch flush of in-memory telemetry buffer.");

        group.MapPost("/crashes/{signature}/status", async (
            [FromRoute] string signature,
            [FromBody] UpdateCrashStatusRequest req,
            IProductAnalyticsService service) =>
        {
            var updated = await service.UpdateCrashStatusAsync(signature, req.Status);
            if (!updated)
            {
                return Results.NotFound(new { error = $"Crash cluster '{signature}' not found." });
            }
            return Results.Ok(new { success = true, signature, status = req.Status });
        })
        .WithName("UpdateCrashClusterStatus")
        .WithSummary("Update triage status of a crash cluster.");

        return app;
    }
}
