using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Analytics;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Analytics;

public class MultiTouchAttributionTests
{
    private readonly SGrowingDbContext _db;
    private readonly MultiTouchAttributionService _service;

    public MultiTouchAttributionTests()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SGrowingDbContext(options);
        _service = new MultiTouchAttributionService(_db);
    }

    [Fact]
    public async Task Attribution_FirstLastLinearWShaped_CalculatesWeightsAccurately()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // 1. Create a deal of $10,000
        var deal = Deal.Create(tenantId, contactId, "Cloud Migration Deal", 10000m, DealStage.Won);
        await _db.Deals.AddAsync(deal);

        // 2. Add 3 touchpoints along the journey
        var tp1 = await _service.RecordTouchpointAsync(
            tenantId, contactId, "GoogleAds", "Q1-Search-Intent", TouchpointStage.FirstDiscovery, deal.Id, cost: 250m);

        var tp2 = await _service.RecordTouchpointAsync(
            tenantId, contactId, "LinkedIn", "B2B-SDR-Outreach", TouchpointStage.LeadCreated, deal.Id, cost: 150m);

        var tp3 = await _service.RecordTouchpointAsync(
            tenantId, contactId, "WhatsApp", "In-Chat-Closing", TouchpointStage.OpportunityCreated, deal.Id, cost: 50m);

        // Model 1: First-Touch (100% to GoogleAds)
        var firstTouch = await _service.CalculateAttributionAsync(tenantId, deal.Id, AttributionModelType.FirstTouch);
        Assert.Single(firstTouch);
        Assert.Equal("GoogleAds", firstTouch[0].Channel);
        Assert.Equal(10000m, firstTouch[0].AttributedRevenue);

        // Model 2: Last-Touch (100% to WhatsApp)
        var lastTouch = await _service.CalculateAttributionAsync(tenantId, deal.Id, AttributionModelType.LastTouch);
        Assert.Single(lastTouch);
        Assert.Equal("WhatsApp", lastTouch[0].Channel);
        Assert.Equal(10000m, lastTouch[0].AttributedRevenue);

        // Model 3: Linear (33.3% to each)
        var linear = await _service.CalculateAttributionAsync(tenantId, deal.Id, AttributionModelType.Linear);
        Assert.Equal(3, linear.Count);
        Assert.Equal(3333.33m, linear[0].AttributedRevenue);

        // Model 4: W-Shaped (40% first, 40% last, 20% middle)
        var wShaped = await _service.CalculateAttributionAsync(tenantId, deal.Id, AttributionModelType.WShaped);
        Assert.Equal(3, wShaped.Count);
        Assert.Equal(4000m, wShaped[0].AttributedRevenue); // First
        Assert.Equal(4000m, wShaped[1].AttributedRevenue); // Last
        Assert.Equal(2000m, wShaped[2].AttributedRevenue); // Middle
    }

    [Fact]
    public async Task ExecutiveKpis_ComputesRoasAndCacCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        // 2 Won deals: $30,000 + $20,000 = $50,000 revenue
        var deal1 = Deal.Create(tenantId, contactId, "Deal 1", 30000m, DealStage.Won);
        var deal2 = Deal.Create(tenantId, contactId, "Deal 2", 20000m, DealStage.Won);
        await _db.Deals.AddRangeAsync(deal1, deal2);

        // Spend: $5,000 across touchpoints
        await _service.RecordTouchpointAsync(tenantId, contactId, "GoogleAds", "Ad-1", TouchpointStage.FirstDiscovery, cost: 3000m);
        await _service.RecordTouchpointAsync(tenantId, contactId, "MetaAds", "Ad-2", TouchpointStage.FirstDiscovery, cost: 2000m);

        var kpis = await _service.CalculateExecutiveKpisAsync(tenantId);

        Assert.Equal(50000m, kpis.TotalRevenue);
        Assert.Equal(5000m, kpis.TotalAdSpend);
        Assert.Equal(10.0m, kpis.ReturnOnAdSpend); // 50,000 / 5,000 = 10x
        Assert.Equal(2500m, kpis.CustomerAcquisitionCost); // 5,000 / 2 = $2,500
        Assert.Equal(25000m, kpis.AverageDealSize); // 50,000 / 2 = $25,000
    }

    [Fact]
    public async Task CalculateAttributionAsync_MissingDeal_ThrowsKeyNotFoundException()
    {
        var tenantId = Guid.NewGuid();
        var missingDealId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CalculateAttributionAsync(tenantId, missingDealId));
    }

    [Fact]
    public async Task CalculateAttributionAsync_DealWithZeroTouchpoints_ReturnsEmptyList()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var deal = Deal.Create(tenantId, contactId, "Zero Touchpoint Deal", 5000m, DealStage.Won);
        await _db.Deals.AddAsync(deal);
        await _db.SaveChangesAsync();

        var result = await _service.CalculateAttributionAsync(tenantId, deal.Id, AttributionModelType.Linear);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task CalculateAttributionAsync_TimeDecay_AttributesMoreToRecentTouchpoints()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var deal = Deal.Create(tenantId, contactId, "Time Decay Deal", 10000m, DealStage.Won);
        await _db.Deals.AddAsync(deal);

        var now = DateTime.UtcNow;
        // Oldest touchpoint (28 days ago = 4 half-lives)
        var oldTp = await _service.RecordTouchpointAsync(
            tenantId, contactId, "Display", "Old-Banner", TouchpointStage.FirstDiscovery, deal.Id, cost: 100m);
        oldTp.OccurredAtUtc = now.AddDays(-28);

        // Middle touchpoint (14 days ago = 2 half-lives)
        var midTp = await _service.RecordTouchpointAsync(
            tenantId, contactId, "Search", "Mid-Search", TouchpointStage.LeadCreated, deal.Id, cost: 200m);
        midTp.OccurredAtUtc = now.AddDays(-14);

        // Recent touchpoint (today)
        var recentTp = await _service.RecordTouchpointAsync(
            tenantId, contactId, "Direct", "Closing-Call", TouchpointStage.OpportunityCreated, deal.Id, cost: 50m);
        recentTp.OccurredAtUtc = now;

        await _db.SaveChangesAsync();

        var result = await _service.CalculateAttributionAsync(tenantId, deal.Id, AttributionModelType.TimeDecay);

        Assert.Equal(3, result.Count);
        var oldItem = result.First(r => r.TouchpointId == oldTp.Id);
        var midItem = result.First(r => r.TouchpointId == midTp.Id);
        var recentItem = result.First(r => r.TouchpointId == recentTp.Id);

        Assert.True(recentItem.WeightPercent > midItem.WeightPercent);
        Assert.True(midItem.WeightPercent > oldItem.WeightPercent);
        Assert.True(recentItem.AttributedRevenue > midItem.AttributedRevenue);
        Assert.True(midItem.AttributedRevenue > oldItem.AttributedRevenue);
    }

    [Fact]
    public async Task CalculateAttributionAsync_WShaped_TwoOrFewerTouchpoints_SplitsEqually()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var deal = Deal.Create(tenantId, contactId, "Dual Touch Deal", 10000m, DealStage.Won);
        await _db.Deals.AddAsync(deal);

        await _service.RecordTouchpointAsync(
            tenantId, contactId, "Google", "Campaign-1", TouchpointStage.FirstDiscovery, deal.Id, cost: 100m);
        await _service.RecordTouchpointAsync(
            tenantId, contactId, "Meta", "Campaign-2", TouchpointStage.OpportunityCreated, deal.Id, cost: 100m);

        var result = await _service.CalculateAttributionAsync(tenantId, deal.Id, AttributionModelType.WShaped);

        Assert.Equal(2, result.Count);
        Assert.Equal(5000m, result[0].AttributedRevenue);
        Assert.Equal(5000m, result[1].AttributedRevenue);
        Assert.Equal(50m, result[0].WeightPercent);
        Assert.Equal(50m, result[1].WeightPercent);
    }

    [Fact]
    public async Task CalculateExecutiveKpisAsync_ZeroSpendAndZeroDeals_HandlesSafelyWithoutException()
    {
        var tenantId = Guid.NewGuid();

        var kpis = await _service.CalculateExecutiveKpisAsync(tenantId);

        Assert.NotNull(kpis);
        Assert.Equal(0m, kpis.TotalRevenue);
        Assert.Equal(0m, kpis.TotalAdSpend);
        Assert.Equal(0m, kpis.ReturnOnAdSpend);
        Assert.Equal(0m, kpis.CustomerAcquisitionCost);
        Assert.Equal(0m, kpis.AverageDealSize);
        Assert.Equal(0, kpis.ClosedWonDealsCount);
    }

    [Fact]
    public async Task RecordTouchpointAsync_PersistsUtmParametersAndMetadata()
    {
        var tenantId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        var tp = await _service.RecordTouchpointAsync(
            tenantId,
            contactId,
            "Affiliate",
            "Partner-Network-Fall",
            TouchpointStage.FirstDiscovery,
            cost: 75m,
            utmSource: "newsletter_partner",
            utmMedium: "email",
            utmCampaign: "fall_promo_2026"
        );

        Assert.NotNull(tp);
        var inDb = await _db.AttributionTouchpoints.FirstOrDefaultAsync(t => t.Id == tp.Id);
        Assert.NotNull(inDb);
        Assert.Equal("newsletter_partner", inDb.UtmSource);
        Assert.Equal("email", inDb.UtmMedium);
        Assert.Equal("fall_promo_2026", inDb.UtmCampaign);
        Assert.Equal(75m, inDb.Cost);
    }
}
