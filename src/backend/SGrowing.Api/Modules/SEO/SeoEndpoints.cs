using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.SEO;

public record RunSeoAuditDto(Guid TenantId, string TargetUrl, List<PageAuditInputDto>? Pages = null);
public record TrackKeywordDto(Guid TenantId, string Keyword, string TargetUrl, string CountryCode = "TN", int? InitialRank = null, int SearchVolume = 1000);
public record UpdateRankDto(Guid TenantId, int NewRank);
public record RecordAeoDto(Guid TenantId, string BrandName, string AiEngine, string PromptQuery, bool IsMentioned, string Sentiment, string Snippet, int? CitationRank = 1);

public static class SeoEndpoints
{
    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/seo").WithTags("Technical SEO & SERP Hub");

        group.MapPost("/audits/run", async ([FromBody] RunSeoAuditDto req, ISeoAuditService service) =>
        {
            var report = await service.RunAuditAsync(req.TenantId, req.TargetUrl, req.Pages);
            var pages = await service.GetAuditPagesAsync(req.TenantId, report.Id);
            return Results.Ok(new { report, pages });
        });

        group.MapGet("/audits/latest", async ([FromQuery] Guid tenantId, ISeoAuditService service) =>
        {
            var report = await service.GetLatestAuditAsync(tenantId);
            if (report == null)
            {
                return Results.Ok(new { report = (object?)null, pages = new object[0] });
            }
            var pages = await service.GetAuditPagesAsync(tenantId, report.Id);
            return Results.Ok(new { report, pages });
        });

        group.MapGet("/audits/{id:guid}/pages", async (Guid id, [FromQuery] Guid tenantId, ISeoAuditService service) =>
        {
            var pages = await service.GetAuditPagesAsync(tenantId, id);
            return Results.Ok(pages);
        });

        group.MapPost("/keywords", async ([FromBody] TrackKeywordDto req, ISeoAuditService service) =>
        {
            var keyword = await service.TrackKeywordAsync(req.TenantId, req.Keyword, req.TargetUrl, req.CountryCode, req.InitialRank, req.SearchVolume);
            return Results.Ok(keyword);
        });

        group.MapPost("/keywords/{id:guid}/rank", async (Guid id, [FromBody] UpdateRankDto req, ISeoAuditService service) =>
        {
            var updated = await service.UpdateKeywordRankAsync(req.TenantId, id, req.NewRank);
            return Results.Ok(updated);
        });

        group.MapGet("/keywords", async ([FromQuery] Guid tenantId, ISeoAuditService service) =>
        {
            var keywords = await service.GetTrackedKeywordsAsync(tenantId);
            return Results.Ok(keywords);
        });

        group.MapPost("/aeo/check", async ([FromBody] RecordAeoDto req, ISeoAuditService service) =>
        {
            var record = await service.RecordAeoMentionAsync(req.TenantId, req.BrandName, req.AiEngine, req.PromptQuery, req.IsMentioned, req.Sentiment, req.Snippet, req.CitationRank);
            return Results.Ok(record);
        });

        group.MapGet("/aeo/mentions", async ([FromQuery] Guid tenantId, ISeoAuditService service) =>
        {
            var mentions = await service.GetAeoMentionsAsync(tenantId);
            return Results.Ok(mentions);
        });

        return endpoints;
    }
}
