namespace SGrowing.Core.Entities;

public class SeoMultipageGraphReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? AuditReportId { get; set; }
    public string RootDomain { get; set; } = string.Empty;
    public int TotalPagesDiscovered { get; set; }
    public int OrphanPageCount { get; set; }
    public string OrphanPageUrlsJson { get; set; } = "[]";
    public int DuplicateTitleCount { get; set; }
    public string DuplicateTitleClustersJson { get; set; } = "[]";
    public int RedirectChainCount { get; set; }
    public string RedirectChainsJson { get; set; } = "[]";
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;

    public static SeoMultipageGraphReport Create(
        Guid tenantId,
        string rootDomain,
        int totalPages,
        List<string> orphanUrls,
        string duplicateTitlesJson,
        string redirectChainsJson,
        Guid? auditReportId = null)
    {
        return new SeoMultipageGraphReport
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AuditReportId = auditReportId,
            RootDomain = rootDomain.Trim(),
            TotalPagesDiscovered = totalPages,
            OrphanPageCount = orphanUrls.Count,
            OrphanPageUrlsJson = System.Text.Json.JsonSerializer.Serialize(orphanUrls),
            DuplicateTitleCount = 0,
            DuplicateTitleClustersJson = duplicateTitlesJson,
            RedirectChainCount = 0,
            RedirectChainsJson = redirectChainsJson,
            EvaluatedAtUtc = DateTime.UtcNow
        };
    }
}

public class SerpRankHistoryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? KeywordId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public int Position { get; set; }
    public int PreviousPosition { get; set; }
    public int DeltaPosition { get; set; } // +2 means moved up 2 ranks, -1 means dropped 1 rank
    public string DeviceType { get; set; } = "Desktop"; // "Desktop", "Mobile"
    public string SearchEngine { get; set; } = "Google";
    public string CountryCode { get; set; } = "TN";
    public string SerpFeaturesJson { get; set; } = "[]"; // e.g. ["FeaturedSnippet", "LocalPack"]
    public DateTime RecordedDateUtc { get; set; } = DateTime.UtcNow;

    public static SerpRankHistoryRecord Create(
        Guid tenantId,
        string keyword,
        string targetUrl,
        int position,
        int previousPosition,
        string deviceType = "Desktop",
        string countryCode = "TN",
        List<string>? serpFeatures = null,
        Guid? keywordId = null)
    {
        var features = serpFeatures ?? new List<string>();
        return new SerpRankHistoryRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeywordId = keywordId,
            Keyword = keyword.Trim(),
            TargetUrl = targetUrl.Trim(),
            Position = position,
            PreviousPosition = previousPosition,
            DeltaPosition = previousPosition > 0 ? previousPosition - position : 0,
            DeviceType = deviceType.Trim(),
            CountryCode = countryCode.Trim().ToUpperInvariant(),
            SerpFeaturesJson = System.Text.Json.JsonSerializer.Serialize(features),
            RecordedDateUtc = DateTime.UtcNow
        };
    }
}
