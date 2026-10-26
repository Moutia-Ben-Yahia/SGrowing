using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class ConversationalCartEngineTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CartEngineTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public void ParseCartIntent_Should_Extract_Quantity_Governorate_Phone_And_Product()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var engine = new ConversationalCartEngine(db, wms);

        var message = "Bonjour, je m'appelle Amine Trabelsi, je veux commander 2 bouteilles de Royal Oud pour Sousse, mon tél est +21698765432";
        var intent = engine.ParseCartIntent(message, defaultUnitPrice: 80.0m);

        Assert.Equal("Amine Trabelsi", intent.CustomerName);
        Assert.Equal("+21698765432", intent.PhoneNumber);
        Assert.Equal("Sousse", intent.Governorate);
        Assert.Equal(2, intent.Quantity);
        Assert.Equal(80.0m, intent.UnitPrice);
        Assert.Equal(7.0m, intent.EstimatedShippingFee);
        Assert.Equal(167.0m, intent.TotalAmount); // (2 * 80) + 7
        Assert.True(intent.IsCompleteForCheckout);
    }

    [Fact]
    public void ParseCartIntent_Should_Apply_Southern_Governorate_Surcharge()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var engine = new ConversationalCartEngine(db, wms);

        var message = "Je veux 1 article pour Tataouine au +21622334455";
        var intent = engine.ParseCartIntent(message, defaultUnitPrice: 50.0m);

        Assert.Equal("Tataouine", intent.Governorate);
        Assert.Equal(8.0m, intent.EstimatedShippingFee); // 8.0 TND southern surcharge
        Assert.Equal(58.0m, intent.TotalAmount); // 50 + 8
    }

    [Fact]
    public async Task CreateInChatOrder_Should_Create_CodOrder_And_Reserve_Stock()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var engine = new ConversationalCartEngine(db, wms);

        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        var intent = new InChatCartIntent
        {
            CustomerName = "Leila Ben Salem",
            PhoneNumber = "+21650112233",
            Governorate = "Nabeul",
            DelegationOrAddress = "Hammamet Centre",
            ProductName = "Scent of Carthage",
            VariantSku = "SCENT-CARTHAGE-50ML",
            Quantity = 2,
            UnitPrice = 75.0m,
            EstimatedShippingFee = 7.0m,
            Currency = "TND"
        };

        // Initialize inventory
        await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, intent.VariantSku, initialPhysical: 20);

        var result = await engine.CreateInChatOrderAsync(tenantId, conversationId, intent, warehouseId);

        Assert.NotNull(result.Order);
        Assert.Equal(CodOrderStatus.PendingConfirmation, result.Order.Status);
        Assert.Equal("Leila Ben Salem", result.Order.CustomerName);
        Assert.Equal(150.0m, result.Order.TotalAmount);
        Assert.Equal(7.0m, result.Order.ShippingFee);

        // Verify Defensive WMS reserved stock
        var stockSummary = await wms.GetStockSummaryAsync(tenantId, warehouseId, intent.VariantSku);
        Assert.Equal(2, stockSummary.Reserved);
        Assert.Equal(18, stockSummary.Available); // 20 - 2

        // Verify interactive reply card
        Assert.Contains("SCENT-CARTHAGE-50ML", result.InteractiveReplyPayload);
        Assert.Contains("CONFIRM_", result.InteractiveReplyPayload);
    }
}
