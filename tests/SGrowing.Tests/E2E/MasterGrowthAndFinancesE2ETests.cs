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

    [Fact]
    public async Task Master_EndToEnd_ConversationalCart_WhenMissingOrderOrMinimalIdentifiers_CalculatesMatchScoreAccurately()
    {
        var db = CreateDbContext();
        var storeService = new ExternalStoreAndSheetsService(db);
        var conversionMesh = new ServerConversionMeshService(db);
        var tenantId = Guid.NewGuid();

        // 1. Generating Google Sheets row for non-existent order throws InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storeService.GenerateGoogleSheetsRowAsync(tenantId, Guid.NewGuid()));

        // 2. Conversion event without user identifiers yields low EMQ score
        var anonymousConversion = await conversionMesh.TrackConversionAsync(
            tenantId,
            AdPlatform.GoogleAdsOffline,
            ConversionEventType.Lead,
            valueAmount: 0m,
            currency: "TND",
            email: null,
            phone: null,
            firstName: null,
            lastName: null,
            city: null
        );

        var lowScore = conversionMesh.CalculateEventMatchQualityScore(anonymousConversion);
        Assert.Equal(0.0, lowScore);

        // 3. Conversion event with phone only yields exactly 3.5 EMQ score
        var phoneOnlyConversion = await conversionMesh.TrackConversionAsync(
            tenantId,
            AdPlatform.MetaCapi,
            ConversionEventType.AddToCart,
            valueAmount: 100m,
            currency: "TND",
            email: null,
            phone: "+21698765432"
        );

        var phoneScore = conversionMesh.CalculateEventMatchQualityScore(phoneOnlyConversion);
        Assert.Equal(3.5, phoneScore);
    }

    [Fact]
    public async Task Master_EndToEnd_ConversionMesh_EmailOnlyMatchScore_And_StoreConnectionFailures()
    {
        var db = CreateDbContext();
        var conversionMesh = new ServerConversionMeshService(db);
        var storeService = new ExternalStoreAndSheetsService(db);
        var tenantId = Guid.NewGuid();

        // 1. Conversion event with email only yields exactly 2.5 EMQ score
        var emailOnlyConversion = await conversionMesh.TrackConversionAsync(
            tenantId,
            AdPlatform.MetaCapi,
            ConversionEventType.Lead,
            valueAmount: 0m,
            currency: "TND",
            email: "director@tech.tn"
        );

        var emailScore = conversionMesh.CalculateEventMatchQualityScore(emailOnlyConversion);
        Assert.Equal(2.5, emailScore);

        // 2. Conversion with email, firstName, and city yields 3.0 EMQ
        var enrichedConversion = await conversionMesh.TrackConversionAsync(
            tenantId,
            AdPlatform.TikTokEvents,
            ConversionEventType.Purchase,
            valueAmount: 180m,
            currency: "TND",
            email: "director@tech.tn",
            firstName: "Sami",
            city: "Tunis"
        );

        var enrichedScore = conversionMesh.CalculateEventMatchQualityScore(enrichedConversion);
        Assert.Equal(3.0, enrichedScore);

        // 3. Pushing fulfillment update for non-existent order throws InvalidOperationException
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storeService.PushFulfillmentUpdateAsync(tenantId, Guid.NewGuid(), "Yalidine", "TRK-999"));
    }

    [Fact]
    public async Task Master_EndToEnd_ConversationalCart_SouthernSurcharge_And_StockDepletion()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var cartEngine = new ConversationalCartEngine(db, wms);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var sku = "SAHARA-AMBER-100ML";

        // 1. Setup Physical Stock in WMS
        await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, sku, initialPhysical: 5);

        // 2. Customer orders 3 bottles to Tataouine (Southern tier governorate with 8 TND shipping)
        var message = "Bonjour je veux commander 3 flacons pour Tataouine mon numero est +21699887766";
        var intent = cartEngine.ParseCartIntent(message, defaultUnitPrice: 60.0m);
        intent.VariantSku = sku;

        Assert.Equal("Tataouine", intent.Governorate);
        Assert.Equal(8.0m, intent.EstimatedShippingFee);
        Assert.Equal(3, intent.Quantity);
        Assert.Equal(188.0m, intent.TotalAmount); // (3 * 60) + 8 = 188.0m

        // 3. Create In-Chat Order and reserve stock
        var orderRes = await cartEngine.CreateInChatOrderAsync(tenantId, conversationId, intent, warehouseId);
        Assert.NotNull(orderRes.Order);

        var stockSummary = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(3, stockSummary.Reserved);
        Assert.Equal(2, stockSummary.Available);

        // 4. Second customer orders 4 units -> WMS defensively records reservation and available stock becomes negative (-2)
        var greedyOrderIntent = new InChatCartIntent
        {
            VariantSku = sku,
            Quantity = 4,
            UnitPrice = 60.0m,
            EstimatedShippingFee = 8.0m,
            CustomerName = "Farid",
            PhoneNumber = "+21622334455",
            Governorate = "Tataouine"
        };

        var secondOrderRes = await cartEngine.CreateInChatOrderAsync(tenantId, Guid.NewGuid(), greedyOrderIntent, warehouseId);
        Assert.NotNull(secondOrderRes.Order);

        var deficitSummary = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(5, deficitSummary.Physical);
        Assert.Equal(7, deficitSummary.Reserved); // 3 + 4 = 7
        Assert.Equal(0, deficitSummary.Available); // Math.Max(0, 5 - 7) = 0
    }
}
