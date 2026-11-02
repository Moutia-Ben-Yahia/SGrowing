namespace SGrowing.Api.Modules.CMS.ContentFactory;

public enum ContentBlockType
{
    Hero,
    FeatureMatrix,
    TestimonialGrid,
    FaqAccordion,
    PricingCalculator
}

public record ContentBlock(
    string BlockId,
    string Type,
    string Title,
    Dictionary<string, string> Properties,
    int OrderIndex
);

public record ContentArticle(
    string ArticleId,
    string Slug,
    string Title,
    string Description,
    string Author,
    List<ContentBlock> Blocks,
    string Locale,
    string Status,
    double BrandVoiceScore,
    DateTime PublishedAtUtc
);

public record BrandVoiceEvaluation(
    double ComplianceScore,
    string ReadingLevel,
    List<string> ToneAdherence,
    List<string> BannedPhrasesFound,
    List<string> Suggestions
);

public record CreateArticleRequest(
    string Slug,
    string Title,
    string Description,
    string Author,
    List<ContentBlock> Blocks,
    string Locale
);

public record EvaluateVoiceRequest(
    string Content,
    string TargetTone
);

public record LocalizationJob(
    string JobId,
    string SourceArticleId,
    string SourceLocale,
    string TargetLocale,
    string Status,
    int TranslatedBlocksCount,
    DateTime CreatedAtUtc
);

public record EdgeWebhookDispatchResult(
    string TargetUrl,
    string EventType,
    int HttpStatus,
    bool Success,
    DateTime DispatchedAtUtc
);
