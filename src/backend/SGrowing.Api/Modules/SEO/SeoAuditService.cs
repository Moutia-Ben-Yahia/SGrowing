using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.SEO;

public record PageAuditInputDto(
    string PageUrl,
    int StatusCode = 200,
    string Title = "",
    string MetaDescription = "",
    string H1Tag = "",
    bool HasCanonical = true,
    bool HasOpenGraph = true,
    bool HasSchemaOrg = true,
    int LoadTimeMs = 350
);

public interface ISeoAuditService
{
    Task<SeoAuditReport> RunAuditAsync(Guid tenantId, string targetUrl, List<PageAuditInputDto>? pages = null);
    Task<SeoAuditReport?> GetLatestAuditAsync(Guid tenantId);
    Task<List<PageAuditResult>> GetAuditPagesAsync(Guid tenantId, Guid auditReportId);
    Task<SerpKeywordTracking> TrackKeywordAsync(Guid tenantId, string keyword, string targetUrl, string countryCode = "TN", int? initialRank = null, int searchVolume = 1000);
    Task<SerpKeywordTracking> UpdateKeywordRankAsync(Guid tenantId, Guid keywordId, int newRank);
    Task<List<SerpKeywordTracking>> GetTrackedKeywordsAsync(Guid tenantId);
    Task<AeoBrandMention> RecordAeoMentionAsync(Guid tenantId, string brandName, string aiEngine, string promptQuery, bool isMentioned, string sentiment, string snippet, int? citationRank = 1);
    Task<List<AeoBrandMention>> GetAeoMentionsAsync(Guid tenantId);
}

public class SeoAuditService : ISeoAuditService
{
    private readonly SGrowingDbContext _db;

    public SeoAuditService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<SeoAuditReport> RunAuditAsync(Guid tenantId, string targetUrl, List<PageAuditInputDto>? pages = null)
    {
        var report = SeoAuditReport.Create(tenantId, targetUrl);
        report.Status = SeoAuditStatus.Crawling;
        _db.SeoAuditReports.Add(report);
        await _db.SaveChangesAsync();

        var pageInputs = pages ?? new List<PageAuditInputDto>
        {
            new PageAuditInputDto(
                targetUrl.TrimEnd('/') + "/",
                200,
                "SGrowing Store - Premium COD E-Commerce North Africa",
                "Shop the finest apparel, electronics, and accessories with fast cash-on-delivery across Tunisia, Algeria, and Morocco.",
                "Elevate Your Shopping Experience",
                true,
                true,
                true,
                280
            ),
            new PageAuditInputDto(
                targetUrl.TrimEnd('/') + "/shop",
                200,
                "All Products Collection | Fast Dispatch COD",
                "Explore over 500+ premium products delivered directly to your doorstep with doorstep inspection and free exchange.",
                "Trending Catalog",
                true,
                true,
                true,
                420
            ),
            new PageAuditInputDto(
                targetUrl.TrimEnd('/') + "/checkout",
                200,
                "Checkout",
                "", // Missing meta description
                "", // Missing H1
                false, // Missing canonical
                false,
                false,
                890
            ),
            new PageAuditInputDto(
                targetUrl.TrimEnd('/') + "/returns-exchange",
                200,
                "Easy 1-Click Drop and Swap Returns Policy",
                "Exchange sizes or models seamlessly with our courier swap service.",
                "Return & Exchange Policy",
                true,
                true,
                true,
                310
            )
        };

        var pageResults = new List<PageAuditResult>();
        int missingMeta = 0;
        int missingH1 = 0;
        int slowPages = 0;
        int brokenLinks = 0;

        foreach (var page in pageInputs)
        {
            var issues = new List<string>();

            if (page.StatusCode >= 400)
            {
                issues.Add($"HttpError_{page.StatusCode}");
                brokenLinks++;
            }

            if (string.IsNullOrWhiteSpace(page.Title) || page.Title.Length < 10)
            {
                issues.Add("MissingOrShortTitle");
            }
            else if (page.Title.Length > 70)
            {
                issues.Add("TitleTooLong");
            }

            if (string.IsNullOrWhiteSpace(page.MetaDescription) || page.MetaDescription.Length < 30)
            {
                issues.Add("MissingMetaDescription");
                missingMeta++;
            }

            if (string.IsNullOrWhiteSpace(page.H1Tag))
            {
                issues.Add("MissingH1Tag");
                missingH1++;
            }

            if (!page.HasCanonical)
            {
                issues.Add("MissingCanonicalTag");
            }

            if (!page.HasOpenGraph)
            {
                issues.Add("MissingOpenGraphTags");
            }

            if (!page.HasSchemaOrg)
            {
                issues.Add("MissingSchemaOrgJsonLd");
            }

            if (page.LoadTimeMs > 600)
            {
                issues.Add("SlowPageLoadTime");
                slowPages++;
            }

            var result = PageAuditResult.Create(
                tenantId,
                report.Id,
                page.PageUrl,
                page.StatusCode,
                page.Title,
                page.MetaDescription,
                page.H1Tag,
                page.HasCanonical,
                page.HasOpenGraph,
                page.HasSchemaOrg,
                page.LoadTimeMs,
                issues
            );

            pageResults.Add(result);
        }

        _db.PageAuditResults.AddRange(pageResults);

        // Aggregate health score
        int totalScore = pageResults.Sum(p => p.HealthScore);
        report.OverallHealthScore = pageResults.Count > 0 ? (int)Math.Round((double)totalScore / pageResults.Count) : 100;
        report.ScannedPagesCount = pageResults.Count;
        report.BrokenLinksCount = brokenLinks;
        report.MissingMetaCount = missingMeta;
        report.MissingH1Count = missingH1;
        report.SlowPagesCount = slowPages;
        report.Status = SeoAuditStatus.Completed;
        report.CompletedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return report;
    }

    public async Task<SeoAuditReport?> GetLatestAuditAsync(Guid tenantId)
    {
        return await _db.SeoAuditReports
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PageAuditResult>> GetAuditPagesAsync(Guid tenantId, Guid auditReportId)
    {
        return await _db.PageAuditResults
            .Where(p => p.TenantId == tenantId && p.AuditReportId == auditReportId)
            .OrderBy(p => p.PageUrl)
            .ToListAsync();
    }

    public async Task<SerpKeywordTracking> TrackKeywordAsync(Guid tenantId, string keyword, string targetUrl, string countryCode = "TN", int? initialRank = null, int searchVolume = 1000)
    {
        var existing = await _db.SerpKeywordTrackings
            .FirstOrDefaultAsync(k => k.TenantId == tenantId && k.Keyword.ToLower() == keyword.ToLower().Trim() && k.CountryCode == countryCode.ToUpperInvariant());

        if (existing != null)
        {
            return existing;
        }

        var tracking = SerpKeywordTracking.Create(tenantId, keyword, targetUrl, countryCode, initialRank, searchVolume);
        _db.SerpKeywordTrackings.Add(tracking);
        await _db.SaveChangesAsync();
        return tracking;
    }

    public async Task<SerpKeywordTracking> UpdateKeywordRankAsync(Guid tenantId, Guid keywordId, int newRank)
    {
        var tracking = await _db.SerpKeywordTrackings
            .FirstOrDefaultAsync(k => k.TenantId == tenantId && k.Id == keywordId);

        if (tracking == null)
        {
            throw new InvalidOperationException($"Tracked keyword {keywordId} not found for tenant {tenantId}.");
        }

        tracking.PreviousRank = tracking.CurrentRank;
        tracking.CurrentRank = newRank;

        if (!tracking.BestRank.HasValue || newRank < tracking.BestRank.Value)
        {
            tracking.BestRank = newRank;
        }

        var historyList = new List<RankHistoryEntry>();
        if (!string.IsNullOrWhiteSpace(tracking.RankHistoryJson) && tracking.RankHistoryJson != "[]")
        {
            try
            {
                historyList = JsonSerializer.Deserialize<List<RankHistoryEntry>>(tracking.RankHistoryJson) ?? new();
            }
            catch { }
        }

        historyList.Add(new RankHistoryEntry(DateTime.UtcNow.ToString("yyyy-MM-dd"), newRank));
        tracking.RankHistoryJson = JsonSerializer.Serialize(historyList);
        tracking.TrackedDateUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return tracking;
    }

    public async Task<List<SerpKeywordTracking>> GetTrackedKeywordsAsync(Guid tenantId)
    {
        return await _db.SerpKeywordTrackings
            .Where(k => k.TenantId == tenantId)
            .OrderBy(k => k.CurrentRank ?? 999)
            .ToListAsync();
    }

    public async Task<AeoBrandMention> RecordAeoMentionAsync(Guid tenantId, string brandName, string aiEngine, string promptQuery, bool isMentioned, string sentiment, string snippet, int? citationRank = 1)
    {
        var mention = AeoBrandMention.Create(tenantId, brandName, aiEngine, promptQuery, isMentioned, sentiment, snippet, citationRank);
        _db.AeoBrandMentions.Add(mention);
        await _db.SaveChangesAsync();
        return mention;
    }

    public async Task<List<AeoBrandMention>> GetAeoMentionsAsync(Guid tenantId)
    {
        return await _db.AeoBrandMentions
            .Where(m => m.TenantId == tenantId)
            .OrderByDescending(m => m.CapturedAtUtc)
            .ToListAsync();
    }
}

public record RankHistoryEntry(string Date, int Rank);
