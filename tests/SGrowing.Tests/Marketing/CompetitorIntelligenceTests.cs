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
}
