using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class ExternalStoreAndSheetsTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"ExternalStoreTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task IngestExternalStoreOrder_And_PushFulfillment_Should_Sync_BiDirectionally()
    {
        var db = CreateDbContext();
        var service = new ExternalStoreAndSheetsService(db);
        var tenantId = Guid.NewGuid();

        var shopifyPayload = """
        {
            "id": 8829102910,
            "name": "#SH-1049",
            "total_price": "145.00",
            "currency": "TND",
            "customer": {
                "first_name": "Kamel",
                "last_name": "Jaziri",
                "phone": "+21699112233"
            },
            "shipping_address": {
                "address1": "Avenue Bourguiba",
                "city": "Monastir",
                "phone": "+21699112233"
            }
        }
        """;

        // Ingest inbound order
        var order = await service.IngestExternalStoreOrderAsync(tenantId, ExternalStorePlatform.Shopify, shopifyPayload);

        Assert.Equal("#SH-1049", order.OrderNumber);
        Assert.Equal("Kamel Jaziri", order.CustomerName);
        Assert.Equal("+21699112233", order.PhoneNumber);
        Assert.Equal("Monastir", order.GovernorateOrWilaya);
        Assert.Equal(145.00m, order.TotalAmount);

        // Push outbound fulfillment update back to Shopify
        var syncRecord = await service.PushFulfillmentUpdateAsync(tenantId, order.Id, "Aramex", "ARX-SH-1049-99");
        Assert.Equal(StoreSyncDirection.OutboundFulfillmentPushed, syncRecord.SyncDirection);
        Assert.Contains("ARX-SH-1049-99", syncRecord.PayloadJson);
    }

    [Fact]
    public async Task GenerateGoogleSheetsRow_Should_Format_Row_Vector_And_Increment_Counter()
    {
        var db = CreateDbContext();
        var service = new ExternalStoreAndSheetsService(db);
        var tenantId = Guid.NewGuid();

        db.GoogleSheetsSyncConfigs.Add(new GoogleSheetsSyncConfig
        {
            TenantId = tenantId,
            SpreadsheetId = "SHEET_12345",
            SheetTabName = "LiveOrders",
            IsActive = true
        });

        var order = CodOrder.Create(tenantId, "ORD-GS-77", "Faten Dridi", "+21650889900", "El Manar 2", "Tunis", "El Manar", 120m);
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var row = await service.GenerateGoogleSheetsRowAsync(tenantId, order.Id);

        Assert.Equal(11, row.Count);
        Assert.Equal("ORD-GS-77", row[1]);
        Assert.Equal("Faten Dridi", row[2]);
        Assert.Equal("+21650889900", row[3]);
        Assert.Equal(120m, row[6]);

        var updatedConfig = await db.GoogleSheetsSyncConfigs.FirstAsync(c => c.TenantId == tenantId);
        Assert.Equal(1, updatedConfig.RowsAppendedCount);
    }
}
