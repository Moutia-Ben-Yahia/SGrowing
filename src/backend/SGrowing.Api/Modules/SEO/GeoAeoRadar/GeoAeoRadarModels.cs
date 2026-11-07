using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.SEO.GeoAeoRadar;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiEngineType
{
    Perplexity,
    ChatGPT_Search,
    Google_Gemini_AIO,
    Claude_Web
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CitabilityGrade
{
    High,
    Moderate,
    NeedsImprovement
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrawlerReadinessStatus
{
    Compliant,
    Warning,
    Blocked
}

public record GeoCitationQueryRecord(
    Guid Id,
    Guid TenantId,
    string Query,
    AiEngineType Engine,
    string CitedUrl,
    int CitationRank,
    string ExtractedPassage,
    string Sentiment,
    double ShareOfVoice,
    DateTime DetectedAtUtc
);

public record PageCitabilityAudit(
    Guid Id,
    Guid TenantId,
    string Url,
    string Title,
    double OverallScore,
    CitabilityGrade Grade,
    double PassageConcisenessScore,
    double EntityDensityScore,
    double StructuredFormatRatio,
    int FactualClaimsCount,
    int AmbiguousPronounOccurrences,
    IReadOnlyList<string> Recommendations,
    DateTime AuditedAtUtc
);

public record AgentCrawlerRule(
    string BotName,
    string UserAgent,
    CrawlerReadinessStatus Status,
    bool AllowedInRobotsTxt,
    string DiscoveredPath,
    string Details
);

public record AgenticCrawlerManifest(
    Guid TenantId,
    string Domain,
    CrawlerReadinessStatus OverallRobotsStatus,
    CrawlerReadinessStatus LlmsTxtStatus,
    IReadOnlyList<AgentCrawlerRule> Crawlers,
    DateTime LastVerifiedAtUtc,
    IReadOnlyList<string> ActionItems
);

public record AddCitationQueryRequest(
    string Query,
    AiEngineType Engine = AiEngineType.Perplexity,
    string? CitedUrl = null,
    string? BrandName = "SGrowing",
    int? CitationRank = 1,
    string? ExtractedPassage = null,
    string? Sentiment = "Positive"
);

public record AuditPageCitabilityRequest(
    string Url,
    string Title,
    string Content,
    AiEngineType? TargetEngine = null
);

public record EngineVisibilityMetric(
    AiEngineType Engine,
    int TotalCitations,
    double AverageRank,
    double ShareOfVoicePercent,
    string SentimentTrend
);

public record GeoRadarOverviewReport(
    Guid TenantId,
    double OverallGeoScore,
    double ShareOfModelVoicePercent,
    int TotalQueriesTracked,
    int TotalCitationsCount,
    int HighCitabilityAuditsCount,
    IReadOnlyList<EngineVisibilityMetric> EngineBreakdown,
    DateTime GeneratedAtUtc
);
