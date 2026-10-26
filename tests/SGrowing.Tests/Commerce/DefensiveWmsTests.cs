using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class DefensiveWmsTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"WmsTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task DefensiveWms_Should_Accurately_Track_MultiState_Inventory_Lifecycle()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sku = "PERFUME-OUD-100ML";

        // Initial inventory: 50 physical units
        var inv = await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, sku, initialPhysical: 50);
        Assert.Equal(50, inv.PhysicalStock);
        Assert.Equal(50, inv.AvailableStock);

        // 1. Order Checkout -> Stock is Reserved (Available drops to 48, Physical stays 50)
        await wms.ReserveStockOnCheckoutAsync(tenantId, warehouseId, sku, quantity: 2, orderId);
        var summary1 = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(50, summary1.Physical);
        Assert.Equal(2, summary1.Reserved);
        Assert.Equal(0, summary1.Committed);
        Assert.Equal(48, summary1.Available);

        // 2. Phone Confirmation -> Stock moves from Reserved to Committed (packed for courier)
        await wms.CommitStockOnConfirmationAsync(tenantId, warehouseId, sku, quantity: 2, orderId);
        var summary2 = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(50, summary2.Physical);
        Assert.Equal(0, summary2.Reserved);
        Assert.Equal(2, summary2.Committed);
        Assert.Equal(48, summary2.Available);

        // 3. Dispatch to Courier -> Stock physically leaves warehouse (Physical = 48, InTransit = 2)
        await wms.DispatchStockAsync(tenantId, warehouseId, sku, quantity: 2, orderId);
        var summary3 = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(48, summary3.Physical);
        Assert.Equal(0, summary3.Committed);
        Assert.Equal(2, summary3.InTransit);
        Assert.Equal(48, summary3.Available);

        // 4. Courier Delivery Return -> Customer refused, item arrives back into Quarantine
        await wms.ReceiveCourierReturnToQuarantineAsync(tenantId, warehouseId, sku, quantity: 2, orderId);
        var summary4 = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(0, summary4.InTransit);
        Assert.Equal(2, summary4.QuarantineReturned);

        // 5. Barcode Scan & Restock Inspection -> 1 item intact (restocked to physical), 1 item damaged (written off)
        await wms.ScanAndRestockQuarantineItemAsync(tenantId, warehouseId, sku, quantity: 1, isDamaged: false);
        await wms.ScanAndRestockQuarantineItemAsync(tenantId, warehouseId, sku, quantity: 1, isDamaged: true);

        var finalSummary = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(49, finalSummary.Physical); // 48 + 1 restocked
        Assert.Equal(0, finalSummary.QuarantineReturned);
        Assert.Equal(1, finalSummary.Damaged);
        Assert.Equal(49, finalSummary.Available);
    }
}
