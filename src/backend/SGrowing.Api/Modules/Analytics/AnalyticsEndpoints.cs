using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Analytics;

public record RecordTouchpointRequest(
    Guid TenantId,
    Guid ContactId,
    string Channel,
    string CampaignName,
    TouchpointStage Stage,
    Guid? DealId = null,
    decimal Cost = 0,
    string? UtmSource = null,
    string? UtmMedium = null,
    string? UtmCampaign = null
);

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/analytics")
            .WithTags("Multi-Touch Attribution, ROAS & Funnel Analytics");

        group.MapPost("/attribution/touchpoints", async (
            [FromBody] RecordTouchpointRequest req,
            MultiTouchAttributionService service,
            CancellationToken ct) =>
        {
            var tp = await service.RecordTouchpointAsync(
                req.TenantId,
                req.ContactId,
                req.Channel,
                req.CampaignName,
                req.Stage,
                req.DealId,
                req.Cost,
                req.UtmSource,
                req.UtmMedium,
                req.UtmCampaign,
                ct);

            return Results.Ok(tp);
        });

        group.MapGet("/attribution/touchpoints", async (
            [FromQuery] Guid tenantId,
            SGrowingDbContext db,
            CancellationToken ct) =>
        {
            var touchpoints = await db.AttributionTouchpoints
                .Where(t => t.TenantId == tenantId)
                .OrderByDescending(t => t.OccurredAtUtc)
                .Take(50)
                .ToListAsync(ct);

            return Results.Ok(touchpoints);
        });

        group.MapGet("/attribution/deal/{dealId:guid}", async (
            Guid dealId,
            [FromQuery] Guid tenantId,
            [FromQuery] AttributionModelType? model,
            MultiTouchAttributionService service,
            CancellationToken ct) =>
        {
            var selectedModel = model ?? AttributionModelType.TimeDecay;
            var results = await service.CalculateAttributionAsync(tenantId, dealId, selectedModel, ct);
            return Results.Ok(new
            {
                dealId,
                model = selectedModel.ToString(),
                attributions = results
            });
        });

        group.MapGet("/attribution/kpis", async (
            [FromQuery] Guid tenantId,
            MultiTouchAttributionService service,
            CancellationToken ct) =>
        {
            var kpis = await service.CalculateExecutiveKpisAsync(tenantId, ct);
            return Results.Ok(kpis);
        });

        return routes;
    }
}
