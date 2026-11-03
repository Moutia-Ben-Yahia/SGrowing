using SGrowing.Api.Modules.Advertising.SponsoredBrandAds;
using Xunit;

namespace SGrowing.Tests.Advertising;

public sealed class SponsoredBrandAdsTests
{
    private readonly SponsoredBrandAdsEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededCampaignsAndBids()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveBrandCampaignsCount >= 1);
        Assert.True(overview.BlendedRoasPercentage > 300.0);
        Assert.NotEmpty(overview.ActiveCampaigns);
        Assert.NotEmpty(overview.RecentBids);
    }

    [Fact]
    public void AuctionBrandBanner_HighIntentKeyword_ClearsWithStockAwareness()
    {
        var bid = _engine.AuctionBrandBanner("premium audiophile headphones", minFloorCpc: 2.50);

        Assert.NotNull(bid);
        Assert.StartsWith("AUC-BRAND-", bid.AuctionId);
        Assert.True(bid.WinningCpcBidUsd >= 2.50);
        Assert.True(bid.InStockSkusCount >= 1);
        Assert.True(bid.EffectiveRoasMultiplier > 4.0);
    }

    [Fact]
    public void CreateCampaign_ValidSkus_StoresNewCampaign()
    {
        var skus = new List<FeaturedProductSku>
        {
            new("SKU-TEST-01", "Smart Sensor Hub", 89.99, 400, "https://cdn.sgrowing.io/products/sensor-hub.png")
        };

        var camp = _engine.CreateCampaign("IoT Innovators Corp", "Automate Your Factory Floor", BrandBannerSlotType.CategoryAisleHeader, 4.00, skus);

        Assert.NotNull(camp);
        Assert.StartsWith("CAMP-BRAND-", camp.CampaignId);
        Assert.Equal("IoT Innovators Corp", camp.BrandName);
        Assert.Single(camp.FeaturedSkus);
    }
}
