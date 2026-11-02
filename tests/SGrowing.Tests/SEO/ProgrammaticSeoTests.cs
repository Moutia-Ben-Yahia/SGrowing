using SGrowing.Api.Modules.SEO.Programmatic;

namespace SGrowing.Tests.SEO;

public class ProgrammaticSeoTests
{
    private readonly ProgrammaticSeoEngine _engine = new();

    [Fact]
    public void ListPages_ReturnsDefaultSeededLandingPages()
    {
        var pages = _engine.ListPages();

        Assert.NotEmpty(pages);
        Assert.Contains(pages, p => p.Industry == "FinTech");
        Assert.Contains(pages, p => p.Industry == "E-Commerce");
    }

    [Fact]
    public void GeneratePages_CreatesTargetedPagesWithStructuredSchema()
    {
        var req = new BatchGeneratePagesRequest(
            BaseSlugPattern: "growth-platform-for-{industry}",
            Industries: new List<string> { "Healthcare", "Logistics", "LegalTech" },
            TargetAudience: "B2B SaaS Leaders"
        );

        var generated = _engine.GeneratePages(req);

        Assert.Equal(3, generated.Count);
        Assert.All(generated, p =>
        {
            Assert.StartsWith("/growth-platform-for-", p.Slug);
            Assert.Contains("schema.org", p.SchemaJsonLd);
            Assert.True(p.WordCount > 1000);
            Assert.NotEmpty(p.InternalLinks);
        });
    }

    [Fact]
    public void ListBacklinkOpportunities_RanksByDomainAuthority()
    {
        var backlinks = _engine.ListBacklinkOpportunities();

        Assert.NotEmpty(backlinks);
        Assert.True(backlinks[0].DomainAuthority >= backlinks[1].DomainAuthority);
        Assert.Contains(backlinks, b => b.TargetDomain == "techcrunch.com");
    }

    [Fact]
    public void SubmitToIndexNow_ReturnsAcceptedReceipt()
    {
        var urls = new List<string> { "https://sgrowing.io/solutions/crm-for-fintech" };
        var receipt = _engine.SubmitToIndexNow(urls);

        Assert.NotNull(receipt);
        Assert.Equal(1, receipt.UrlsSubmitted);
        Assert.Equal("202 Accepted", receipt.ResponseCode);
    }
}
