using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class BarcodeFulfillmentTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"BarcodeTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task PickSlip_Scan_Pick_And_Pack_Should_Validate_Skus_And_Update_Wms()
    {
        var db = CreateDbContext();
        var service = new BarcodeFulfillmentService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Seed Inventory
        var inv = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "PERF-JASMIN-50ML",
            PhysicalStock = 20,
            CommittedStock = 1,
            InTransitStock = 0
        };
        db.WarehouseInventories.Add(inv);

        // 2. Seed Order with Items JSON
        var items = new[]
        {
            new { sku = "PERF-JASMIN-50ML", title = "Jasmin de Carthage 50ml", quantity = 1, barcode = "6191234567890" }
        };

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: "ORD-TN-7711",
            customerName: "Amira Ben Romdhane",
            phoneNumber: "+21698223344",
            shippingAddress: "El Menzah 6",
            governorateOrWilaya: "Ariana",
            delegationOrCommune: "Menzah",
            totalAmount: 95.0m,
            itemsJson: JsonSerializer.Serialize(items));
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // 3. Create Pick Slip
        var pickSlip = await service.CreatePickSlipAsync(tenantId, order.Id, warehouseId);
        Assert.NotNull(pickSlip);
        Assert.Single(pickSlip.Items);
        Assert.Equal("6191234567890", pickSlip.Items[0].Barcode);

        // 4. Test Mismatch Scan
        var mismatchResult = await service.ScanItemToPickSlipAsync(tenantId, pickSlip.Id, "WRONG-BARCODE-000");
        Assert.False(mismatchResult.Success);
        Assert.Contains("Mismatch", mismatchResult.Message);

        // 5. Test Valid Scan
        var validResult = await service.ScanItemToPickSlipAsync(tenantId, pickSlip.Id, "6191234567890");
        Assert.True(validResult.Success);
        Assert.True(validResult.IsFullyPicked);

        // 6. Complete Pack & Assign AWB
        var packedSlip = await service.CompletePackAndAssignAwbAsync(tenantId, pickSlip.Id, "YAL-TN-987654", "Yalidine Express");
        Assert.Equal(PickSlipStatus.Packed, packedSlip.Status);

        // Verify Order updated to Dispatched
        var updatedOrder = await db.CodOrders.FindAsync(order.Id);
        Assert.Equal(CodOrderStatus.Dispatched, updatedOrder!.Status);

        // Verify Defensive WMS moved Committed to InTransit
        var updatedInv = await db.WarehouseInventories.FindAsync(inv.Id);
        Assert.Equal(0, updatedInv!.CommittedStock);
        Assert.Equal(1, updatedInv.InTransitStock);
    }

    [Fact]
    public async Task Manifest_And_Rma_Inspection_Should_Aggregate_Parcels_And_Restock_Returned_Stock()
    {
        var db = CreateDbContext();
        var service = new BarcodeFulfillmentService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Seed Inventory in InTransit state
        var inv = new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            Sku = "MONTRE-CHRONO-BLACK",
            PhysicalStock = 10,
            InTransitStock = 1,
            DamagedStock = 0
        };
        db.WarehouseInventories.Add(inv);

        // 2. Create Manifest Run-Sheet
        var manifest = CourierManifestRunSheet.Create(tenantId, "Aramex");
        db.CourierManifestRunSheets.Add(manifest);
        await db.SaveChangesAsync();

        // 3. Scan Parcel into Manifest
        var updatedManifest = await service.ScanParcelToManifestAsync(tenantId, manifest.Id, "ARX-TN-445566");
        Assert.Equal(1, updatedManifest.TotalParcels);
        Assert.Single(updatedManifest.Parcels);

        // 4. Close & Sign Manifest with Driver
        var closedManifest = await service.CloseAndSignManifestAsync(
            tenantId,
            manifest.Id,
            driverName: "Karim Dridi",
            driverPhone: "+21622114477",
            driverSignature: "data:image/svg+xml;base64,PHN2Zz4...");

        Assert.Equal(ManifestStatus.HandedOverToDriver, closedManifest.Status);
        Assert.Equal("Karim Dridi", closedManifest.DriverName);

        // 5. Test RMA Scan & Restock Inspection (Customer Refused Delivery, Parcel Returned)
        var rma = await service.InspectAndRestockRmaParcelAsync(
            tenantId,
            trackingNumber: "ARX-TN-445566",
            inspectorId: "STAFF-01",
            condition: RmaItemCondition.IntactRestock,
            notes: "Package intact, customer was travelling",
            warehouseId: warehouseId);

        Assert.NotNull(rma);
        Assert.Equal(RmaItemCondition.IntactRestock, rma.Condition);

        // Verify Defensive WMS moved item out of InTransit back into Physical Stock
        var updatedInv = await db.WarehouseInventories.FindAsync(inv.Id);
        Assert.Equal(0, updatedInv!.InTransitStock);
        Assert.Equal(11, updatedInv.PhysicalStock);
    }
}
