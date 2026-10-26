using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class MultiWarehouseTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"MultiWhTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task MultiWarehouse_Should_Allocate_By_Proximity_And_Transfer_Stock()
    {
        var db = CreateDbContext();
        var service = new MultiWarehouseService(db);
        var tenantId = Guid.NewGuid();

        var tunisWhId = Guid.NewGuid();
        var sousseWhId = Guid.NewGuid();

        // 1. Setup Warehouses
        db.Warehouses.Add(new Warehouse { Id = tunisWhId, TenantId = tenantId, Name = "Tunis Central Hub", IsPrimary = true });
        db.Warehouses.Add(new Warehouse { Id = sousseWhId, TenantId = tenantId, Name = "Sousse Coastal Hub", IsPrimary = false });

        // 2. Setup Proximity Rule: Sousse & Monastir route to Sousse Hub
        db.RegionalHubProximityRules.Add(RegionalHubProximityRule.Create(tenantId, sousseWhId, "Sousse Coastal Hub", "Sousse", priority: 1));
        db.RegionalHubProximityRules.Add(RegionalHubProximityRule.Create(tenantId, sousseWhId, "Sousse Coastal Hub", "Monastir", priority: 1));

        // 3. Setup Inventories: Sousse has 5 units available of SKU
        db.WarehouseInventories.Add(new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = sousseWhId,
            Sku = "ROBE-ETE-BLEU",
            PhysicalStock = 5,
            ReservedStock = 0
        });

        db.WarehouseInventories.Add(new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = tunisWhId,
            Sku = "ROBE-ETE-BLEU",
            PhysicalStock = 20,
            ReservedStock = 0
        });

        await db.SaveChangesAsync();

        // 4. Test Proximity Allocation for Sousse customer
        var allocation = await service.AllocateOrderToOptimalWarehouseAsync(tenantId, "Sousse", "ROBE-ETE-BLEU", 1);
        Assert.Equal(sousseWhId, allocation.WarehouseId);
        Assert.Contains("Sousse Coastal Hub", allocation.WarehouseName);

        // 5. Create and Execute Inter-Hub Transfer from Tunis to Sousse
        var transfer = await service.CreateInterHubTransferAsync(tenantId, tunisWhId, sousseWhId, "ROBE-ETE-BLEU", 10);
        Assert.Equal(InterHubTransferStatus.Draft, transfer.Status);

        // Dispatch transfer
        var dispatched = await service.DispatchInterHubTransferAsync(tenantId, transfer.Id);
        Assert.Equal(InterHubTransferStatus.InTransit, dispatched.Status);

        var tunisInv = await db.WarehouseInventories.FirstOrDefaultAsync(w => w.WarehouseId == tunisWhId);
        Assert.Equal(19, tunisInv!.PhysicalStock);
        Assert.Equal(1, tunisInv.InTransitStock);

        // Receive transfer at Sousse Hub
        var received = await service.ReceiveInterHubTransferAsync(tenantId, transfer.Id);
        Assert.Equal(InterHubTransferStatus.Received, received.Status);

        var sousseInv = await db.WarehouseInventories.FirstOrDefaultAsync(w => w.WarehouseId == sousseWhId);
        Assert.Equal(6, sousseInv!.PhysicalStock); // 5 + 1 received
    }

    [Fact]
    public async Task ReorderPoint_Should_Generate_Supplier_Purchase_Order_When_Low()
    {
        var db = CreateDbContext();
        var service = new MultiWarehouseService(db);
        var tenantId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        // Inventory with only 12 units available
        db.WarehouseInventories.Add(new WarehouseInventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = whId,
            Sku = "SAVON-NOIR-BIO",
            PhysicalStock = 12,
            ReservedStock = 0
        });
        await db.SaveChangesAsync();

        // Daily demand = 5, lead time = 7 days -> ROP = (5 * 7) + 10 = 45 units
        var po = await service.EvaluateReorderPointAndGeneratePoAsync(tenantId, whId, "SAVON-NOIR-BIO", dailyDemand: 5, leadDays: 7, unitCost: 15.0m);

        Assert.NotNull(po);
        Assert.Equal("Draft", po.Status);
        Assert.StartsWith("PO-", po.PoNumber);
        Assert.Contains("SAVON-NOIR-BIO", po.ItemsJson);
        Assert.True(po.TotalCost > 0);
    }
}
