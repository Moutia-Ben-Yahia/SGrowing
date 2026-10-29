using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SGrowing.Core.Tenancy;

namespace SGrowing.Api.Modules.SEO;

public static class TechnicalSeoCrawlerEndpoints
{
    private static readonly Guid FallbackTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapTechnicalSeoCrawlerEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Crawler Engine Endpoints
        var crawlerGroup = app.MapGroup("/api/v1/seo/crawler")
            .WithTags("TechnicalSeoCrawler");

        crawlerGroup.MapPost("/start", async (
            [FromBody] StartCrawlRequest req,
            ITechnicalSeoCrawlerService service,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var tenantId = req.TenantId
                ?? (tenantContext != null && tenantContext.HasTenant ? tenantContext.TenantId : FallbackTenantId);

            if (string.IsNullOrWhiteSpace(req.Url))
            {
                return Results.BadRequest(new { error = "Starting URL is required." });
            }

            try
            {
                var summary = await service.StartCrawlAsync(
                    tenantId,
                    req.Url,
                    req.MaxPages ?? 50,
                    req.MaxDepth ?? 3,
                    ct);

                return Results.Ok(summary);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        crawlerGroup.MapGet("/{crawlId:guid}/summary", async (
            Guid crawlId,
            [FromQuery] Guid? tenantId,
            ITechnicalSeoCrawlerService service,
            ITenantContext? tenantContext) =>
        {
            var effectiveTenantId = tenantId
                ?? (tenantContext != null && tenantContext.HasTenant ? tenantContext.TenantId : FallbackTenantId);

            var summary = await service.GetCrawlSummaryAsync(effectiveTenantId, crawlId);
            return summary != null ? Results.Ok(summary) : Results.NotFound(new { error = "Crawl report not found." });
        });

        crawlerGroup.MapGet("/{crawlId:guid}/pages", async (
            Guid crawlId,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? filter,
            [FromQuery] Guid? tenantId,
            ITechnicalSeoCrawlerService service,
            ITenantContext? tenantContext) =>
        {
            var effectiveTenantId = tenantId
                ?? (tenantContext != null && tenantContext.HasTenant ? tenantContext.TenantId : FallbackTenantId);

            var result = await service.GetCrawledPagesAsync(
                effectiveTenantId,
                crawlId,
                page ?? 1,
                pageSize ?? 20,
                filter);

            return Results.Ok(result);
        });

        crawlerGroup.MapGet("/{crawlId:guid}/link-graph", async (
            Guid crawlId,
            [FromQuery] Guid? tenantId,
            ITechnicalSeoCrawlerService service,
            ITenantContext? tenantContext) =>
        {
            var effectiveTenantId = tenantId
                ?? (tenantContext != null && tenantContext.HasTenant ? tenantContext.TenantId : FallbackTenantId);

            var graph = await service.GetLinkGraphAsync(effectiveTenantId, crawlId);
            return graph != null ? Results.Ok(graph) : Results.NotFound(new { error = "Link graph not found for crawl ID." });
        });

        // 2. SERP Keyword Tracker Endpoints
        var serpGroup = app.MapGroup("/api/v1/seo/serp")
            .WithTags("SerpRankTracker");

        serpGroup.MapPost("/track-keyword", async (
            [FromBody] TrackKeywordRequest req,
            ITechnicalSeoCrawlerService service,
            ITenantContext? tenantContext) =>
        {
            var effectiveTenantId = req.TenantId
                ?? (tenantContext != null && tenantContext.HasTenant ? tenantContext.TenantId : FallbackTenantId);

            if (string.IsNullOrWhiteSpace(req.Keyword) || string.IsNullOrWhiteSpace(req.TargetUrl))
            {
                return Results.BadRequest(new { error = "Keyword and TargetUrl are required." });
            }

            var record = await service.TrackKeywordAsync(
                effectiveTenantId,
                req.Keyword,
                req.TargetUrl,
                req.CountryCode ?? "TN",
                req.InitialPosition,
                req.SearchEngine ?? "Google");

            return Results.Ok(record);
        });

        serpGroup.MapGet("/rankings", async (
            [FromQuery] string? countryCode,
            [FromQuery] Guid? tenantId,
            ITechnicalSeoCrawlerService service,
            ITenantContext? tenantContext) =>
        {
            var effectiveTenantId = tenantId
                ?? (tenantContext != null && tenantContext.HasTenant ? tenantContext.TenantId : FallbackTenantId);

            var rankings = await service.GetSerpRankingsAsync(effectiveTenantId, countryCode);
            return Results.Ok(rankings);
        });

        return app;
    }
}
