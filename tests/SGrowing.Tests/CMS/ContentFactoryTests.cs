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
}
