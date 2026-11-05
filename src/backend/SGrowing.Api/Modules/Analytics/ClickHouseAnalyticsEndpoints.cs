using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Common;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.Analytics;

public record FunnelQueryRequest(
    DateTime StartUtc,
    DateTime EndUtc,
    IReadOnlyList<string> Steps
);

public class ClickHouseAnalyticsEndpoints : IEndpointModule
{
    private static Guid ResolveTenantId(HttpContext context, ITenantContext tenantContext)
    {
        if (tenantContext.HasTenant && tenantContext.TenantId != Guid.Empty)
        {
            return tenantContext.TenantId;
        }

        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var h) && Guid.TryParse(h.ToString(), out var parsedId))
        {
            return parsedId;
        }

        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/analytics/olap")
            .WithTags("ClickHouse Analytics & OLAP Engine");

        group.MapGet("/realtime-roas", async (
            HttpContext context,
            ITenantContext tenantContext,
            DateTime? startUtc,
            DateTime? endUtc,
            string? channel,
            IClickHouseAnalyticsProjectionService olapService) =>
        {
            var tenantId = ResolveTenantId(context, tenantContext);
            var start = startUtc ?? DateTime.UtcNow.AddDays(-30);
            var end = endUtc ?? DateTime.UtcNow;

            var result = await olapService.GetRealtimeRoasAsync(tenantId, start, end, channel, context.RequestAborted);
            return Results.Ok(result);
        })
        .WithName("GetRealtimeRoas")
        .WithSummary("Retrieves sub-second aggregated ROAS and CPA metrics across ad channels.");

        group.MapPost("/funnel-conversion", async (
            HttpContext context,
            ITenantContext tenantContext,
            FunnelQueryRequest request,
            IClickHouseAnalyticsProjectionService olapService) =>
        {
            var tenantId = ResolveTenantId(context, tenantContext);
            var result = await olapService.GetFunnelConversionAsync(
                tenantId,
                request.StartUtc,
                request.EndUtc,
                request.Steps,
                context.RequestAborted);

            return Results.Ok(result);
        })
        .WithName("GetFunnelConversion")
        .WithSummary("Calculates multi-step conversion funnel drop-off rates from ClickHouse columnar store.");

        group.MapGet("/agent-telemetry-summary", async (
            HttpContext context,
            ITenantContext tenantContext,
            DateTime? startUtc,
            DateTime? endUtc,
            IClickHouseAnalyticsProjectionService olapService) =>
        {
            var tenantId = ResolveTenantId(context, tenantContext);
            var start = startUtc ?? DateTime.UtcNow.AddDays(-7);
            var end = endUtc ?? DateTime.UtcNow;

            var result = await olapService.GetAgentExecutionTelemetryAggregationAsync(tenantId, start, end, context.RequestAborted);
            return Results.Ok(result);
        })
        .WithName("GetAgentTelemetrySummary")
        .WithSummary("Aggregates token consumption, mission durations, and success rates per autonomous agent persona.");

        group.MapPost("/flush", async (
            IClickHouseAnalyticsProjectionService olapService) =>
        {
            var flushedCount = await olapService.FlushPendingBatchesAsync();
            return Results.Ok(new { Flushed = flushedCount, Status = "Flushed" });
        })
        .WithName("FlushClickHouseOlapBatches")
        .WithSummary("Forces flushing of in-memory micro-batches to columnar persistent store.");
    }
}
