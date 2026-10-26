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
}
