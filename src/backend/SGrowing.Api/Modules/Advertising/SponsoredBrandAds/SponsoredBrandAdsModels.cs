namespace SGrowing.Api.Modules.Advertising.SponsoredBrandAds;

public enum BrandBannerSlotType
{
    TopSearchHeaderBanner,
    CategoryAisleHeader,
    ProductDetailRecommendationStrip
}

public sealed record FeaturedProductSku(
    string SkuId,
    string ProductName,
    double RetailPriceUsd,
    int StockUnitsAvailable,
    string ProductImageUri
);

public sealed record SponsoredBrandCampaign(
    string CampaignId,
    string BrandName,
    string HeadlineText,
    BrandBannerSlotType SlotType,
    double MaxCpcBidUsd,
    IReadOnlyList<FeaturedProductSku> FeaturedSkus,
    bool IsActive
);

public sealed record DynamicBannerAuctionBid(
    string AuctionId,
    string CampaignId,
    string SearchQueryKeyword,
    double WinningCpcBidUsd,
    double EffectiveRoasMultiplier,
    int InStockSkusCount,
    DateTime AuctionTimestampUtc
);

public sealed record SponsoredBrandOverview(
    int ActiveBrandCampaignsCount,
    int TotalBannerAuctionsToday,
    double AverageWinningCpcUsd,
    double BlendedRoasPercentage,
    IReadOnlyList<SponsoredBrandCampaign> ActiveCampaigns,
    IReadOnlyList<DynamicBannerAuctionBid> RecentBids
);
