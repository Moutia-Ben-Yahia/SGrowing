using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class ReverseExchangeTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"ReverseExchangeDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task DropAndSwap_Express_Reverse_Logistics_Lifecycle_Should_Succeed()
    {
        var db = CreateDbContext();
        var service = new ReverseExchangeService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Seed WMS Inventories for Original SKU (Shoe Size 42) and Replacement SKU (Shoe Size 43)
        var originalInv = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SNEAKER-LEATHER-42",
            PhysicalStock = 0,
            ReservedStock = 0
        };
        var replacementInv = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "SNEAKER-LEATHER-43",
            PhysicalStock = 10,
            ReservedStock = 0
        };
        db.WarehouseInventories.AddRange(originalInv, replacementInv);

        // 2. Original Order (Delivered size 42)
        var order = CodOrder.Create(
            tenantId,
            "ORD-ORIG-88",
            "Sami Trabelsi",
            "+21622334455",
            "Avenue Habib Bourguiba",
            "Tunis",
            "Centre Ville",
            195.0m,
            itemsJson: "[{\"sku\":\"SNEAKER-LEATHER-42\",\"title\":\"Leather Sneaker Size 42\",\"quantity\":1}]");
        order.Status = CodOrderStatus.Delivered;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // 3. Customer requests Size Exchange (42 -> 43, wrong size)
        var exchange = await service.RequestExchangeAsync(
            tenantId,
            order.Id,
            "SNEAKER-LEATHER-42",
            "SNEAKER-LEATHER-43",
            "Leather Sneaker Size 43",
            "Wrong size: too tight",
            cashAdjustment: 0m);

        Assert.Equal(ExchangeStatus.Requested, exchange.Status);
        // Replacement inventory is defensively reserved
        Assert.Equal(1, replacementInv.ReservedStock);
        Assert.Equal(9, replacementInv.AvailableStock);

        // 4. Approve & Dispatch Drop-and-Swap Courier Manifest
        var courierId = Guid.NewGuid();
        var manifest = await service.ApproveAndDispatchSwapManifestAsync(
            tenantId,
            exchange.Id,
            courierId,
            "Aramex Swap Express",
            swapDeliveryFee: 9.5m);

        Assert.StartsWith("SWAP-", manifest.SwapTrackingBarcode);
        Assert.Equal(9.5m, manifest.BundledSwapDeliveryFee);

        // 5. Courier confirms Doorstep Swap (Delivers 43, collects 42 in single stop)
        var completedSwap = await service.ConfirmDoorstepSwapAsync(
            tenantId,
            manifest.Id,
            handoverNewParcelConfirmed: true,
            returnParcelCollectedConfirmed: true);

        Assert.Equal(ExchangeStatus.SwapCompletedAtDoorstep, completedSwap.Status);
        // Replacement inventory deducted from physical and reserved
        Assert.Equal(9, replacementInv.PhysicalStock);
        Assert.Equal(0, replacementInv.ReservedStock);

        // 6. Returned Parcel arrives at Warehouse -> QA Inspection Grade A (Clean, pristine condition)
        var inspection = await service.ProcessWarehouseReturnIntakeAsync(
            tenantId,
            exchange.Id,
            SwapInspectionResult.GradeA_RestockedSellable,
            "Pristine condition in original box, restocked");

        Assert.True(inspection.RestockedToPhysicalWms);
        Assert.Equal(1, originalInv.PhysicalStock); // Returned unit is back in sellable inventory!
        Assert.Equal(ExchangeStatus.ReturnInspectedAndRestocked, exchange.Status);
        Assert.NotNull(exchange.CompletedAtUtc);
    }
}
