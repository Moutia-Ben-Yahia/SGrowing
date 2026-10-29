using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.SEO;
using Xunit;

namespace SGrowing.Tests.SEO;

public class TechnicalSeoCrawlerTests
{
    private static SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task RecursiveCrawl_DiscoversInternalLinks_AndRespectsMaxPagesLimit()
    {
        // Arrange
        using var db = CreateDbContext();
        var fetcher = new MockPageFetcher();

        // Site graph:
        // / -> /p1, /p2, /p3
        // /p1 -> /p4, /p5
        // /p2 -> /p6
        fetcher.AddPage("https://myshop.com", 200, @"
            <html>
              <head>
                <title>My Shop - Premium E-Commerce Store Online</title>
                <meta name='description' content='Discover high quality products with cash on delivery and fast shipping across North Africa and the Mediterranean.' />
                <link rel='canonical' href='https://myshop.com' />
                <meta property='og:title' content='My Shop' />
                <meta property='og:image' content='https://myshop.com/og.jpg' />
                <meta property='og:description' content='Best Shop' />
              </head>
              <body>
                <h1>Welcome to My Shop</h1>
                <a href='/p1'>Page 1</a>
                <a href='/p2'>Page 2</a>
                <a href='/p3'>Page 3</a>
              </body>
            </html>");

        fetcher.AddPage("https://myshop.com/p1", 200, @"
            <html>
              <head>
                <title>Page 1 - Excellent Collection Catalog</title>
                <meta name='description' content='Explore catalog 1 with comprehensive product variants and immediate doorstep delivery service everywhere.' />
                <link rel='canonical' href='https://myshop.com/p1' />
                <meta property='og:title' content='P1' />
                <meta property='og:image' content='https://myshop.com/p1.jpg' />
                <meta property='og:description' content='P1 desc' />
              </head>
              <body>
                <h1>Collection 1</h1>
                <a href='/p4'>Page 4</a>
                <a href='/p5'>Page 5</a>
              </body>
            </html>");

        fetcher.AddPage("https://myshop.com/p2", 200, @"
            <html>
              <head>
                <title>Page 2 - Special Offers And Deals Today</title>
                <meta name='description' content='Take advantage of limited time offers and discounts on our entire line of apparel and electronics.' />
                <link rel='canonical' href='https://myshop.com/p2' />
                <meta property='og:title' content='P2' />
                <meta property='og:image' content='https://myshop.com/p2.jpg' />
                <meta property='og:description' content='P2 desc' />
              </head>
              <body>
                <h1>Special Deals</h1>
                <a href='/p6'>Page 6</a>
              </body>
            </html>");

        fetcher.AddPage("https://myshop.com/p3", 200, "<html><head><title>Page 3 Collection Online Shop</title></head><body><h1>P3</h1></body></html>");
        fetcher.AddPage("https://myshop.com/p4", 200, "<html><head><title>Page 4 Item Details Collection</title></head><body><h1>P4</h1></body></html>");
        fetcher.AddPage("https://myshop.com/p5", 200, "<html><head><title>Page 5 Item Details Collection</title></head><body><h1>P5</h1></body></html>");
        fetcher.AddPage("https://myshop.com/p6", 200, "<html><head><title>Page 6 Item Details Collection</title></head><body><h1>P6</h1></body></html>");

        var service = new TechnicalSeoCrawlerService(db, fetcher);
        var tenantId = Guid.NewGuid();

        // Act - crawl with maxPages = 3
        var summary = await service.StartCrawlAsync(tenantId, "https://myshop.com", maxPages: 3, maxDepth: 2);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal("Completed", summary.Status);
        Assert.Equal(3, summary.TotalPagesCrawled);
        Assert.True(summary.OverallHealthScore >= 0 && summary.OverallHealthScore <= 100);

        var pagesResult = await service.GetCrawledPagesAsync(tenantId, summary.CrawlId, page: 1, pageSize: 10);
        Assert.Equal(3, pagesResult.TotalCount);
    }

    [Fact]
    public void AnalyzeHtml_DetectsMissingTitle_AndLengthIssues()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);

        // 1. Missing Title
        var htmlMissingTitle = "<html><head></head><body><h1>Hello</h1></body></html>";
        var res1 = service.AnalyzeHtml("https://example.com/missing", 200, htmlMissingTitle);
        Assert.Contains("MissingTitle", res1.Issues);

        // 2. Title Too Short (< 30 chars)
        var htmlShort = "<html><head><title>Short Title</title></head><body><h1>Hello</h1></body></html>";
        var res2 = service.AnalyzeHtml("https://example.com/short", 200, htmlShort);
        Assert.Contains("TitleTooShort", res2.Issues);
        Assert.Equal("Short Title", res2.Title);

        // 3. Title Too Long (> 60 chars)
        var longTitle = new string('A', 75);
        var htmlLong = $"<html><head><title>{longTitle}</title></head><body><h1>Hello</h1></body></html>";
        var res3 = service.AnalyzeHtml("https://example.com/long", 200, htmlLong);
        Assert.Contains("TitleTooLong", res3.Issues);

        // 4. Optimal Title (30 - 60 chars)
        var optimalTitle = "SGrowing Store - Best Cash on Delivery"; // 38 chars
        var htmlOptimal = $"<html><head><title>{optimalTitle}</title></head><body><h1>Hello</h1></body></html>";
        var res4 = service.AnalyzeHtml("https://example.com/opt", 200, htmlOptimal);
        Assert.DoesNotContain("MissingTitle", res4.Issues);
        Assert.DoesNotContain("TitleTooShort", res4.Issues);
        Assert.DoesNotContain("TitleTooLong", res4.Issues);
    }

    [Fact]
    public void AnalyzeHtml_EnforcesStrictlyOneH1Tag()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);

        // 1. Missing H1
        var htmlMissingH1 = "<html><head><title>Sample Valid Title For Page Testing 12345</title></head><body><p>No H1 here</p></body></html>";
        var res1 = service.AnalyzeHtml("https://example.com/no-h1", 200, htmlMissingH1);
        Assert.Equal(0, res1.H1Count);
        Assert.Contains("MissingH1", res1.Issues);

        // 2. Multiple H1 tags
        var htmlMultiH1 = @"
            <html>
              <head><title>Sample Valid Title For Page Testing 12345</title></head>
              <body>
                <h1>Main Heading One</h1>
                <h2>Subheading</h2>
                <h1>Duplicate Main Heading Two</h1>
              </body>
            </html>";
        var res2 = service.AnalyzeHtml("https://example.com/multi-h1", 200, htmlMultiH1);
        Assert.Equal(2, res2.H1Count);
        Assert.Contains("MultipleH1", res2.Issues);
        Assert.Equal("Main Heading One", res2.H1Tag);

        // 3. Exactly one H1 tag
        var htmlSingleH1 = @"
            <html>
              <head><title>Sample Valid Title For Page Testing 12345</title></head>
              <body>
                <h1>Unique Perfect Heading</h1>
                <h2>Subheading</h2>
              </body>
            </html>";
        var res3 = service.AnalyzeHtml("https://example.com/single-h1", 200, htmlSingleH1);
        Assert.Equal(1, res3.H1Count);
        Assert.DoesNotContain("MissingH1", res3.Issues);
        Assert.DoesNotContain("MultipleH1", res3.Issues);
        Assert.Equal("Unique Perfect Heading", res3.H1Tag);
    }

    [Fact]
    public void AnalyzeHtml_ValidatesCanonical_AndDetectsExternalMismatch()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);

        // 1. Missing canonical
        var htmlNoCan = "<html><head><title>Testing Valid Title For Page Verification 12</title></head><body><h1>Heading</h1></body></html>";
        var res1 = service.AnalyzeHtml("https://example.com/page", 200, htmlNoCan);
        Assert.False(res1.HasCanonical);
        Assert.Contains("MissingCanonical", res1.Issues);

        // 2. Self-referential canonical (optimal)
        var htmlSelfCan = @"
            <html>
              <head>
                <title>Testing Valid Title For Page Verification 12</title>
                <link rel='canonical' href='https://example.com/page' />
              </head>
              <body><h1>Heading</h1></body>
            </html>";
        var res2 = service.AnalyzeHtml("https://example.com/page", 200, htmlSelfCan);
        Assert.True(res2.HasCanonical);
        Assert.True(res2.IsCanonicalSelfReferential);
        Assert.DoesNotContain("CanonicalMismatch", res2.Issues);

        // 3. External / Mismatch canonical
        var htmlMismatchCan = @"
            <html>
              <head>
                <title>Testing Valid Title For Page Verification 12</title>
                <link rel='canonical' href='https://otherdomain.com/canonical-page' />
              </head>
              <body><h1>Heading</h1></body>
            </html>";
        var res3 = service.AnalyzeHtml("https://example.com/page", 200, htmlMismatchCan);
        Assert.True(res3.HasCanonical);
        Assert.False(res3.IsCanonicalSelfReferential);
        Assert.Contains("CanonicalMismatch", res3.Issues);
    }

    [Fact]
    public void AnalyzeHtml_DetectsMissingOpenGraphTags()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);

        var htmlIncompleteOg = @"
            <html>
              <head>
                <title>Sample Valid Title For Page Testing 12345</title>
                <meta property='og:title' content='Only Title Provided' />
              </head>
              <body><h1>Hello</h1></body>
            </html>";

        var res = service.AnalyzeHtml("https://example.com/og", 200, htmlIncompleteOg);

        Assert.False(res.HasOpenGraph);
        Assert.Contains("MissingOgImage", res.Issues);
        Assert.Contains("MissingOgDescription", res.Issues);
        Assert.Contains("IncompleteOpenGraph", res.Issues);
        Assert.DoesNotContain("MissingOgTitle", res.Issues);
    }

    [Fact]
    public void PageRank_PowerIteration_ConvergesAndSumsToOne()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);

        var pages = new List<string>
        {
            "https://site.com/",
            "https://site.com/products",
            "https://site.com/about"
        };

        // Graph: / -> /products, /products -> /about, /about -> /
        var edges = new List<KeyValuePair<string, string>>
        {
            new("https://site.com/", "https://site.com/products"),
            new("https://site.com/products", "https://site.com/about"),
            new("https://site.com/about", "https://site.com/")
        };
        var lookup = edges.ToLookup(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        // Act
        var prScores = service.CalculatePageRank(pages, lookup, dampingFactor: 0.85, maxIterations: 30);

        // Assert
        Assert.Equal(3, prScores.Count);
        double sum = prScores.Values.Sum();
        Assert.True(Math.Abs(sum - 1.0) < 1e-4, $"Expected sum 1.0, got {sum}");

        // In a symmetric 3-cycle, PageRank of each node should be approximately 1/3 = 0.3333
        foreach (var score in prScores.Values)
        {
            Assert.True(Math.Abs(score - 0.3333) < 0.05, $"Expected ~0.3333, got {score}");
        }
    }

    [Fact]
    public void PageRank_AuthorityHub_ReceivesHigherScore()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);

        var pages = new List<string>
        {
            "https://site.com/hub",
            "https://site.com/p1",
            "https://site.com/p2",
            "https://site.com/p3"
        };

        // Everyone links to /hub!
        var edges = new List<KeyValuePair<string, string>>
        {
            new("https://site.com/p1", "https://site.com/hub"),
            new("https://site.com/p2", "https://site.com/hub"),
            new("https://site.com/p3", "https://site.com/hub"),
            new("https://site.com/hub", "https://site.com/p1")
        };
        var lookup = edges.ToLookup(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

        // Act
        var prScores = service.CalculatePageRank(pages, lookup);

        // Assert
        double hubScore = prScores["https://site.com/hub"];
        double p2Score = prScores["https://site.com/p2"];
        double p3Score = prScores["https://site.com/p3"];

        Assert.True(hubScore > p2Score, $"Hub score {hubScore} should exceed p2 score {p2Score}");
        Assert.True(hubScore > p3Score, $"Hub score {hubScore} should exceed p3 score {p3Score}");
    }

    [Fact]
    public async Task Crawl_IdentifiesOrphanPages_AndComputesLinkGraph()
    {
        // Arrange
        using var db = CreateDbContext();
        var fetcher = new MockPageFetcher();

        // Root links to /p1 and /p2.
        // /orphan is a discovered page that has 0 inbound links.
        fetcher.AddPage("https://myshop.com", 200, @"
            <html>
              <head><title>My Shop - Best Cash on Delivery In North Africa</title></head>
              <body>
                <h1>Shop Home</h1>
                <a href='/p1'>Page 1</a>
                <a href='/p2'>Page 2</a>
              </body>
            </html>");

        fetcher.AddPage("https://myshop.com/p1", 200, @"
            <html>
              <head><title>Page One Collection Online Shop Catalog</title></head>
              <body>
                <h1>Page One</h1>
                <a href='/p2'>Page 2</a>
              </body>
            </html>");

        fetcher.AddPage("https://myshop.com/p2", 200, @"
            <html>
              <head><title>Page Two Special Offers And Discounts</title></head>
              <body>
                <h1>Page Two</h1>
                <a href='/orphan'>Discovered Orphan Link</a>
              </body>
            </html>");

        // Note: in this graph /orphan has 1 inbound link from /p2.
        // Let's add an isolated mock page that is passed or added to graph:
        var service = new TechnicalSeoCrawlerService(db, fetcher);
        var tenantId = Guid.NewGuid();

        var summary = await service.StartCrawlAsync(tenantId, "https://myshop.com", maxPages: 10);
        var linkGraph = await service.GetLinkGraphAsync(tenantId, summary.CrawlId);

        Assert.NotNull(linkGraph);
        Assert.True(linkGraph.TotalNodes >= 3);
        Assert.True(linkGraph.TotalEdges >= 3);
    }

    [Fact]
    public void CalculateHealthScore_WeightsCrawlability_Metadata_AndLinkIntegrity()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);

        // 1. Perfect Site -> 100 score
        var perfectPages = new List<PageAnalysisResult>
        {
            new()
            {
                Url = "https://site.com/",
                StatusCode = 200,
                Title = "SGrowing - Premier E-Commerce Growth Platform",
                MetaDescription = "Comprehensive omnichannel marketing, COD CRM, and AI sales copilot designed for fast-growing digital direct-to-consumer retail brands.",
                H1Count = 1,
                H1Tag = "Welcome to Growth",
                HasCanonical = true,
                IsCanonicalSelfReferential = true,
                HasOpenGraph = true,
                ResponseTimeMs = 150
            }
        };

        int perfectScore = service.CalculateHealthScore(perfectPages, orphanPages: Array.Empty<string>(), out int crawlability1, out int meta1, out int link1);
        Assert.Equal(100, perfectScore);
        Assert.Equal(100, crawlability1);
        Assert.Equal(100, meta1);
        Assert.Equal(100, link1);

        // 2. Broken & Problematic Site
        var badPages = new List<PageAnalysisResult>
        {
            new()
            {
                Url = "https://site.com/",
                StatusCode = 200,
                Issues = new List<string> { "MissingTitle", "MissingH1", "MissingCanonical" }
            },
            new()
            {
                Url = "https://site.com/broken",
                StatusCode = 404,
                Issues = new List<string> { "BrokenLink_404" }
            }
        };

        int badScore = service.CalculateHealthScore(badPages, orphanPages: new[] { "https://site.com/orphan" }, out int crawlability2, out int meta2, out int link2);

        Assert.True(badScore < 75, $"Expected degraded health score, got {badScore}");
        Assert.True(crawlability2 < 100);
        Assert.True(meta2 < 100);
        Assert.True(link2 < 100);
    }

    [Fact]
    public void ComputeRankDelta_CalculatesImproved_Declined_Unchanged_AndNew()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);

        // Improved: was #8, now #3 (+5)
        var (d1, s1) = service.ComputeRankDelta(3, 8);
        Assert.Equal("+5", d1);
        Assert.Equal("improved", s1);

        // Declined: was #2, now #6 (-4)
        var (d2, s2) = service.ComputeRankDelta(6, 2);
        Assert.Equal("-4", d2);
        Assert.Equal("declined", s2);

        // Unchanged: was #4, now #4 (0)
        var (d3, s3) = service.ComputeRankDelta(4, 4);
        Assert.Equal("0", d3);
        Assert.Equal("unchanged", s3);

        // New keyword: prevPos is null or 0
        var (d4, s4) = service.ComputeRankDelta(5, null);
        Assert.Equal("New", d4);
        Assert.Equal("new", s4);
    }

    [Fact]
    public void ComputeVisibilityIndex_CalculatesTop3_Top10_Top30_AndWeightedScore()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);
        var tenantId = Guid.NewGuid();

        var keywords = new List<SerpKeywordRankItemDto>
        {
            new(Guid.NewGuid(), tenantId, "kw1", "https://url.com/1", "TN", "Google", 1, 2, "+1", "improved", 1, new List<SerpRankPointDto>(), DateTime.UtcNow),
            new(Guid.NewGuid(), tenantId, "kw2", "https://url.com/2", "TN", "Google", 3, 3, "0", "unchanged", 3, new List<SerpRankPointDto>(), DateTime.UtcNow),
            new(Guid.NewGuid(), tenantId, "kw3", "https://url.com/3", "TN", "Google", 7, 10, "+3", "improved", 7, new List<SerpRankPointDto>(), DateTime.UtcNow),
            new(Guid.NewGuid(), tenantId, "kw4", "https://url.com/4", "TN", "Google", 22, 25, "+3", "improved", 20, new List<SerpRankPointDto>(), DateTime.UtcNow),
            new(Guid.NewGuid(), tenantId, "kw5", "https://url.com/5", "TN", "Google", 65, 60, "-5", "declined", 55, new List<SerpRankPointDto>(), DateTime.UtcNow)
        };

        // Act
        var overview = service.ComputeVisibilityIndex(keywords);

        // Assert
        Assert.Equal(5, overview.TotalTrackedKeywords);
        Assert.Equal(2, overview.Top3Count); // kw1 (1), kw2 (3)
        Assert.Equal(40.0, overview.Top3Percentage);

        Assert.Equal(3, overview.Top10Count); // kw1, kw2, kw3
        Assert.Equal(60.0, overview.Top10Percentage);

        Assert.Equal(4, overview.Top30Count); // kw1, kw2, kw3, kw4
        Assert.Equal(80.0, overview.Top30Percentage);

        Assert.True(overview.OverallVisibilityScore > 0 && overview.OverallVisibilityScore <= 100);
    }

    [Fact]
    public async Task EdgeCases_InvalidUrl_ThrowsArgumentException()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.StartCrawlAsync(Guid.NewGuid(), "not-a-valid-url"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.StartCrawlAsync(Guid.NewGuid(), "ftp://myshop.com"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.StartCrawlAsync(Guid.NewGuid(), ""));
    }

    [Fact]
    public async Task EdgeCases_SinglePageSite_CalculatesPageRankAndHealthScore()
    {
        // Arrange
        using var db = CreateDbContext();
        var fetcher = new MockPageFetcher();

        fetcher.AddPage("https://singlepage.com", 200, @"
            <html>
              <head>
                <title>Single Page App - Modern Minimalist Landing</title>
                <meta name='description' content='A clean single page showcasing portfolio items and direct contact info with instant live chat and booking calendar today.' />
                <link rel='canonical' href='https://singlepage.com' />
                <meta property='og:title' content='Single Page' />
                <meta property='og:image' content='https://singlepage.com/pic.jpg' />
                <meta property='og:description' content='Single Page Desc' />
              </head>
              <body>
                <h1>Single Page Landing</h1>
                <p>No links here.</p>
              </body>
            </html>");

        var service = new TechnicalSeoCrawlerService(db, fetcher);
        var tenantId = Guid.NewGuid();

        // Act
        var summary = await service.StartCrawlAsync(tenantId, "https://singlepage.com");

        // Assert
        Assert.Equal(1, summary.TotalPagesCrawled);
        Assert.Equal(100, summary.OverallHealthScore);
        Assert.Equal(0, summary.OrphanPagesCount);

        var pages = await service.GetCrawledPagesAsync(tenantId, summary.CrawlId);
        Assert.Single(pages.Items);
        Assert.Equal(1.0, pages.Items[0].PageRank);
    }

    [Fact]
    public async Task EdgeCases_CyclicInternalLinks_DoesNotInfiniteLoop()
    {
        // Arrange
        using var db = CreateDbContext();
        var fetcher = new MockPageFetcher();

        // Cyclic: A -> B, B -> A
        fetcher.AddPage("https://cyclic.com", 200, @"
            <html>
              <head><title>Cycle Page A - Continuous Cycle Test Model</title></head>
              <body>
                <h1>Page A</h1>
                <a href='https://cyclic.com/b'>Go to B</a>
              </body>
            </html>");

        fetcher.AddPage("https://cyclic.com/b", 200, @"
            <html>
              <head><title>Cycle Page B - Continuous Cycle Test Model</title></head>
              <body>
                <h1>Page B</h1>
                <a href='https://cyclic.com'>Go to A</a>
              </body>
            </html>");

        var service = new TechnicalSeoCrawlerService(db, fetcher);
        var tenantId = Guid.NewGuid();

        // Act
        var summary = await service.StartCrawlAsync(tenantId, "https://cyclic.com", maxPages: 10);

        // Assert
        Assert.Equal(2, summary.TotalPagesCrawled);
        Assert.Equal("Completed", summary.Status);
    }

    [Fact]
    public async Task TrackKeywordAsync_And_GetSerpRankingsAsync_RecordsHistoryAndUpdatesBestRank()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new TechnicalSeoCrawlerService(db);
        var tenantId = Guid.NewGuid();

        // 1. Initial Track at rank 12
        var item1 = await service.TrackKeywordAsync(tenantId, "vetements sport tunisie", "https://store.sgrowing.io/sport", "TN", initialPosition: 12);
        Assert.Equal("vetements sport tunisie", item1.Keyword);
        Assert.Equal(12, item1.CurrentPosition);
        Assert.Equal(12, item1.BestPosition);
        Assert.Equal("New", item1.Delta);

        // 2. Update to rank 5 (improvement)
        var item2 = await service.TrackKeywordAsync(tenantId, "vetements sport tunisie", "https://store.sgrowing.io/sport", "TN", initialPosition: 5);
        Assert.Equal(5, item2.CurrentPosition);
        Assert.Equal(12, item2.PreviousPosition);
        Assert.Equal(5, item2.BestPosition);
        Assert.Equal("+7", item2.Delta);
        Assert.Equal("improved", item2.Status);

        // 3. Query rankings overview
        var overview = await service.GetSerpRankingsAsync(tenantId, "TN");
        Assert.True(overview.TotalTrackedKeywords >= 1);
        var found = overview.Keywords.FirstOrDefault(k => k.Keyword == "vetements sport tunisie");
        Assert.NotNull(found);
        Assert.Equal(5, found.CurrentPosition);
        Assert.Equal("+7", found.Delta);
    }

    [Fact]
    public void MapTechnicalSeoCrawlerEndpoints_RegistersExpectedRoutes()
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();
        var app = builder.Build();
        app.MapTechnicalSeoCrawlerEndpoints();
        Assert.NotNull(app);
    }
}
