using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Advertising.SponsoredBrandAds;

public interface ISponsoredBrandAdsEngine
{
    SponsoredBrandOverview GetOverview();
    SponsoredBrandCampaign CreateCampaign(string brandName, string headline, BrandBannerSlotType slotType, double maxCpc, List<FeaturedProductSku> skus);
    DynamicBannerAuctionBid AuctionBrandBanner(string searchQuery, double minFloorCpc);
}

public sealed class SponsoredBrandAdsEngine : ISponsoredBrandAdsEngine
{
    private readonly ConcurrentDictionary<string, SponsoredBrandCampaign> _campaigns = new();
    private readonly ConcurrentDictionary<string, DynamicBannerAuctionBid> _bids = new();

    public SponsoredBrandAdsEngine()
    {
        SeedCampaigns();
        SeedInitialBids();
    }

    private void SeedCampaigns()
    {
        var skus1 = new List<FeaturedProductSku>
        {
            new("SKU-AUDIO-01", "AuraSound Studio Pro Wireless Headphones", 349.99, 120, "https://cdn.sgrowing.io/products/aurasound-pro.png"),
            new("SKU-AUDIO-02", "AuraSound ANC Earbuds 3", 199.99, 450, "https://cdn.sgrowing.io/products/aurasound-buds.png"),
            new("SKU-AUDIO-03", "AuraSound HiFi Bluetooth Amp", 149.99, 85, "https://cdn.sgrowing.io/products/aurasound-amp.png")
        };

        var c1 = new SponsoredBrandCampaign(
            "CAMP-BRAND-001",
            "AuraSound Acoustic Labs",
            "Experience Pure Studio Master Audio Anywhere",
            BrandBannerSlotType.TopSearchHeaderBanner,
            3.25,
            skus1,
            true
        );
        _campaigns.TryAdd(c1.CampaignId, c1);
    }

    private void SeedInitialBids()
    {
        var b1 = new DynamicBannerAuctionBid(
            "AUC-BRAND-001",
            "CAMP-BRAND-001",
            "wireless noise cancelling headphones",
            WinningCpcBidUsd: 3.10,
            EffectiveRoasMultiplier: 4.85,
            InStockSkusCount: 3,
            DateTime.UtcNow.AddMinutes(-12)
        );
        _bids.TryAdd(b1.AuctionId, b1);
    }

    public SponsoredBrandOverview GetOverview()
    {
        var active = _campaigns.Values.Where(c => c.IsActive).ToList();
        var bids = _bids.Values.OrderByDescending(b => b.AuctionTimestampUtc).Take(10).ToList();

        return new SponsoredBrandOverview(
            ActiveBrandCampaignsCount: active.Count,
            TotalBannerAuctionsToday: 8420,
            AverageWinningCpcUsd: 2.85,
            BlendedRoasPercentage: 420.5,
            ActiveCampaigns: active,
            RecentBids: bids
        );
    }

    public SponsoredBrandCampaign CreateCampaign(string brandName, string headline, BrandBannerSlotType slotType, double maxCpc, List<FeaturedProductSku> skus)
    {
        var campaign = new SponsoredBrandCampaign(
            $"CAMP-BRAND-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            brandName,
            headline,
            slotType,
            maxCpc,
            skus,
            true
        );
        _campaigns.TryAdd(campaign.CampaignId, campaign);
        return campaign;
    }

    public DynamicBannerAuctionBid AuctionBrandBanner(string searchQuery, double minFloorCpc)
    {
        var activeCampaign = _campaigns.Values.FirstOrDefault(c => c.IsActive)
            ?? CreateCampaign("Default Premium Brand", "Discover Exceptional Quality", BrandBannerSlotType.TopSearchHeaderBanner, 3.0, []);

        int inStockCount = activeCampaign.FeaturedSkus.Count(s => s.StockUnitsAvailable > 0);
        double winningCpc = Math.Max(minFloorCpc, activeCampaign.MaxCpcBidUsd * 0.95);

        var bid = new DynamicBannerAuctionBid(
            $"AUC-BRAND-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            activeCampaign.CampaignId,
            searchQuery,
            Math.Round(winningCpc, 2),
            EffectiveRoasMultiplier: 4.60,
            InStockSkusCount: inStockCount,
            DateTime.UtcNow
        );

        _bids.TryAdd(bid.AuctionId, bid);
        return bid;
    }
}
