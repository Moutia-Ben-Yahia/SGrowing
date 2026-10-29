using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.SEO;

public record CrawledPageLinkNodeDto(
    string Url,
    string Title,
    string? MetaDescription,
    List<string> OutboundInternalLinks,
    string? RedirectTargetUrl = null
);

public record MultipageAnalysisResultDto(
    Guid ReportId,
    string RootDomain,
    int TotalPagesDiscovered,
    int OrphanPageCount,
    List<string> OrphanPageUrls,
    int DuplicateTitleClusterCount,
    Dictionary<string, List<string>> DuplicateTitleClusters,
    int RedirectChainCount,
    List<string> RedirectChains,
    DateTime EvaluatedAtUtc
);

public interface ISeoMultipageGraphService
{
    Task<MultipageAnalysisResultDto> AnalyzeMultipageGraphAsync(
        Guid tenantId,
        string rootDomain,
        List<string> sitemapUrls,
        List<CrawledPageLinkNodeDto> crawledGraph,
        Guid? auditReportId = null);

    Task<SeoMultipageGraphReport?> GetLatestMultipageReportAsync(
        Guid tenantId,
        string rootDomain);
}
