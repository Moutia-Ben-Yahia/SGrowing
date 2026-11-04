using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Advertising.CatalogFeeds;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class CatalogFeedsTests
{
    private readonly CatalogFeedsEngine _engine = new();

    [Fact]
    public void GetFeeds_ReturnsConfiguredSyndicationFeeds()
    {
        var feeds = _engine.GetFeeds();

        feeds.Should().NotBeNull();
        feeds.Should().HaveCount(3);
        feeds.Select(f => f.Channel).Should().Contain(new[]
        {
            FeedChannel.GoogleMerchantCenter,
            FeedChannel.MetaAdvantageCatalog,
            FeedChannel.TikTokShop
        });
    }

    [Fact]
    public void GetProducts_Should_Return_Products_Sorted_By_ProductId()
    {
        var products = _engine.GetProducts();

        products.Should().NotBeNull();
        products.Should().HaveCount(3);
        products.Should().BeInAscendingOrder(p => p.ProductId);
    }

    [Theory]
    [InlineData(FeedChannel.GoogleMerchantCenter, FeedFormat.XmlRss2, "xml")]
    [InlineData(FeedChannel.TikTokShop, FeedFormat.JsonLd, "json")]
    [InlineData(FeedChannel.MetaAdvantageCatalog, FeedFormat.CsvTsv, "csv")]
    public void GenerateFeed_CreatesCompliantFeed_With_Correct_Extension(FeedChannel channel, FeedFormat format, string expectedExt)
    {
        var request = new GenerateFeedRequest(channel, format);
        var feed = _engine.GenerateFeed(request);

        feed.Should().NotBeNull();
        feed.Channel.Should().Be(channel);
        feed.Format.Should().Be(format);
        feed.IsCompliant.Should().BeTrue();
        feed.TotalItems.Should().Be(3);
        feed.FeedEndpointUrl.Should().EndWith($".{expectedExt}");
    }

    [Fact]
    public void ApplyDynamicBadge_AppendsBadgeToProductItem()
    {
        var productId = "prod_saas_ent_01";
        var request = new ApplyDynamicBadgeRequest(productId, "Top Rated 2026");

        var updated = _engine.ApplyDynamicBadge(request);

        updated.Should().NotBeNull();
        updated.ProductId.Should().Be(productId);
        updated.Badges.Should().Contain("Top Rated 2026");
    }

    [Fact]
    public void ApplyDynamicBadge_Should_Be_Idempotent_Without_Duplicate_Badges()
    {
        var productId = "prod_voice_copilot_02";
        var request = new ApplyDynamicBadgeRequest(productId, "Instant Provisioning");

        var updated = _engine.ApplyDynamicBadge(request);

        // Badge was already seeded on this product, count should not double
        updated.Badges.Count(b => b.Equals("Instant Provisioning", System.StringComparison.OrdinalIgnoreCase)).Should().Be(1);
    }

    [Fact]
    public void ApplyDynamicBadge_Should_Throw_When_Product_Not_Found()
    {
        var request = new ApplyDynamicBadgeRequest("non_existent_product_99", "New Badge");

        var action = () => _engine.ApplyDynamicBadge(request);

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found*");
    }
}
