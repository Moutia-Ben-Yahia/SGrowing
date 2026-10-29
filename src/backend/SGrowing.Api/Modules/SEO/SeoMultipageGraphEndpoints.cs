using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.SEO;

public record AnalyzeMultipageRequest(
    string RootDomain,
    List<string> SitemapUrls,
    List<CrawledPageLinkNodeDto> CrawledGraph,
    Guid? AuditReportId = null
);

public static class SeoMultipageGraphEndpoints
{
    public static IEndpointRouteBuilder MapSeoMultipageGraphEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seo/multipage")
            .WithTags("SeoMultipageGraph");

        group.MapPost("/analyze", async (
            Guid tenantId,
            AnalyzeMultipageRequest req,
            ISeoMultipageGraphService service) =>
        {
            var result = await service.AnalyzeMultipageGraphAsync(
                tenantId,
                req.RootDomain,
                req.SitemapUrls,
                req.CrawledGraph,
                req.AuditReportId);

            return Results.Ok(result);
        });

        group.MapGet("/reports/latest", async (
            Guid tenantId,
            string rootDomain,
            ISeoMultipageGraphService service) =>
        {
            var report = await service.GetLatestMultipageReportAsync(tenantId, rootDomain);
            return report != null ? Results.Ok(report) : Results.NotFound();
        });

        return app;
    }
}
