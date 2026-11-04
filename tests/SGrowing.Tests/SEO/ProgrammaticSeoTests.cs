using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.SEO.Programmatic;
using Xunit;

namespace SGrowing.Tests.SEO;

public class ProgrammaticSeoTests
{
    private readonly ProgrammaticSeoEngine _engine = new();

    [Fact]
    public void ListPages_ReturnsDefaultSeededLandingPages()
    {
        var pages = _engine.ListPages();

        pages.Should().NotBeEmpty();
        pages.Should().Contain(p => p.Industry == "FinTech");
        pages.Should().Contain(p => p.Industry == "E-Commerce");
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

        generated.Should().HaveCount(3);
        generated.Should().AllSatisfy(p =>
        {
            p.Slug.Should().StartWith("/growth-platform-for-");
            p.SchemaJsonLd.Should().Contain("schema.org");
            p.WordCount.Should().BeGreaterThan(1000);
            p.InternalLinks.Should().NotBeEmpty();
        });
    }

    [Fact]
    public void GeneratePages_AppendsCreatedPagesToListPages()
    {
        // Arrange
        var initialCount = _engine.ListPages().Count;
        var req = new BatchGeneratePagesRequest(
            BaseSlugPattern: "solutions/{industry}",
            Industries: new List<string> { "EdTech", "InsurTech" },
            TargetAudience: "Startups"
        );

        // Act
        var generated = _engine.GeneratePages(req);
        var updatedPages = _engine.ListPages();

        // Assert
        generated.Should().HaveCount(2);
        updatedPages.Count.Should().Be(initialCount + 2);
        updatedPages.Should().Contain(p => p.Industry == "EdTech");
        updatedPages.Should().Contain(p => p.Industry == "InsurTech");
    }

    [Fact]
    public void GeneratePages_NormalizesSlugPrefixWithLeadingSlash()
    {
        // Arrange: slug pattern without leading slash
        var req = new BatchGeneratePagesRequest(
            BaseSlugPattern: "no-leading-slash/{industry}",
            Industries: new List<string> { "GovTech" },
            TargetAudience: "Public Sector"
        );

        // Act
        var generated = _engine.GeneratePages(req);

        // Assert
        generated[0].Slug.Should().StartWith("/no-leading-slash/govtech");
    }

    [Fact]
    public void ListBacklinkOpportunities_RanksByDomainAuthority()
    {
        var backlinks = _engine.ListBacklinkOpportunities();

        backlinks.Should().NotBeEmpty();
        backlinks[0].DomainAuthority.Should().BeGreaterThanOrEqualTo(backlinks[1].DomainAuthority);
        backlinks.Should().Contain(b => b.TargetDomain == "techcrunch.com");
    }

    [Fact]
    public void ListBacklinkOpportunities_OrdersStrictlyDescendingByDomainAuthority()
    {
        // Act
        var backlinks = _engine.ListBacklinkOpportunities();

        // Assert
        backlinks.Should().BeInDescendingOrder(b => b.DomainAuthority);
    }

    [Fact]
    public void SubmitToIndexNow_ReturnsAcceptedReceipt()
    {
        var urls = new List<string> { "https://sgrowing.io/solutions/crm-for-fintech" };
        var receipt = _engine.SubmitToIndexNow(urls);

        receipt.Should().NotBeNull();
        receipt.UrlsSubmitted.Should().Be(1);
        receipt.ResponseCode.Should().Be("202 Accepted");
        receipt.Engine.Should().Contain("IndexNow");
    }

    [Fact]
    public void SubmitToIndexNow_HandlesEmptyUrlList_ReturnsZeroSubmitted()
    {
        // Arrange & Act
        var receipt = _engine.SubmitToIndexNow(new List<string>());

        // Assert
        receipt.UrlsSubmitted.Should().Be(0);
        receipt.ResponseCode.Should().Be("202 Accepted");
    }
}
