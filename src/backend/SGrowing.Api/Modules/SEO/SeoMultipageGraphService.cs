using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.SEO;

public class SeoMultipageGraphService : ISeoMultipageGraphService
{
    private readonly SGrowingDbContext _db;

    public SeoMultipageGraphService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<MultipageAnalysisResultDto> AnalyzeMultipageGraphAsync(
        Guid tenantId,
        string rootDomain,
        List<string> sitemapUrls,
        List<CrawledPageLinkNodeDto> crawledGraph,
        Guid? auditReportId = null)
    {
        // 1. Build Inbound Link Graph (Destination URL -> Inbound Source URLs)
        var inboundMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // Normalize URLs
        foreach (var url in sitemapUrls)
        {
            var cleanUrl = CleanUrl(url);
            if (!inboundMap.ContainsKey(cleanUrl))
            {
                inboundMap[cleanUrl] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        foreach (var node in crawledGraph)
        {
            var cleanSource = CleanUrl(node.Url);
            if (!inboundMap.ContainsKey(cleanSource))
            {
                inboundMap[cleanSource] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var outbound in node.OutboundInternalLinks)
            {
                var cleanTarget = CleanUrl(outbound);
                if (!inboundMap.ContainsKey(cleanTarget))
                {
                    inboundMap[cleanTarget] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                if (!string.Equals(cleanSource, cleanTarget, StringComparison.OrdinalIgnoreCase))
                {
                    inboundMap[cleanTarget].Add(cleanSource);
                }
            }
        }

        // 2. Discover Orphan Pages (in sitemap or crawled, but 0 inbound links and not root homepage)
        var cleanRoot = CleanUrl(rootDomain);
        var orphanUrls = inboundMap
            .Where(kvp => kvp.Value.Count == 0 && !kvp.Key.Equals(cleanRoot, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        // 3. Discover Duplicate Titles
        var duplicateTitles = crawledGraph
            .Where(n => !string.IsNullOrWhiteSpace(n.Title))
            .GroupBy(n => n.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(n => CleanUrl(n.Url)).Distinct().ToList());

        // 4. Trace Redirect Chains (A -> B -> C)
        var redirectChains = new List<string>();
        var redirectMap = crawledGraph
            .Where(n => !string.IsNullOrWhiteSpace(n.RedirectTargetUrl))
            .ToDictionary(n => CleanUrl(n.Url), n => CleanUrl(n.RedirectTargetUrl!), StringComparer.OrdinalIgnoreCase);

        foreach (var (src, dest) in redirectMap)
        {
            if (redirectMap.TryGetValue(dest, out var secondHop))
            {
                redirectChains.Add($"{src} -> {dest} -> {secondHop}");
            }
        }

        var dupTitlesJson = JsonSerializer.Serialize(duplicateTitles);
        var redChainsJson = JsonSerializer.Serialize(redirectChains);

        var report = SeoMultipageGraphReport.Create(
            tenantId,
            rootDomain,
            inboundMap.Count,
            orphanUrls,
            dupTitlesJson,
            redChainsJson,
            auditReportId);

        report.DuplicateTitleCount = duplicateTitles.Count;
        report.RedirectChainCount = redirectChains.Count;

        _db.SeoMultipageGraphReports.Add(report);
        await _db.SaveChangesAsync();

        return new MultipageAnalysisResultDto(
            report.Id,
            rootDomain,
            inboundMap.Count,
            orphanUrls.Count,
            orphanUrls,
            duplicateTitles.Count,
            duplicateTitles,
            redirectChains.Count,
            redirectChains,
            report.EvaluatedAtUtc);
    }

    public async Task<SeoMultipageGraphReport?> GetLatestMultipageReportAsync(Guid tenantId, string rootDomain)
    {
        return await _db.SeoMultipageGraphReports
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.RootDomain == rootDomain.Trim())
            .OrderByDescending(r => r.EvaluatedAtUtc)
            .FirstOrDefaultAsync();
    }

    private static string CleanUrl(string url)
    {
        var trimmed = url.Trim();
        if (trimmed.EndsWith('/') && trimmed.Length > 8)
        {
            return trimmed.TrimEnd('/');
        }
        return trimmed;
    }
}
