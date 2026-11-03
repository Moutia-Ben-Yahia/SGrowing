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
    public void GenerateFeed_CreatesCompliantFeed_ForTargetChannel()
    {
        var request = new GenerateFeedRequest(FeedChannel.PinterestCommerce, FeedFormat.CsvTsv);
        var feed = _engine.GenerateFeed(request);

        feed.Should().NotBeNull();
        feed.Channel.Should().Be(FeedChannel.PinterestCommerce);
        feed.Format.Should().Be(FeedFormat.CsvTsv);
        feed.IsCompliant.Should().BeTrue();
        feed.TotalItems.Should().Be(3);
        feed.FeedEndpointUrl.Should().Contain("pinterestcommerce.csv");
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
}
