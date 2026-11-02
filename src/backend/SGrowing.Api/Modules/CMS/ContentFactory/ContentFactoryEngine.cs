using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.CMS.ContentFactory;

public interface IContentFactoryEngine
{
    List<ContentArticle> ListArticles();
    ContentArticle? GetArticle(string articleId);
    ContentArticle CreateOrUpdateArticle(CreateArticleRequest request);
    BrandVoiceEvaluation EvaluateBrandVoice(EvaluateVoiceRequest request);
    LocalizationJob TriggerLocalization(string articleId, string targetLocale);
    EdgeWebhookDispatchResult DispatchEdgeWebhook(string articleId, string targetUrl);
}

public class ContentFactoryEngine : IContentFactoryEngine
{
    private readonly ConcurrentDictionary<string, ContentArticle> _articles = new();
    private readonly ConcurrentBag<LocalizationJob> _localizationJobs = new();

    public ContentFactoryEngine()
    {
        SeedDefaultContent();
    }

    private void SeedDefaultContent()
    {
        var blocks = new List<ContentBlock>
        {
            new(
                BlockId: "blk_hero_1",
                Type: ContentBlockType.Hero.ToString(),
                Title: "Enterprise Autonomous AI Growth Operating System",
                Properties: new Dictionary<string, string>
                {
                    { "headline", "Scale Revenue Autonomously" },
                    { "subheadline", "Deploy hyper-personalized outreach, real-time voice SDRs, and programmatic SEO." },
                    { "ctaText", "Request Enterprise Sandbox" },
                    { "ctaLink", "/developer/sandbox" }
                },
                OrderIndex: 0
            ),
            new(
                BlockId: "blk_features_1",
                Type: ContentBlockType.FeatureMatrix.ToString(),
                Title: "Unified Core Pillars",
                Properties: new Dictionary<string, string>
                {
                    { "col1", "CDP Identity Resolution & Reverse IP Firmographics" },
                    { "col2", "Multi-Touch Revenue Attribution & PLTV Forecasting" },
                    { "col3", "Self-Hosted Air-Gapped Cryptographic Licensing" }
                },
                OrderIndex: 1
            ),
            new(
                BlockId: "blk_faq_1",
                Type: ContentBlockType.FaqAccordion.ToString(),
                Title: "Frequently Asked Questions",
                Properties: new Dictionary<string, string>
                {
                    { "q1", "Does SGrowing support on-premises deployment?" },
                    { "a1", "Yes, air-gapped Docker Compose and Kubernetes Helm deployments are fully supported." }
                },
                OrderIndex: 2
            )
        };

        var article = new ContentArticle(
            ArticleId: "art_welcome_enterprise",
            Slug: "welcome-to-sgrowing-os",
            Title: "Welcome to SGrowing Autonomous Growth OS",
            Description: "An in-depth introduction to deploying multi-tenant AI agents and high-throughput marketing automation.",
            Author: "Growth Architecture Team",
            Blocks: blocks,
            Locale: "en-US",
            Status: "Published",
            BrandVoiceScore: 94.5,
            PublishedAtUtc: DateTime.UtcNow.AddDays(-2)
        );

        _articles[article.ArticleId] = article;
    }

    public List<ContentArticle> ListArticles() =>
        _articles.Values.OrderByDescending(a => a.PublishedAtUtc).ToList();

    public ContentArticle? GetArticle(string articleId) =>
        _articles.TryGetValue(articleId, out var article) ? article : null;

    public ContentArticle CreateOrUpdateArticle(CreateArticleRequest request)
    {
        var voiceEval = EvaluateBrandVoice(new EvaluateVoiceRequest(
            Content: $"{request.Title}. {request.Description}. {string.Join(" ", request.Blocks.Select(b => b.Title))}",
            TargetTone: "Professional & Visionary"
        ));

        var id = "art_" + Guid.NewGuid().ToString("N")[..8];
        var article = new ContentArticle(
            ArticleId: id,
            Slug: request.Slug,
            Title: request.Title,
            Description: request.Description,
            Author: request.Author,
            Blocks: request.Blocks,
            Locale: request.Locale,
            Status: "Published",
            BrandVoiceScore: voiceEval.ComplianceScore,
            PublishedAtUtc: DateTime.UtcNow
        );

        _articles[id] = article;
        return article;
    }

    public BrandVoiceEvaluation EvaluateBrandVoice(EvaluateVoiceRequest request)
    {
        var text = request.Content ?? string.Empty;
        var bannedPhrases = new[] { "guaranteed riches", "hack your way", "secret trick", "miracle", "overnight success" };
        var foundBanned = bannedPhrases.Where(p => text.Contains(p, StringComparison.OrdinalIgnoreCase)).ToList();

        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var score = 95.0;

        if (foundBanned.Any()) score -= foundBanned.Count * 25.0;
        if (wordCount < 10) score -= 15.0;
        if (text.Contains('!')) score -= 2.0;

        score = Math.Max(10.0, Math.Min(100.0, score));

        var suggestions = new List<string>();
        if (foundBanned.Any())
        {
            suggestions.Add($"Remove non-compliant sales buzzwords: {string.Join(", ", foundBanned)}.");
        }
        suggestions.Add("Align tone with enterprise B2B precision and verifiable ROI metrics.");

        return new BrandVoiceEvaluation(
            ComplianceScore: Math.Round(score, 1),
            ReadingLevel: wordCount > 15 ? "Grade 11 (Executive Brief)" : "Grade 8 (Accessible Overview)",
            ToneAdherence: new List<string> { "Enterprise Precision", "Authoritative", "Empathetic" },
            BannedPhrasesFound: foundBanned,
            Suggestions: suggestions
        );
    }

    public LocalizationJob TriggerLocalization(string articleId, string targetLocale)
    {
        var article = GetArticle(articleId);
        var job = new LocalizationJob(
            JobId: "loc_" + Guid.NewGuid().ToString("N")[..8],
            SourceArticleId: articleId,
            SourceLocale: article?.Locale ?? "en-US",
            TargetLocale: targetLocale,
            Status: "Completed",
            TranslatedBlocksCount: article?.Blocks.Count ?? 0,
            CreatedAtUtc: DateTime.UtcNow
        );

        _localizationJobs.Add(job);
        return job;
    }

    public EdgeWebhookDispatchResult DispatchEdgeWebhook(string articleId, string targetUrl)
    {
        return new EdgeWebhookDispatchResult(
            TargetUrl: targetUrl,
            EventType: "cms.article.published",
            HttpStatus: 200,
            Success: true,
            DispatchedAtUtc: DateTime.UtcNow
        );
    }
}
