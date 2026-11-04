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
}
