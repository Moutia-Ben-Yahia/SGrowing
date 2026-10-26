using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Finance;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterEnterpriseOperationsE2ETests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MasterEnterpriseOpsE2EDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_E2E_Enterprise_Operations_And_Retention_Lifecycle()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var sfaxWhId = Guid.NewGuid();
        var yalidineId = Guid.NewGuid();

        // 1. Setup Regional Warehouse (Sfax Coastal Hub) & Proximity Rule
        db.Warehouses.Add(new Warehouse { Id = sfaxWhId, TenantId = tenantId, Name = "Sfax Regional Hub", IsPrimary = false });
        db.RegionalHubProximityRules.Add(RegionalHubProximityRule.Create(tenantId, sfaxWhId, "Sfax Regional Hub", "Sfax", priority: 1));

        // Setup Inventory in Sfax Hub
        db.WarehouseInventories.Add(new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = sfaxWhId,
            Sku = "ELIXIR-ROYAL-50ML",
            PhysicalStock = 30,
            ReservedStock = 0
        });

        // 2. Setup Smart Courier Rate Cards & Performance Metrics in Sfax
        db.CarrierRateCards.Add(new CarrierRateCard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = yalidineId,
            CourierName = "Yalidine Express",
            BaseFee = 7.0m,
            IsActive = true
        });

        db.CarrierPerformanceMetrics.Add(CarrierPerformanceMetric.Create(
            tenantId, yalidineId, "Yalidine Express", "Sfax", dispatched: 150, delivered: 140, returned: 10, returnFee: 3.5m));

        // 3. Setup Consumable Burn-Rate Config for Replenishment
        db.ProductBurnRateConfigs.Add(new ProductBurnRateConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Sku = "ELIXIR-ROYAL-50ML",
            ProductName = "Elixir Royal 50ml",
            EstimatedDaysOfUse = 45,
            ReminderLeadDays = 10,
            IncentiveDiscountPercent = 15.0m,
            IsActive = true
        });

        // Setup Live Stream Session & Flash Deal
        var liveService = new LiveCommerceOverlayService(db);
        var liveSession = LiveStreamSession.Create(tenantId, "TikTok Live Flash Sales", LiveStreamPlatform.TikTokLive, "TT-SESSION-99");
        db.LiveStreamSessions.Add(liveSession);

        var flashDeal = new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = liveSession.Id,
            Sku = "ELIXIR-ROYAL-50ML",
            ProductName = "Elixir Royal 50ml",
            TriggerKeyword = "ACHETER",
            FlashPrice = 85.0m,
            OriginalPrice = 110.0m,
            MaxQuantity = 20,
            SoldQuantity = 0,
            IsActive = true
        };
        db.LiveFlashDeals.Add(flashDeal);
        await db.SaveChangesAsync();

        // 4. Live Customer comments 'ACHETER' during stream
        var commentResult = await liveService.ProcessLiveCommentAsync(
            tenantId,
            liveSession.Id,
            commenterUsername: "faten_sfaxienne",
            commentText: "ACHETER 1 svp",
            customerPhone: "+21623445566",
            governorate: "Sfax");

        Assert.True(commentResult.Success);
        Assert.NotNull(commentResult.OrderId);

        // 5. Multi-Warehouse Proximity Allocation
        var whService = new MultiWarehouseService(db);
        var allocation = await whService.AllocateOrderToOptimalWarehouseAsync(tenantId, "Sfax", "ELIXIR-ROYAL-50ML", 1);
        Assert.Equal(sfaxWhId, allocation.WarehouseId);

        // 6. Smart Courier Dispatch Routing
        var routingService = new SmartCourierRoutingService(db);
        var optimalCarrier = await routingService.AutoAssignOptimalCarrierAsync(tenantId, commentResult.OrderId.Value, 1.0m);
        Assert.Equal("Yalidine Express", optimalCarrier.CourierName);
        Assert.True(optimalCarrier.HistoricalDeliveryRate >= 90m);

        // 7. Treasury Float Valuation during In-Transit & Delivery
        var order = await db.CodOrders.FindAsync(commentResult.OrderId.Value);
        order!.Status = CodOrderStatus.Dispatched;
        await db.SaveChangesAsync();

        var treasuryService = new TreasuryFloatService(db);
        var floatValuation = await treasuryService.CalculateFloatValuationAsync(tenantId);
        Assert.Equal(1, floatValuation.InTransitParcelCount);
        Assert.Equal(92.0m, floatValuation.InTransitCashValue); // 85 flash + 7 shipping

        // 8. Order is Delivered -> Trigger Post-Purchase CSAT & Day-35 Replenishment
        order.Status = CodOrderStatus.Delivered;
        await db.SaveChangesAsync();

        var retentionService = new PostPurchaseRetentionService(db);
        var schedule = await retentionService.SchedulePostPurchaseRetentionAsync(tenantId, order.Id);

        Assert.NotNull(schedule);
        Assert.Equal("ELIXIR-ROYAL-50ML", schedule.Sku);

        // Customer rates 5/5 -> Promoted to VIP with discount
        var survey = await db.PostPurchaseSurveys.FirstOrDefaultAsync(s => s.OrderId == order.Id);
        var rated = await retentionService.RecordSurveyResponseAsync(tenantId, survey!.Id, 5, "Produit magnifique !");
        Assert.True(rated.IsPromoter);
        Assert.NotNull(rated.DiscountCodeIssued);

        // Day 35 Replenishment trigger
        var reminders = await retentionService.ProcessDueReplenishmentRemindersAsync(tenantId, schedule.ScheduledReminderUtc.AddHours(1));
        Assert.Single(reminders);
        Assert.Equal(order.PhoneNumber, reminders[0].CustomerPhone);
        Assert.Contains("touche bientôt à sa fin", reminders[0].MessageText);
    }
}
