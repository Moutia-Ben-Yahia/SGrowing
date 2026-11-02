namespace SGrowing.Api.Modules.SEO.GeoVisibility;

public enum AiAnswerEngine
{
    Perplexity,
    ChatGptSearch,
    GoogleAiOverviews,
    ClaudeSearch,
    Copilot
}

public record GeoCitation(
    string CitationId,
    AiAnswerEngine Engine,
    string Query,
    string BrandMentioned,
    string CitedUrl,
    int CitationRank, // 1st citation, 2nd, etc.
    string ExtractedPassage,
    string Sentiment, // "Positive", "Neutral", "Negative"
    DateTime DetectedAtUtc
);

public record GeoVisibilitySummary(
    string TenantId,
    string BrandName,
    int OverallGeoScore, // 0 - 100
    double ShareOfModelVoicePercent, // e.g. 42.5%
    int TotalCitationsTracked,
    Dictionary<string, int> EngineBreakdown,
    List<string> StrategicRecommendations
);

public record AuditQueryRequest(
    string TenantId,
    string BrandName,
    string TargetQuery,
    List<AiAnswerEngine> Engines
);
