using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Channels;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Channels;

public class LiveCommerceOverlayTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"LiveCommerceTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task LiveCommerce_Should_Parse_Comment_Keyword_Reserve_Stock_And_Generate_Dm_Card()
    {
        var db = CreateDbContext();
        var service = new LiveCommerceOverlayService(db);
        var tenantId = Guid.NewGuid();

        // 1. Setup Live Stream Session
        var session = LiveStreamSession.Create(tenantId, "TikTok Live Flash Sales Night", LiveStreamPlatform.TikTokLive, "TT-STREAM-8899");
        db.LiveStreamSessions.Add(session);

        // 2. Setup Flash Deal (Trigger: "ACHETER", Max: 10 units, Flash Price: 65 DT)
        var deal = new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            Sku = "FLASH-SERUM-VITC",
            ProductName = "Sérum Éclat Vitamine C",
            TriggerKeyword = "ACHETER",
            FlashPrice = 65.0m,
            OriginalPrice = 95.0m,
            MaxQuantity = 10,
            SoldQuantity = 0,
            IsActive = true
        };
        db.LiveFlashDeals.Add(deal);

        // 3. Setup WMS Inventory
        db.WarehouseInventories.Add(new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = Guid.NewGuid(),
            Sku = "FLASH-SERUM-VITC",
            PhysicalStock = 15,
            ReservedStock = 0
        });

        await db.SaveChangesAsync();

        // 4. Ingest Live Viewer Comment
        var result = await service.ProcessLiveCommentAsync(
            tenantId,
            session.Id,
            commenterUsername: "mariem_fashion",
            commentText: "J'adore ! ACHETER svp",
            customerPhone: "+21698334455",
            governorate: "Nabeul");

        Assert.True(result.Success);
        Assert.NotNull(result.OrderId);
        Assert.Equal("mariem_fashion", result.CustomerUsername);
        Assert.Equal(65.0m, result.Price);
        Assert.Contains("mariem_fashion", result.CheckoutCardJson);

        // Verify Flash Deal updated
        var updatedDeal = await db.LiveFlashDeals.FindAsync(deal.Id);
        Assert.Equal(1, updatedDeal!.SoldQuantity);
        Assert.Equal(9, updatedDeal.RemainingQuantity);

        // Verify OBS overlay stock feed
        var overlayFeed = await service.GetLiveOverlayStockFeedAsync(tenantId, session.Id);
        Assert.Single(overlayFeed);
        Assert.Equal(9, overlayFeed[0].RemainingUnits);
        Assert.Equal("ACHETER", overlayFeed[0].TriggerKeyword);
    }

    [Fact]
    public async Task ProcessLiveCommentAsync_WithUnknownSession_ShouldThrowInvalidOperationException()
    {
        var db = CreateDbContext();
        var service = new LiveCommerceOverlayService(db);
        var tenantId = Guid.NewGuid();
        var unknownSessionId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ProcessLiveCommentAsync(tenantId, unknownSessionId, "viewer1", "ACHETER"));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task ProcessLiveCommentAsync_WhenNoTriggerKeywordMatches_ShouldReturnFailure()
    {
        var db = CreateDbContext();
        var service = new LiveCommerceOverlayService(db);
        var tenantId = Guid.NewGuid();

        var session = LiveStreamSession.Create(tenantId, "Instagram Live", LiveStreamPlatform.InstagramLive, "IG-STREAM-01");
        db.LiveStreamSessions.Add(session);

        var deal = new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            Sku = "SKU-PARFUM-GOLD",
            ProductName = "Gold Musk",
            TriggerKeyword = "PROMO",
            FlashPrice = 80m,
            OriginalPrice = 120m,
            MaxQuantity = 5,
            SoldQuantity = 0,
            IsActive = true
        };
        db.LiveFlashDeals.Add(deal);
        await db.SaveChangesAsync();

        var result = await service.ProcessLiveCommentAsync(
            tenantId,
            session.Id,
            commenterUsername: "user_random",
            commentText: "Hello quelle belle couleur !");

        Assert.False(result.Success);
        Assert.Null(result.OrderId);
        Assert.Contains("did not match any active flash deal", result.Message);
        Assert.Equal(1, session.TotalCommentsProcessed);
        Assert.Equal(0, session.TotalOrdersGenerated);
    }

    [Fact]
    public async Task ProcessLiveCommentAsync_WhenDealSoldOut_ShouldReturnSoldOutFailure()
    {
        var db = CreateDbContext();
        var service = new LiveCommerceOverlayService(db);
        var tenantId = Guid.NewGuid();

        var session = LiveStreamSession.Create(tenantId, "Flash Sale", LiveStreamPlatform.TikTokLive, "TT-02");
        db.LiveStreamSessions.Add(session);

        // Flash deal already at max capacity (SoldQuantity == MaxQuantity)
        var deal = new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            Sku = "SKU-LIMITED-EDITION",
            ProductName = "Limited Edition Watch",
            TriggerKeyword = "BUYNOW",
            FlashPrice = 150m,
            OriginalPrice = 300m,
            MaxQuantity = 5,
            SoldQuantity = 5,
            IsActive = true
        };
        db.LiveFlashDeals.Add(deal);
        await db.SaveChangesAsync();

        var result = await service.ProcessLiveCommentAsync(
            tenantId,
            session.Id,
            commenterUsername: "late_buyer",
            commentText: "I want this BUYNOW");

        Assert.False(result.Success);
        Assert.Null(result.OrderId);
        Assert.Contains("SOLD OUT", result.Message);
        Assert.Equal(0, deal.RemainingQuantity);
    }

    [Fact]
    public async Task ProcessLiveCommentAsync_WhenWarehouseInventoryMissing_ShouldStillGenerateOrderGracefully()
    {
        var db = CreateDbContext();
        var service = new LiveCommerceOverlayService(db);
        var tenantId = Guid.NewGuid();

        var session = LiveStreamSession.Create(tenantId, "Facebook Live", LiveStreamPlatform.FacebookLive, "FB-01");
        db.LiveStreamSessions.Add(session);

        var deal = new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            Sku = "SKU-NO-WMS",
            ProductName = "Virtual Masterclass",
            TriggerKeyword = "JOIN",
            FlashPrice = 40m,
            OriginalPrice = 80m,
            MaxQuantity = 20,
            SoldQuantity = 0,
            IsActive = true
        };
        db.LiveFlashDeals.Add(deal);
        await db.SaveChangesAsync();

        // Process comment without any warehouse inventory seeded
        var result = await service.ProcessLiveCommentAsync(
            tenantId,
            session.Id,
            commenterUsername: "direct_learner",
            commentText: "JOIN now please",
            customerPhone: "+21655123456",
            governorate: "Sousse");

        Assert.True(result.Success);
        Assert.NotNull(result.OrderId);
        Assert.Equal(1, deal.SoldQuantity);
        Assert.Equal(1, session.TotalOrdersGenerated);

        var order = await db.CodOrders.FindAsync(result.OrderId);
        Assert.NotNull(order);
        Assert.Equal(47m, order.TotalAmount); // 40 flash + 7 shipping
        Assert.Equal("Sousse", order.GovernorateOrWilaya);
    }

    [Fact]
    public async Task GetLiveOverlayStockFeedAsync_WhenRemainingUnitsLow_ShouldFlagUrgentAlert()
    {
        var db = CreateDbContext();
        var service = new LiveCommerceOverlayService(db);
        var tenantId = Guid.NewGuid();

        var session = LiveStreamSession.Create(tenantId, "Urgent Live Stream", LiveStreamPlatform.TikTokLive, "TT-03");
        db.LiveStreamSessions.Add(session);

        // Deal 1: 3 units remaining (<= 5 -> UrgentAlert should be true)
        db.LiveFlashDeals.Add(new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            Sku = "SKU-SCARCE",
            ProductName = "Scarce Item",
            TriggerKeyword = "GRAB",
            FlashPrice = 25m,
            OriginalPrice = 50m,
            MaxQuantity = 10,
            SoldQuantity = 7,
            IsActive = true
        });

        // Deal 2: 15 units remaining (> 5 -> UrgentAlert should be false)
        db.LiveFlashDeals.Add(new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            Sku = "SKU-PLENTIFUL",
            ProductName = "Plentiful Item",
            TriggerKeyword = "WANT",
            FlashPrice = 30m,
            OriginalPrice = 60m,
            MaxQuantity = 20,
            SoldQuantity = 5,
            IsActive = true
        });

        await db.SaveChangesAsync();

        var feed = await service.GetLiveOverlayStockFeedAsync(tenantId, session.Id);

        Assert.Equal(2, feed.Count);
        var scarce = feed.Single(f => f.Sku == "SKU-SCARCE");
        Assert.True(scarce.IsUrgentAlert);
        Assert.Equal(3, scarce.RemainingUnits);

        var plentiful = feed.Single(f => f.Sku == "SKU-PLENTIFUL");
        Assert.False(plentiful.IsUrgentAlert);
        Assert.Equal(15, plentiful.RemainingUnits);
    }

    [Fact]
    public async Task GetLiveOverlayStockFeedAsync_WhenSessionHasNoActiveDeals_ReturnsEmptyList()
    {
        var db = CreateDbContext();
        var service = new LiveCommerceOverlayService(db);
        var tenantId = Guid.NewGuid();

        var session = LiveStreamSession.Create(tenantId, "Empty Deals Session", LiveStreamPlatform.TikTokLive, "TT-EMPTY-01");
        db.LiveStreamSessions.Add(session);
        await db.SaveChangesAsync();

        var feed = await service.GetLiveOverlayStockFeedAsync(tenantId, session.Id);

        Assert.Empty(feed);
    }

    [Fact]
    public async Task ProcessLiveCommentAsync_MultipleOrders_DecrementsRemainingQuantityConsistently()
    {
        var db = CreateDbContext();
        var service = new LiveCommerceOverlayService(db);
        var tenantId = Guid.NewGuid();

        var session = LiveStreamSession.Create(tenantId, "Flash Sale Night", LiveStreamPlatform.TikTokLive, "TT-MULTI-01");
        db.LiveStreamSessions.Add(session);

        var deal = new LiveFlashDeal
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SessionId = session.Id,
            Sku = "SKU-DUAL-FLASH",
            ProductName = "Dual Flash Item",
            TriggerKeyword = "CMD",
            FlashPrice = 50m,
            OriginalPrice = 100m,
            MaxQuantity = 5,
            SoldQuantity = 0,
            IsActive = true
        };
        db.LiveFlashDeals.Add(deal);
        await db.SaveChangesAsync();

        var res1 = await service.ProcessLiveCommentAsync(tenantId, session.Id, "buyer1", "CMD 1");
        var res2 = await service.ProcessLiveCommentAsync(tenantId, session.Id, "buyer2", "CMD 2");

        Assert.True(res1.Success);
        Assert.True(res2.Success);
        Assert.Equal(2, deal.SoldQuantity);
        Assert.Equal(3, deal.RemainingQuantity);
        Assert.Equal(2, session.TotalOrdersGenerated);
        Assert.Equal(2, session.TotalCommentsProcessed);
    }
}
