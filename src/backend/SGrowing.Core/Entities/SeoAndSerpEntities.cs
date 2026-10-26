namespace SGrowing.Core.Entities;

public enum SeoAuditStatus
{
    Queued = 1,
    Crawling = 2,
    Completed = 3,
    Failed = 4
}

public class SeoAuditReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public int OverallHealthScore { get; set; } = 100; // 0 to 100
    public int ScannedPagesCount { get; set; } = 0;
    public int BrokenLinksCount { get; set; } = 0;
    public int MissingMetaCount { get; set; } = 0;
    public int MissingH1Count { get; set; } = 0;
    public int SlowPagesCount { get; set; } = 0;
    public SeoAuditStatus Status { get; set; } = SeoAuditStatus.Queued;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public static SeoAuditReport Create(Guid tenantId, string targetUrl)
    {
        return new SeoAuditReport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TargetUrl = targetUrl.Trim(),
            Status = SeoAuditStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class PageAuditResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AuditReportId { get; set; }
    public string PageUrl { get; set; } = string.Empty;
    public int StatusCode { get; set; } = 200;
    public string Title { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty;
    public string H1Tag { get; set; } = string.Empty;
    public bool HasCanonical { get; set; } = true;
    public bool HasOpenGraph { get; set; } = true;
    public bool HasSchemaOrgJsonLd { get; set; } = true;
    public int LoadTimeMs { get; set; } = 350;
    public int HealthScore { get; set; } = 100;
    public string IssuesJson { get; set; } = "[]"; // e.g. ["MissingMetaDescription", "TitleTooLong"]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static PageAuditResult Create(
        Guid tenantId,
        Guid auditReportId,
        string pageUrl,
        int statusCode = 200,
        string title = "",
        string metaDescription = "",
        string h1Tag = "",
        bool hasCanonical = true,
        bool hasOpenGraph = true,
        bool hasSchemaOrg = true,
        int loadTimeMs = 350,
        List<string>? issues = null)
    {
        var issuesList = issues ?? new List<string>();
        int score = 100 - (issuesList.Count * 15);
        if (score < 0) score = 0;

        return new PageAuditResult
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AuditReportId = auditReportId,
            PageUrl = pageUrl.Trim(),
            StatusCode = statusCode,
            Title = title.Trim(),
            MetaDescription = metaDescription.Trim(),
            H1Tag = h1Tag.Trim(),
            HasCanonical = hasCanonical,
            HasOpenGraph = hasOpenGraph,
            HasSchemaOrgJsonLd = hasSchemaOrg,
            LoadTimeMs = loadTimeMs,
            HealthScore = score,
            IssuesJson = System.Text.Json.JsonSerializer.Serialize(issuesList),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class SerpKeywordTracking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "TN"; // TN, FR, DZ, MA, SA, US
    public string SearchEngine { get; set; } = "Google";
    public int? CurrentRank { get; set; }
    public int? PreviousRank { get; set; }
    public int? BestRank { get; set; }
    public int SearchVolumeMonthly { get; set; } = 0;
    public string RankHistoryJson { get; set; } = "[]";
    public DateTime TrackedDateUtc { get; set; } = DateTime.UtcNow;

    public static SerpKeywordTracking Create(
        Guid tenantId,
        string keyword,
        string targetUrl,
        string countryCode = "TN",
        int? initialRank = null,
        int searchVolume = 1200)
    {
        var history = initialRank.HasValue 
            ? new List<object> { new { Date = DateTime.UtcNow.ToString("yyyy-MM-dd"), Rank = initialRank.Value } }
            : new List<object>();

        return new SerpKeywordTracking
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Keyword = keyword.Trim(),
            TargetUrl = targetUrl.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            SearchEngine = "Google",
            CurrentRank = initialRank,
            PreviousRank = initialRank,
            BestRank = initialRank,
            SearchVolumeMonthly = searchVolume,
            RankHistoryJson = System.Text.Json.JsonSerializer.Serialize(history),
            TrackedDateUtc = DateTime.UtcNow
        };
    }
}

public class AeoBrandMention
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string AiEngine { get; set; } = "ChatGPT"; // ChatGPT, Perplexity, Gemini, Claude
    public string PromptQuery { get; set; } = string.Empty;
    public bool IsBrandMentioned { get; set; } = true;
    public string MentionSentiment { get; set; } = "Positive"; // Positive, Neutral, Negative
    public string SnippetText { get; set; } = string.Empty;
    public int? CitationRank { get; set; } = 1;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

    public static AeoBrandMention Create(
        Guid tenantId,
        string brandName,
        string aiEngine,
        string promptQuery,
        bool isMentioned,
        string sentiment,
        string snippetText,
        int? citationRank = 1)
    {
        return new AeoBrandMention
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BrandName = brandName.Trim(),
            AiEngine = aiEngine.Trim(),
            PromptQuery = promptQuery.Trim(),
            IsBrandMentioned = isMentioned,
            MentionSentiment = sentiment,
            SnippetText = snippetText.Trim(),
            CitationRank = citationRank,
            CapturedAtUtc = DateTime.UtcNow
        };
    }
}
