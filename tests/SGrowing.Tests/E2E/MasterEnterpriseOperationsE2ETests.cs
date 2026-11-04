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

        // 1. Setup Regional Warehouse & Inventory
        db.Warehouses.Add(new Warehouse { Id = sfaxWhId, TenantId = tenantId, Name = "Sfax Regional Hub", IsPrimary = false });

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

        // 2. Setup Consumable Burn-Rate Config for Replenishment
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

        // 3. Live Customer comments 'ACHETER' during stream
        var commentResult = await liveService.ProcessLiveCommentAsync(
            tenantId,
            liveSession.Id,
            commenterUsername: "faten_sfaxienne",
            commentText: "ACHETER 1 svp",
            customerPhone: "+21623445566",
            governorate: "Sfax");

        Assert.True(commentResult.Success);
        Assert.NotNull(commentResult.OrderId);

        // 4. Treasury Float Valuation during In-Transit & Delivery
        var order = await db.CodOrders.FindAsync(commentResult.OrderId.Value);
        order!.Status = CodOrderStatus.Dispatched;
        await db.SaveChangesAsync();

        var treasuryService = new TreasuryFloatService(db);
        var floatValuation = await treasuryService.CalculateFloatValuationAsync(tenantId);
        Assert.Equal(1, floatValuation.InTransitParcelCount);
        Assert.Equal(92.0m, floatValuation.InTransitCashValue); // 85 flash + 7 shipping

        // 5. Order is Delivered -> Trigger Post-Purchase CSAT & Day-35 Replenishment
        order.Status = CodOrderStatus.Delivered;
        await db.SaveChangesAsync();

        var retentionService = new PostPurchaseRetentionService(db);
        var schedule = await retentionService.SchedulePostPurchaseRetentionAsync(tenantId, order.Id);

        Assert.NotNull(schedule);
        Assert.Equal("ELIXIR-ROYAL-50ML", schedule.Sku);

        // Customer rates 5/5 -> Promoter
        var survey = await db.PostPurchaseSurveys.FirstOrDefaultAsync(s => s.OrderId == order.Id);
        var rated = await retentionService.RecordSurveyResponseAsync(tenantId, survey!.Id, 5, "Produit magnifique !");
        Assert.True(rated.IsPromoter);

        // 6. Day-35 Predictive Replenishment Reminder Generated
        var targetDate = DateTime.UtcNow.AddDays(36);
        var dueReminders = await retentionService.ProcessDueReplenishmentRemindersAsync(tenantId, targetDate);
        Assert.NotEmpty(dueReminders);
    }

    [Fact]
    public async Task Master_E2E_Operations_WhenCommentDoesNotMatchFlashTrigger_DoesNotCreateOrderAndPreservesInventory()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        // Setup warehouse
        db.Warehouses.Add(new Warehouse { Id = whId, TenantId = tenantId, Name = "Tunis Central Hub", IsPrimary = true });
        db.WarehouseInventories.Add(new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = whId,
            Sku = "SERUM-VIT-C",
            PhysicalStock = 50,
            ReservedStock = 0
        });

        // Setup live stream with keyword "ACHETER"
        var liveService = new LiveCommerceOverlayService(db);
        var liveSession = LiveStreamSession.Create(tenantId, "Instagram Live Flash", LiveStreamPlatform.InstagramLive, "IG-STREAM-101");
        db.LiveStreamSessions.Add(liveSession);

        var flashDeal = new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = liveSession.Id,
            Sku = "SERUM-VIT-C",
            ProductName = "Serum Vitamin C",
            TriggerKeyword = "ACHETER",
            FlashPrice = 45.0m,
            OriginalPrice = 65.0m,
            MaxQuantity = 10,
            SoldQuantity = 0,
            IsActive = true
        };
        db.LiveFlashDeals.Add(flashDeal);
        await db.SaveChangesAsync();

        // 1. Customer leaves a comment without trigger keyword
        var commentResult = await liveService.ProcessLiveCommentAsync(
            tenantId,
            liveSession.Id,
            commenterUsername: "curious_shopper",
            commentText: "Bonjour est-ce qu'il y a d'autres couleurs disponibles ?",
            customerPhone: "+21699887766",
            governorate: "Tunis");

        Assert.False(commentResult.Success);
        Assert.Null(commentResult.OrderId);

        // Flash deal was not purchased
        var freshDeal = await db.LiveFlashDeals.FindAsync(flashDeal.Id);
        Assert.Equal(0, freshDeal!.SoldQuantity);

        // 2. Recording survey response for unknown survey throws InvalidOperationException
        var retentionService = new PostPurchaseRetentionService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            retentionService.RecordSurveyResponseAsync(tenantId, Guid.NewGuid(), 5, "Great"));
    }

    [Fact]
    public async Task Master_E2E_Operations_SoldOutFlashDeal_And_SessionMissingFailures()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var liveService = new LiveCommerceOverlayService(db);

        // 1. Missing live session throws InvalidOperationException
        var missingSessionId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            liveService.ProcessLiveCommentAsync(tenantId, missingSessionId, "user1", "ACHETER"));

        // 2. Sold out flash deal returns clean failure with sold out message
        var session = LiveStreamSession.Create(tenantId, "TikTok Live Rush", LiveStreamPlatform.TikTokLive, "TT-999");
        db.LiveStreamSessions.Add(session);

        var soldOutDeal = new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            Sku = "SOLDOUT-LIPSTICK",
            ProductName = "Matte Velvet Lipstick",
            TriggerKeyword = "ACHETER",
            FlashPrice = 25m,
            OriginalPrice = 40m,
            MaxQuantity = 5,
            SoldQuantity = 5, // Fully sold out!
            IsActive = true
        };
        db.LiveFlashDeals.Add(soldOutDeal);
        await db.SaveChangesAsync();

        var soldOutResult = await liveService.ProcessLiveCommentAsync(
            tenantId,
            session.Id,
            "late_shopper",
            "ACHETER svp",
            "+21622114477",
            "Sousse");

        Assert.False(soldOutResult.Success);
        Assert.Contains("SOLD OUT", soldOutResult.Message);
        Assert.Null(soldOutResult.OrderId);
    }

    [Fact]
    public async Task Master_E2E_Operations_Treasury14DayForecast_And_CarrierRemittanceLifecycle()
    {
        var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var treasuryService = new TreasuryFloatService(db);

        // 1. Setup Carrier Remittance Schedule
        var schedule = new CarrierRemittanceScheduleConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierName = "Aramex Maghreb",
            ContractFeePerParcel = 6.5m,
            RemittanceDayOfWeek = DayOfWeek.Thursday,
            IsActive = true
        };
        db.CarrierRemittanceScheduleConfigs.Add(schedule);

        // 2. Setup Dispatched and Delivered COD orders
        db.CodOrders.Add(CodOrder.Create(tenantId, "ORD-COD-T1", "Samir Kallel", "+21699112233", "Avenue Habib Bourguiba", "Tunis", "Tunis", 150m));
        var dispatchedOrder = CodOrder.Create(tenantId, "ORD-COD-T2", "Nadia Mansour", "+21622334455", "Route de Teniour", "Sfax", "Sfax", 220m);
        dispatchedOrder.Status = CodOrderStatus.Dispatched;
        db.CodOrders.Add(dispatchedOrder);

        var deliveredOrder = CodOrder.Create(tenantId, "ORD-COD-T3", "Yassine Dridi", "+21655443322", "Khezama", "Sousse", "Sousse", 300m);
        deliveredOrder.Status = CodOrderStatus.Delivered;
        db.CodOrders.Add(deliveredOrder);
        await db.SaveChangesAsync();

        // 3. Generate 14-Day Forecast starting on a Monday
        var monday = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        var forecast = await treasuryService.Generate14DayCashFlowForecastAsync(tenantId, monday);

        Assert.NotEmpty(forecast);
        Assert.All(forecast, f =>
        {
            Assert.Equal("Aramex Maghreb", f.CourierName);
            Assert.True(f.ExpectedGrossCash > 0);
            Assert.True(f.ExpectedNetRemittance > 0);
            Assert.NotNull(f.Confidence);
        });
    }
}
