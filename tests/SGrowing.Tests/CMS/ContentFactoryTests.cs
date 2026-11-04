using SGrowing.Api.Modules.CMS.ContentFactory;

namespace SGrowing.Tests.CMS;

public class ContentFactoryTests
{
    private readonly IContentFactoryEngine _engine = new ContentFactoryEngine();

    [Fact]
    public void ListArticles_ReturnsSeededContent()
    {
        var articles = _engine.ListArticles();

        Assert.NotEmpty(articles);
        Assert.Contains(articles, a => a.Blocks.Count >= 2);
        Assert.Equal("en-US", articles[0].Locale);
    }

    [Fact]
    public void CreateArticle_ValidatesBlocksAndScoresBrandVoice()
    {
        var blocks = new List<ContentBlock>
        {
            new("blk_test_1", "Hero", "Enterprise AI Architecture", new Dictionary<string, string> { { "k", "v" } }, 0)
        };

        var request = new CreateArticleRequest(
            Slug: "enterprise-ai-architecture",
            Title: "Enterprise AI Architecture Guide",
            Description: "Comprehensive deep-dive into autonomous multi-tenant growth operating systems.",
            Author: "Chief Architect",
            Blocks: blocks,
            Locale: "en-US"
        );

        var article = _engine.CreateOrUpdateArticle(request);

        Assert.NotNull(article);
        Assert.Equal("enterprise-ai-architecture", article.Slug);
        Assert.True(article.BrandVoiceScore >= 80);
        Assert.Single(article.Blocks);
    }

    [Fact]
    public void EvaluateBrandVoice_DetectsBannedPhrases()
    {
        var request = new EvaluateVoiceRequest(
            Content: "This secret trick will guarantee overnight success for any founder!",
            TargetTone: "Professional"
        );

        var eval = _engine.EvaluateBrandVoice(request);

        Assert.NotNull(eval);
        Assert.True(eval.ComplianceScore < 70);
        Assert.Contains("secret trick", eval.BannedPhrasesFound);
        Assert.NotEmpty(eval.Suggestions);
    }

    [Fact]
    public void TriggerLocalization_CreatesCompletedJob()
    {
        var job = _engine.TriggerLocalization("art_welcome_enterprise", "de-DE");

        Assert.NotNull(job);
        Assert.Equal("de-DE", job.TargetLocale);
        Assert.Equal("Completed", job.Status);
    }

    [Fact]
    public void GetArticle_ExistingAndNonExisting_ReturnsArticleOrNull()
    {
        var existing = _engine.GetArticle("art_welcome_enterprise");
        Assert.NotNull(existing);
        Assert.Equal("welcome-to-sgrowing-os", existing.Slug);

        var missing = _engine.GetArticle("art_ghost_nonexistent");
        Assert.Null(missing);
    }

    [Fact]
    public void DispatchEdgeWebhook_ReturnsSuccessDispatchResult()
    {
        var result = _engine.DispatchEdgeWebhook("art_welcome_enterprise", "https://edge.sgrowing.io/api/v1/cache-purge");

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(200, result.HttpStatus);
        Assert.Equal("cms.article.published", result.EventType);
        Assert.Equal("https://edge.sgrowing.io/api/v1/cache-purge", result.TargetUrl);
    }

    [Fact]
    public void EvaluateBrandVoice_CleanExecutiveText_ReceivesHighScore()
    {
        var request = new EvaluateVoiceRequest(
            Content: "Autonomous revenue acceleration architecture with deterministic SLA guarantees and SOC2 compliance.",
            TargetTone: "Professional"
        );

        var eval = _engine.EvaluateBrandVoice(request);

        Assert.NotNull(eval);
        Assert.True(eval.ComplianceScore >= 90);
        Assert.Empty(eval.BannedPhrasesFound);
    }

    [Fact]
    public void TriggerLocalization_MissingArticle_UsesFallbackLocaleAndZeroBlocks()
    {
        var job = _engine.TriggerLocalization("art_missing_article_id", "fr-FR");

        Assert.NotNull(job);
        Assert.Equal("en-US", job.SourceLocale);
        Assert.Equal("fr-FR", job.TargetLocale);
        Assert.Equal(0, job.TranslatedBlocksCount);
    }

    [Fact]
    public void EvaluateBrandVoice_ShortTextUnderTenWords_DeductsFifteenPoints()
    {
        var request = new EvaluateVoiceRequest(
            Content: "Brief revenue update note.", // 4 words
            TargetTone: "Professional"
        );

        var eval = _engine.EvaluateBrandVoice(request);

        Assert.NotNull(eval);
        // Base 95 - 15 (short text) = 80.0
        Assert.Equal(80.0, eval.ComplianceScore);
        Assert.Equal("Grade 8 (Accessible Overview)", eval.ReadingLevel);
    }

    [Fact]
    public void EvaluateBrandVoice_ExclamationPoint_DeductsTwoPoints()
    {
        var request = new EvaluateVoiceRequest(
            Content: "Autonomous revenue acceleration architecture with deterministic SLA guarantees and SOC2 compliance!",
            TargetTone: "Professional"
        );

        var eval = _engine.EvaluateBrandVoice(request);

        Assert.NotNull(eval);
        // Base 95 - 2 (exclamation point) = 93.0
        Assert.Equal(93.0, eval.ComplianceScore);
    }

    [Fact]
    public void EvaluateBrandVoice_ReadingLevel_ReturnsGrade11ForLongerContent()
    {
        var request = new EvaluateVoiceRequest(
            Content: "Our enterprise platform provides reverse IP firmographics, predictive lead enrichment, autonomous multi-turn voice SDR capabilities, and real-time webhook mesh verification.",
            TargetTone: "Executive"
        );

        var eval = _engine.EvaluateBrandVoice(request);

        Assert.NotNull(eval);
        Assert.Equal("Grade 11 (Executive Brief)", eval.ReadingLevel);
        Assert.Contains("Enterprise Precision", eval.ToneAdherence);
    }

    [Fact]
    public void EvaluateBrandVoice_MultipleBannedPhrases_CumulativelyDeductsScore()
    {
        var request = new EvaluateVoiceRequest(
            Content: "Experience guaranteed riches and our miracle growth hack your way method for overnight success today in modern business!",
            TargetTone: "Casual"
        );

        var eval = _engine.EvaluateBrandVoice(request);

        Assert.NotNull(eval);
        Assert.True(eval.BannedPhrasesFound.Count >= 3);
        Assert.True(eval.ComplianceScore <= 20.0);
        Assert.NotEmpty(eval.Suggestions);
    }

    [Fact]
    public void CreateArticle_PersistsToArticleDictionary_AllowsSubsequentRetrievalByGeneratedId()
    {
        var request = new CreateArticleRequest(
            Slug: "autonomous-growth-playbook",
            Title: "Autonomous Growth Playbook 2026",
            Description: "Field guide to building deterministic revenue operations using agent swarms.",
            Author: "Operations Guild",
            Blocks: new List<ContentBlock>
            {
                new("blk_intro", "Text", "Introduction", new Dictionary<string, string> { { "body", "Welcome." } }, 0)
            },
            Locale: "en-US"
        );

        var created = _engine.CreateOrUpdateArticle(request);
        Assert.NotNull(created);
        Assert.StartsWith("art_", created.ArticleId);

        var retrieved = _engine.GetArticle(created.ArticleId);
        Assert.NotNull(retrieved);
        Assert.Equal("autonomous-growth-playbook", retrieved.Slug);
        Assert.Equal("Operations Guild", retrieved.Author);
        Assert.Equal("Published", retrieved.Status);
    }

    [Fact]
    public void ListArticles_OrderedByPublishedAtUtcDescending()
    {
        var articles = _engine.ListArticles();

        Assert.NotEmpty(articles);
        for (var i = 0; i < articles.Count - 1; i++)
        {
            Assert.True(articles[i].PublishedAtUtc >= articles[i + 1].PublishedAtUtc);
        }
    }

    [Fact]
    public void EvaluateBrandVoice_NullContent_HandledGracefullyWithoutException()
    {
        var eval = _engine.EvaluateBrandVoice(new EvaluateVoiceRequest(Content: null!, TargetTone: "Neutral"));

        Assert.NotNull(eval);
        Assert.Equal(80.0, eval.ComplianceScore); // 95 base - 15 (0 words < 10)
        Assert.Empty(eval.BannedPhrasesFound);
    }

    [Fact]
    public void EvaluateBrandVoice_ScoreFlooredAtTenPoints()
    {
        var request = new EvaluateVoiceRequest(
            Content: "Guaranteed riches and miracle hack your way secret trick for overnight success!",
            TargetTone: "Hyperbole"
        );

        var eval = _engine.EvaluateBrandVoice(request);

        Assert.NotNull(eval);
        Assert.Equal(10.0, eval.ComplianceScore); // Clamped at 10.0 minimum floor
        Assert.Equal(5, eval.BannedPhrasesFound.Count);
    }

    [Fact]
    public void TriggerLocalization_ExistingArticleWithMultipleBlocks_ReflectsBlockCount()
    {
        var job = _engine.TriggerLocalization("art_welcome_enterprise", "es-ES");

        Assert.NotNull(job);
        Assert.Equal("en-US", job.SourceLocale);
        Assert.Equal("es-ES", job.TargetLocale);
        Assert.Equal(3, job.TranslatedBlocksCount);
        Assert.Equal("Completed", job.Status);
    }

    [Fact]
    public void DispatchEdgeWebhook_CreatesCompletedDispatchWithIsoTimestamp()
    {
        var now = DateTime.UtcNow;
        var result = _engine.DispatchEdgeWebhook("art_any", "https://cdn.sgrowing.io/hooks/purge");

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(200, result.HttpStatus);
        Assert.Equal("cms.article.published", result.EventType);
        Assert.True(result.DispatchedAtUtc >= now.AddSeconds(-2));
    }
}
