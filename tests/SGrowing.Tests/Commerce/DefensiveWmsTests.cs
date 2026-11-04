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

    [Fact]
    public async Task ReleaseStockOnCancellationAsync_ShouldDecrementReservedStockAndRecordCancellationMovement()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sku = "SKU-CANCELLATION-TEST";

        // Initial 20 physical units, reserve 5
        await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, sku, initialPhysical: 20);
        await wms.ReserveStockOnCheckoutAsync(tenantId, warehouseId, sku, 5, orderId);

        var beforeCancel = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(5, beforeCancel.Reserved);
        Assert.Equal(15, beforeCancel.Available);

        // Customer cancels order before confirmation
        await wms.ReleaseStockOnCancellationAsync(tenantId, warehouseId, sku, 5, orderId, "CustomerSupport");

        var afterCancel = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(0, afterCancel.Reserved);
        Assert.Equal(20, afterCancel.Available);
        Assert.Equal(20, afterCancel.Physical);

        // Verify stock movement logged
        var movement = await db.StockMovements.FirstOrDefaultAsync(m => m.OrderId == orderId && m.MovementType == StockMovementType.CancellationRestock);
        Assert.NotNull(movement);
        Assert.Equal(-5, movement.QuantityChange);
        Assert.Equal("CustomerSupport", movement.PerformedBy);
    }

    [Fact]
    public async Task CommitStockOnConfirmationAsync_WithPartialCancellation_ShouldClampReservedStock()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sku = "SKU-CLAMP-TEST";

        await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, sku, initialPhysical: 10);
        await wms.ReserveStockOnCheckoutAsync(tenantId, warehouseId, sku, 3, orderId);

        // Commit 5 (more than reserved 3) -> should clamp reserved to 0, committed becomes 5
        await wms.CommitStockOnConfirmationAsync(tenantId, warehouseId, sku, 5, orderId);

        var summary = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(0, summary.Reserved);
        Assert.Equal(5, summary.Committed);
        Assert.Equal(10, summary.Physical);
    }

    [Fact]
    public async Task ScanAndRestockQuarantineItemAsync_WhenMultipleItemsRestocked_ShouldAccuratelyReflectPhysical()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sku = "SKU-RETURN-BATCH";

        await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, sku, initialPhysical: 100);
        await wms.ReserveStockOnCheckoutAsync(tenantId, warehouseId, sku, 10, orderId);
        await wms.CommitStockOnConfirmationAsync(tenantId, warehouseId, sku, 10, orderId);
        await wms.DispatchStockAsync(tenantId, warehouseId, sku, 10, orderId);

        // 10 units in transit returned to quarantine
        await wms.ReceiveCourierReturnToQuarantineAsync(tenantId, warehouseId, sku, 10, orderId);

        var quarantineSummary = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(90, quarantineSummary.Physical);
        Assert.Equal(10, quarantineSummary.QuarantineReturned);

        // Inspect 8 undamaged items -> restock
        await wms.ScanAndRestockQuarantineItemAsync(tenantId, warehouseId, sku, 8, isDamaged: false, inspectorId: "Agent_Sarah");

        // Inspect 2 damaged items -> write off
        await wms.ScanAndRestockQuarantineItemAsync(tenantId, warehouseId, sku, 2, isDamaged: true, inspectorId: "Agent_Sarah");

        var finalSummary = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(98, finalSummary.Physical); // 90 + 8
        Assert.Equal(0, finalSummary.QuarantineReturned);
        Assert.Equal(2, finalSummary.Damaged);
        Assert.Equal(98, finalSummary.Available);
    }

    [Fact]
    public async Task GetStockSummaryAsync_WithNonExistentInventory_ShouldCreateDefaultZeroPhysical()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var sku = "SKU-BRAND-NEW";

        var summary = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);

        Assert.NotNull(summary);
        Assert.Equal("SKU-BRAND-NEW", summary.Sku);
        Assert.Equal(0, summary.Physical);
        Assert.Equal(0, summary.Reserved);
        Assert.Equal(0, summary.Committed);
        Assert.Equal(0, summary.InTransit);
        Assert.Equal(0, summary.QuarantineReturned);
        Assert.Equal(0, summary.Damaged);
        Assert.Equal(0, summary.Available);
    }

    [Fact]
    public async Task GetOrCreateInventoryAsync_AcrossMultipleWarehouses_ShouldIsolateStock()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var tenantId = Guid.NewGuid();
        var warehouseTunis = Guid.NewGuid();
        var warehouseSfax = Guid.NewGuid();
        var sku = "SKU-SHARED-CATALOG";

        var invTunis = await wms.GetOrCreateInventoryAsync(tenantId, warehouseTunis, sku, initialPhysical: 100);
        var invSfax = await wms.GetOrCreateInventoryAsync(tenantId, warehouseSfax, sku, initialPhysical: 40);

        Assert.NotEqual(invTunis.WarehouseId, invSfax.WarehouseId);
        Assert.Equal(100, invTunis.PhysicalStock);
        Assert.Equal(40, invSfax.PhysicalStock);

        var summaryTunis = await wms.GetStockSummaryAsync(tenantId, warehouseTunis, sku);
        var summarySfax = await wms.GetStockSummaryAsync(tenantId, warehouseSfax, sku);

        Assert.Equal(100, summaryTunis.Physical);
        Assert.Equal(40, summarySfax.Physical);
    }

    [Fact]
    public async Task DispatchStockAsync_ClampsCommittedAndPhysicalStockAtZero()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sku = "SKU-OVER-DISPATCH";

        await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, sku, initialPhysical: 2);
        await wms.ReserveStockOnCheckoutAsync(tenantId, warehouseId, sku, 2, orderId);
        await wms.CommitStockOnConfirmationAsync(tenantId, warehouseId, sku, 2, orderId);

        // Dispatch 5 (more than 2 committed & physical) -> should clamp physical & committed at 0
        await wms.DispatchStockAsync(tenantId, warehouseId, sku, 5, orderId);

        var summary = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(0, summary.Physical);
        Assert.Equal(0, summary.Committed);
        Assert.Equal(5, summary.InTransit);
    }

    [Fact]
    public async Task ReceiveCourierReturnToQuarantineAsync_ClampsInTransitAtZero()
    {
        var db = CreateDbContext();
        var wms = new DefensiveWmsService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var sku = "SKU-RETURN-CLAMP";

        await wms.GetOrCreateInventoryAsync(tenantId, warehouseId, sku, initialPhysical: 10);
        await wms.ReserveStockOnCheckoutAsync(tenantId, warehouseId, sku, 2, orderId);
        await wms.CommitStockOnConfirmationAsync(tenantId, warehouseId, sku, 2, orderId);
        await wms.DispatchStockAsync(tenantId, warehouseId, sku, 2, orderId);

        // Return 6 to quarantine (more than 2 in transit) -> InTransit clamps to 0
        await wms.ReceiveCourierReturnToQuarantineAsync(tenantId, warehouseId, sku, 6, orderId);

        var summary = await wms.GetStockSummaryAsync(tenantId, warehouseId, sku);
        Assert.Equal(0, summary.InTransit);
        Assert.Equal(6, summary.QuarantineReturned);
    }
}
