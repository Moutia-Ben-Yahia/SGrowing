using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Api.Modules.Marketing;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.E2E;

public class MasterGrowthAndFinancesE2ETests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MasterGrowthDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task Master_EndToEnd_ConversationalCart_To_AdConversion_And_Sheets()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var cartEngine = new ConversationalCartEngine(db, wms);
        var storeService = new ExternalStoreAndSheetsService(db);
        var conversionMesh = new ServerConversionMeshService(db);

        var tenantId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "OUD-ROYAL-PREMIUM";

        // Step 0: Setup Inventory
        await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, sku, initialPhysical: 30);

        // Step 1: Customer messages on WhatsApp
        var chatMessage = "Bonjour je m'appelle Yassine Trabelsi, je veux commander 2 bouteilles de Oud Royal pour Sousse, mon num est +21698112233";
        var intent = cartEngine.ParseCartIntent(chatMessage, defaultUnitPrice: 90.0m);
        intent.VariantSku = sku;
        Assert.Equal("Sousse", intent.Governorate);
        Assert.Equal(2, intent.Quantity);
        Assert.Equal(187.0m, intent.TotalAmount); // (2 * 90) + 7 shipping

        // Step 2: Create in-chat order and reserve stock
        var chatOrderResult = await cartEngine.CreateInChatOrderAsync(tenantId, conversationId, intent, warehouseId);
        Assert.NotNull(chatOrderResult.Order);
        var stockAfterReservation = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(2, stockAfterReservation.Reserved);
        Assert.Equal(28, stockAfterReservation.Available);

        // Step 3: Stream order to Google Sheets row
        var sheetRow = await storeService.GenerateGoogleSheetsRowAsync(tenantId, chatOrderResult.Order.Id);
        Assert.Equal(chatOrderResult.Order.OrderNumber, sheetRow[1]);
        Assert.Equal("Yassine Trabelsi", sheetRow[2]);

        // Step 4: Dispatch order and push fulfillment to external store
        var syncFulfillment = await storeService.PushFulfillmentUpdateAsync(tenantId, chatOrderResult.Order.Id, "Express Courrier TN", "EXP-ORD-991");
        Assert.Equal("Success", syncFulfillment.Status);

        // Step 5: Order delivered -> Dispatch Meta CAPI conversion event with SHA-256 hashing
        var conversion = await conversionMesh.TrackConversionAsync(
            tenantId,
            AdPlatform.MetaCapi,
            ConversionEventType.Purchase,
            chatOrderResult.Order.TotalAmount,
            chatOrderResult.Order.CodCurrency,
            email: "yassine.trabelsi@gmail.com",
            phone: chatOrderResult.Order.PhoneNumber,
            firstName: "Yassine",
            lastName: "Trabelsi",
            city: "Sousse",
            country: "TN",
            fbc: "fb.1.169000.IwARtest",
            fbp: "fb.1.169000.998877",
            userAgent: "Mozilla/5.0 iOS",
            ipAddress: "197.3.4.5");

        Assert.Equal(ConversionDispatchStatus.Dispatched, conversion.Status);
        var emqScore = conversionMesh.CalculateEventMatchQualityScore(conversion);
        Assert.True(emqScore >= 9.0);
    }
}
