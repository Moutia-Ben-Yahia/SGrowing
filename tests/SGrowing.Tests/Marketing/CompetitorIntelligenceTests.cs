using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Marketing;
using Xunit;

namespace SGrowing.Tests.Marketing;

public class CompetitorIntelligenceTests
{
    private readonly SGrowingDbContext _db;
    private readonly CompetitorIntelligenceService _service;

    public CompetitorIntelligenceTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new CompetitorIntelligenceService(_db);
    }

    [Fact]
    public async Task IngestCompetitorAdAndTrackPrice_DetectsPriceDeficitAndSuggestsBundle()
    {
        var tenantId = Guid.NewGuid();

        // 1. Ingest Competitor Ad
        var ad = await _service.IngestCompetitorAdAsync(
            tenantId,
            "BeautyLux Brands",
            "Meta",
            "Flash Promo 50% Off Luxury Perfumes",
            "2 Bottles for 120 TND with Free Shipping",
            estimatedDailySpend: 1500m,
            threatLevel: "High"
        );

        Assert.NotNull(ad);
        Assert.Equal("BeautyLux Brands", ad.CompetitorName);
        Assert.Equal("High", ad.ThreatLevel);

        // 2. Track price disparity where competitor undercuts by > 15%
        var tracker = await _service.TrackPriceDisparityAsync(
            tenantId,
            "PERFUME-ROYAL-OUD-50ML",
            "BeautyLux Brands",
            "https://beautylux.tn/offers/oud-duo",
            competitorPrice: 60m,
            ourPrice: 120m
        );

        Assert.NotNull(tracker);
        Assert.Equal("BundleIncentive", tracker.RecommendedCounterAction);
        Assert.True(tracker.PriceDifferencePercentage < 0);

        // 3. Radar Summary
        var summary = await _service.GetRadarSummaryAsync(tenantId);
        Assert.Equal(1, summary.TotalCompetitorAdsMonitored);
        Assert.Equal(1, summary.HighThreatCompetitorsCount);
        Assert.Equal(1, summary.SkusWithPriceDeficit);
    }

    [Fact]
    public async Task TrackPriceDisparityAsync_WhenCompetitorPriceHigher_ShouldRecommendMaintainPrice()
    {
        var tenantId = Guid.NewGuid();

        var tracker = await _service.TrackPriceDisparityAsync(
            tenantId,
            "SKU-PREMIUM-ROSE",
            "AromaPlus",
            "https://aromaplus.tn/rose",
            competitorPrice: 150m,
            ourPrice: 120m
        );

        Assert.NotNull(tracker);
        Assert.Equal("MaintainPrice", tracker.RecommendedCounterAction);
        Assert.Equal(25.0m, tracker.PriceDifferencePercentage);
        Assert.True(tracker.PriceDifferencePercentage > 0);
    }

    [Fact]
    public async Task TrackPriceDisparityAsync_WhenCompetitorPriceSlightlyLower_ShouldRecommendFlashDiscount()
    {
        var tenantId = Guid.NewGuid();

        // Competitor price 110m vs our price 120m (85% cutoff is 102m; 110m is between 102m and 120m)
        var tracker = await _service.TrackPriceDisparityAsync(
            tenantId,
            "SKU-AMBER-NIGHT",
            "ScentCraft",
            "https://scentcraft.tn/amber",
            competitorPrice: 110m,
            ourPrice: 120m
        );

        Assert.NotNull(tracker);
        Assert.Equal("FlashDiscount", tracker.RecommendedCounterAction);
        Assert.Equal(-8.3m, tracker.PriceDifferencePercentage);
    }

    [Fact]
    public async Task TrackPriceDisparityAsync_WhenExistingTrackerUpdated_ShouldRecalculateActionAndPercentage()
    {
        var tenantId = Guid.NewGuid();
        var sku = "SKU-UPDATE-FLOW";
        var competitor = "RivalCo";

        // 1. Initial tracking: MaintainPrice
        var initial = await _service.TrackPriceDisparityAsync(
            tenantId,
            sku,
            competitor,
            "https://rivalco.tn/p1",
            competitorPrice: 140m,
            ourPrice: 100m
        );
        Assert.Equal("MaintainPrice", initial.RecommendedCounterAction);
        Assert.Equal(40.0m, initial.PriceDifferencePercentage);

        // 2. Updated tracking: Severe undercut -> BundleIncentive (< 85m)
        var updated = await _service.TrackPriceDisparityAsync(
            tenantId,
            sku,
            competitor,
            "https://rivalco.tn/p1",
            competitorPrice: 70m,
            ourPrice: 100m
        );

        Assert.Equal(initial.Id, updated.Id);
        Assert.Equal("BundleIncentive", updated.RecommendedCounterAction);
        Assert.Equal(-30.0m, updated.PriceDifferencePercentage);
        Assert.Equal(70m, updated.CompetitorPrice);
    }

    [Fact]
    public async Task GetRadarSummaryAsync_WithMultipleAdsAndTrackers_ShouldAggregateAccuratelyAndFilterByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Ads for tenant A
        await _service.IngestCompetitorAdAsync(tenantA, "Rival 1", "Meta", "Hook 1", "Offer 1", 500m, "High");
        await _service.IngestCompetitorAdAsync(tenantA, "Rival 2", "TikTok", "Hook 2", "Offer 2", 1200m, "Medium");
        await _service.IngestCompetitorAdAsync(tenantA, "Rival 3", "Google", "Hook 3", "Offer 3", 200m, "High");

        // Price trackers for tenant A
        await _service.TrackPriceDisparityAsync(tenantA, "SKU-A1", "Rival 1", "https://r1.com", 80m, 100m); // Deficit
        await _service.TrackPriceDisparityAsync(tenantA, "SKU-A2", "Rival 2", "https://r2.com", 150m, 100m); // No deficit

        // Foreign ad and tracker for tenant B
        await _service.IngestCompetitorAdAsync(tenantB, "Foreign Rival", "Meta", "Hook B", "Offer B", 9999m, "High");
        await _service.TrackPriceDisparityAsync(tenantB, "SKU-B1", "Foreign Rival", "https://rb.com", 20m, 100m);

        var summaryA = await _service.GetRadarSummaryAsync(tenantA);

        Assert.Equal(3, summaryA.TotalCompetitorAdsMonitored);
        Assert.Equal(2, summaryA.HighThreatCompetitorsCount);
        Assert.Equal(1, summaryA.SkusWithPriceDeficit);
        Assert.Equal(2, summaryA.PriceAlerts.Count);

        // Ads ordered by EstimatedDailySpend descending: 1200m, 500m, 200m
        Assert.Equal(1200m, summaryA.ActiveCompetitorCampaigns[0].EstimatedDailySpend);
        Assert.Equal(500m, summaryA.ActiveCompetitorCampaigns[1].EstimatedDailySpend);
        Assert.Equal(200m, summaryA.ActiveCompetitorCampaigns[2].EstimatedDailySpend);
    }

    [Fact]
    public async Task IngestCompetitorAdAsync_ShouldDefaultThreatLevelToMediumAndPersistDetails()
    {
        var tenantId = Guid.NewGuid();

        var ad = await _service.IngestCompetitorAdAsync(
            tenantId,
            "OrganicSkin Co",
            "TikTok",
            "Try our new botanical wash",
            "Buy 1 Get 1 Free",
            estimatedDailySpend: 350m
        );

        Assert.NotNull(ad);
        Assert.Equal("Medium", ad.ThreatLevel);
        Assert.Equal("TikTok", ad.Platform);
        Assert.Equal("Try our new botanical wash", ad.HookSnippet);
        Assert.Equal("Buy 1 Get 1 Free", ad.OfferDetails);
        Assert.Equal(350m, ad.EstimatedDailySpend);
        Assert.True(ad.IsActive);
    }
}
