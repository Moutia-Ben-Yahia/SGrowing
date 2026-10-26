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
}
