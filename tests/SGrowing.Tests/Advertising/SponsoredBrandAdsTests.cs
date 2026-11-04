using System.Collections.Generic;
using System.Linq;
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
    public void AuctionBrandBanner_Should_Enforce_MinFloorCpc_When_Higher_Than_Calculated_Bid()
    {
        var floorCpc = 5.00; // higher than default 3.25 * 0.95 = ~3.09
        var bid = _engine.AuctionBrandBanner("expensive enterprise keyword", minFloorCpc: floorCpc);

        Assert.Equal(floorCpc, bid.WinningCpcBidUsd);
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

        var overview = _engine.GetOverview();
        Assert.Contains(overview.ActiveCampaigns, c => c.CampaignId == camp.CampaignId);
    }

    [Fact]
    public void CreateCampaign_With_Multiple_Products_Tracks_InStock_Skus_Accurately()
    {
        var skus = new List<FeaturedProductSku>
        {
            new("SKU-PRO-01", "In Stock Item", 49.99, 50, "url1"),
            new("SKU-PRO-02", "Out of Stock Item", 79.99, 0, "url2")
        };

        var camp = _engine.CreateCampaign("Dual Stock Brand", "Top Quality Gear", BrandBannerSlotType.TopSearchHeaderBanner, 3.50, skus);

        Assert.Equal(2, camp.FeaturedSkus.Count);
        Assert.Equal(1, camp.FeaturedSkus.Count(s => s.StockUnitsAvailable > 0));
    }

    [Fact]
    public void AuctionBrandBanner_ZeroOrEmptyQuery_ExecutesSafely()
    {
        var bid = _engine.AuctionBrandBanner("", minFloorCpc: 1.00);

        Assert.NotNull(bid);
        Assert.StartsWith("AUC-BRAND-", bid.AuctionId);
        Assert.True(bid.WinningCpcBidUsd >= 1.00);
    }

    [Fact]
    public void CreateCampaign_NullOrEmptySkus_PermitsCampaignCreation()
    {
        var camp = _engine.CreateCampaign("Minimalist Brand", "Simple Aesthetics", BrandBannerSlotType.CategoryAisleHeader, 2.50, new List<FeaturedProductSku>());

        Assert.NotNull(camp);
        Assert.Empty(camp.FeaturedSkus);
        Assert.True(camp.IsActive);
    }

    [Fact]
    public void CreateCampaign_DifferentSlotTypes_StoresSpecifiedSlot()
    {
        var camp1 = _engine.CreateCampaign("Slot Brand 1", "Header Promo", BrandBannerSlotType.TopSearchHeaderBanner, 3.00, []);
        var camp2 = _engine.CreateCampaign("Slot Brand 2", "Aisle Promo", BrandBannerSlotType.CategoryAisleHeader, 2.50, []);

        Assert.Equal(BrandBannerSlotType.TopSearchHeaderBanner, camp1.SlotType);
        Assert.Equal(BrandBannerSlotType.CategoryAisleHeader, camp2.SlotType);
    }

    [Fact]
    public void GetOverview_ReflectsAuctionCountAndRecentBidOrdering()
    {
        _engine.AuctionBrandBanner("gaming monitor 4k", minFloorCpc: 2.00);
        _engine.AuctionBrandBanner("mechanical keyboard", minFloorCpc: 1.50);

        var overview = _engine.GetOverview();
        Assert.NotNull(overview);
        Assert.True(overview.RecentBids.Count >= 2);

        // Recent bids ordered descending by timestamp
        for (int i = 0; i < overview.RecentBids.Count - 1; i++)
        {
            Assert.True(overview.RecentBids[i].AuctionTimestampUtc >= overview.RecentBids[i + 1].AuctionTimestampUtc);
        }
    }

    [Fact]
    public void AuctionBrandBanner_HighFloorCpc_SetsWinningBidToFloor()
    {
        var floor = 8.50; // Much higher than max cpc
        var bid = _engine.AuctionBrandBanner("ultra high-end luxury watch", minFloorCpc: floor);

        Assert.Equal(floor, bid.WinningCpcBidUsd);
    }
}
