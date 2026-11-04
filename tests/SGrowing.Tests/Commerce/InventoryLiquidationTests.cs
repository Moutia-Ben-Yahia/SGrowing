using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class InventoryLiquidationTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"InventoryLiquidationDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task InventoryLiquidation_Aging_Markdown_And_Bundle_Should_Succeed()
    {
        var db = CreateDbContext();
        var service = new InventoryLiquidationService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 1. Fresh Inventory (15 days, 0% markdown)
        var fresh = await service.RecordStockIntakeAgingAsync(
            tenantId,
            Guid.NewGuid(),
            "PARFUM-OUD-NEW",
            "Royal Oud New Batch",
            warehouseId,
            quantity: 50,
            costPrice: 40m,
            retailPrice: 120m,
            daysInWarehouse: 15);

        Assert.Equal(StockAgeTier.Fresh, fresh.Tier);
        Assert.Equal(0m, fresh.CurrentMarkdownPercentage);
        Assert.Equal(120m, fresh.CurrentEffectivePrice);

        // 2. Dead Stock Inventory (110 days in warehouse, 45% markdown)
        var dead = await service.RecordStockIntakeAgingAsync(
            tenantId,
            Guid.NewGuid(),
            "JACKET-WINTER-LASTYEAR",
            "Winter Puffer Jacket 2025",
            warehouseId,
            quantity: 35,
            costPrice: 50m,
            retailPrice: 150m,
            daysInWarehouse: 110);

        Assert.Equal(StockAgeTier.DeadStock, dead.Tier);
        Assert.Equal(45m, dead.CurrentMarkdownPercentage);
        // 150 * 0.55 = 82.5 TND (> cost floor 52.5 TND)
        Assert.Equal(82.5m, dead.CurrentEffectivePrice);

        // 3. Stagnant Stock with low margin floor test
        // Cost: 80, Retail: 100, 75 days in warehouse (Stagnant -> 25% discount -> 75, but floor is 80*1.05 = 84)
        var lowMargin = await service.RecordStockIntakeAgingAsync(
            tenantId,
            Guid.NewGuid(),
            "LEATHER-BELT-STAGNANT",
            "Leather Belt",
            warehouseId,
            quantity: 20,
            costPrice: 80m,
            retailPrice: 100m,
            daysInWarehouse: 75);

        Assert.Equal(StockAgeTier.Stagnant, lowMargin.Tier);
        Assert.Equal(84m, lowMargin.CurrentEffectivePrice); // Profit floor protected!

        // 4. Generate Liquidation Bundle: Buy Oud Perfume (120 TND) + Winter Jacket (150 TND) at 35% bundle discount
        var bundle = await service.GenerateLiquidationBundleAsync(
            tenantId,
            "PARFUM-OUD-NEW",
            "JACKET-WINTER-LASTYEAR",
            "Prestige Winter Luxury Bundle",
            bundleDiscountPct: 35m);

        Assert.Equal(270m, bundle.OriginalCombinedPrice); // 120 + 150
        Assert.Equal(175.5m, bundle.DiscountedBundlePrice); // 270 * 0.65 = 175.5
        Assert.True(bundle.ActiveInChatCpq);
    }

    [Fact]
    public async Task RecordStockIntakeAgingAsync_WithMatureTier_ShouldApplyTenPercentMarkdown()
    {
        var db = CreateDbContext();
        var service = new InventoryLiquidationService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // 45 days in warehouse falls in (30, 60] -> Mature tier (10% markdown)
        var item = await service.RecordStockIntakeAgingAsync(
            tenantId,
            Guid.NewGuid(),
            "SKU-MATURE-SHIRT",
            "Linen Summer Shirt",
            warehouseId,
            quantity: 40,
            costPrice: 30m,
            retailPrice: 100m,
            daysInWarehouse: 45);

        Assert.Equal(StockAgeTier.Mature, item.Tier);
        Assert.Equal(10m, item.CurrentMarkdownPercentage);
        Assert.Equal(90m, item.CurrentEffectivePrice); // 100 * 0.90 = 90 (> 31.5)
    }

    [Fact]
    public async Task RecordStockIntakeAgingAsync_WhenExistingStockUpdated_ShouldRecalculateTierAndPricing()
    {
        var db = CreateDbContext();
        var service = new InventoryLiquidationService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var sku = "SKU-UPDATE-LIFECYCLE";

        // 1. Initial intake: Fresh (20 days)
        var initial = await service.RecordStockIntakeAgingAsync(
            tenantId,
            productId,
            sku,
            "Lifecycle Demo Item",
            warehouseId,
            quantity: 100,
            costPrice: 40m,
            retailPrice: 100m,
            daysInWarehouse: 20);

        Assert.Equal(StockAgeTier.Fresh, initial.Tier);
        Assert.Equal(0m, initial.CurrentMarkdownPercentage);
        Assert.Equal(100m, initial.CurrentEffectivePrice);

        // 2. Updated intake after 70 days in warehouse -> Stagnant (25% markdown)
        var updated = await service.RecordStockIntakeAgingAsync(
            tenantId,
            productId,
            sku,
            "Lifecycle Demo Item",
            warehouseId,
            quantity: 75,
            costPrice: 40m,
            retailPrice: 100m,
            daysInWarehouse: 70);

        Assert.Equal(initial.Id, updated.Id);
        Assert.Equal(75, updated.Quantity);
        Assert.Equal(70, updated.DaysInWarehouse);
        Assert.Equal(StockAgeTier.Stagnant, updated.Tier);
        Assert.Equal(25m, updated.CurrentMarkdownPercentage);
        Assert.Equal(75m, updated.CurrentEffectivePrice);
    }

    [Fact]
    public async Task EvaluateWarehouseAgingAsync_ShouldReevaluateAllStockInWarehouse()
    {
        var db = CreateDbContext();
        var service = new InventoryLiquidationService(db);
        var tenantId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();

        // Seed 4 items across different age brackets
        var item1 = await service.RecordStockIntakeAgingAsync(tenantId, Guid.NewGuid(), "SKU-W-1", "Prod 1", warehouseId, 10, 20m, 50m, 10); // Fresh
        var item2 = await service.RecordStockIntakeAgingAsync(tenantId, Guid.NewGuid(), "SKU-W-2", "Prod 2", warehouseId, 20, 20m, 50m, 40); // Mature
        var item3 = await service.RecordStockIntakeAgingAsync(tenantId, Guid.NewGuid(), "SKU-W-3", "Prod 3", warehouseId, 30, 20m, 50m, 80); // Stagnant
        var item4 = await service.RecordStockIntakeAgingAsync(tenantId, Guid.NewGuid(), "SKU-W-4", "Prod 4", warehouseId, 40, 20m, 50m, 150); // DeadStock

        // Now run warehouse evaluation
        var evaluated = await service.EvaluateWarehouseAgingAsync(tenantId, warehouseId);

        Assert.Equal(4, evaluated.Count);

        var freshEvaluated = evaluated.Single(e => e.Sku == "SKU-W-1");
        Assert.Equal(StockAgeTier.Fresh, freshEvaluated.Tier);
        Assert.Equal(0m, freshEvaluated.CurrentMarkdownPercentage);
        Assert.Equal(50m, freshEvaluated.CurrentEffectivePrice);

        var matureEvaluated = evaluated.Single(e => e.Sku == "SKU-W-2");
        Assert.Equal(StockAgeTier.Mature, matureEvaluated.Tier);
        Assert.Equal(10m, matureEvaluated.CurrentMarkdownPercentage);
        Assert.Equal(45m, matureEvaluated.CurrentEffectivePrice);

        var stagnantEvaluated = evaluated.Single(e => e.Sku == "SKU-W-3");
        Assert.Equal(StockAgeTier.Stagnant, stagnantEvaluated.Tier);
        Assert.Equal(25m, stagnantEvaluated.CurrentMarkdownPercentage);
        Assert.Equal(37.5m, stagnantEvaluated.CurrentEffectivePrice);

        var deadEvaluated = evaluated.Single(e => e.Sku == "SKU-W-4");
        Assert.Equal(StockAgeTier.DeadStock, deadEvaluated.Tier);
        Assert.Equal(45m, deadEvaluated.CurrentMarkdownPercentage);
        Assert.Equal(27.5m, deadEvaluated.CurrentEffectivePrice);
    }

    [Fact]
    public async Task GenerateLiquidationBundleAsync_WithNonExistentSkus_ShouldFallbackToDefaultPrices()
    {
        var db = CreateDbContext();
        var service = new InventoryLiquidationService(db);
        var tenantId = Guid.NewGuid();

        // Skus do not exist in ledger -> fallback winning = 120m, aging = 80m => 200m
        var bundle = await service.GenerateLiquidationBundleAsync(
            tenantId,
            "SKU-GHOST-WINNER",
            "SKU-GHOST-AGING",
            "Mystery Duo Pack",
            bundleDiscountPct: 20m);

        Assert.NotNull(bundle);
        Assert.Equal(200m, bundle.OriginalCombinedPrice);
        Assert.Equal(160m, bundle.DiscountedBundlePrice); // 200 * 0.80 = 160
        Assert.Equal("Mystery Duo Pack", bundle.BundleName);
        Assert.Equal("SKU-GHOST-WINNER", bundle.WinningProductSku);
        Assert.Equal("SKU-GHOST-AGING", bundle.AgingProductSku);
    }

    [Fact]
    public async Task ListAgingLedgerAndActiveBundles_ShouldFilterByTierAndTenantAccurately()
    {
        var db = CreateDbContext();
        var service = new InventoryLiquidationService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var warehouseA = Guid.NewGuid();
        var warehouseB = Guid.NewGuid();

        // Tenant A: 1 Fresh, 2 DeadStock
        await service.RecordStockIntakeAgingAsync(tenantA, Guid.NewGuid(), "SKU-A-FRESH", "Fresh Item", warehouseA, 10, 20m, 50m, 5);
        await service.RecordStockIntakeAgingAsync(tenantA, Guid.NewGuid(), "SKU-A-DEAD1", "Dead Item 1", warehouseA, 10, 20m, 50m, 120);
        await service.RecordStockIntakeAgingAsync(tenantA, Guid.NewGuid(), "SKU-A-DEAD2", "Dead Item 2", warehouseA, 10, 20m, 50m, 140);

        // Tenant B: 1 DeadStock
        await service.RecordStockIntakeAgingAsync(tenantB, Guid.NewGuid(), "SKU-B-DEAD", "Tenant B Dead", warehouseB, 10, 20m, 50m, 150);

        // Bundles
        await service.GenerateLiquidationBundleAsync(tenantA, "SKU-A-FRESH", "SKU-A-DEAD1", "Bundle A1");
        await service.GenerateLiquidationBundleAsync(tenantB, "SKU-B-FRESH", "SKU-B-DEAD", "Bundle B1");

        var allAgingA = await service.ListAgingLedgerAsync(tenantA);
        var deadAgingA = await service.ListAgingLedgerAsync(tenantA, StockAgeTier.DeadStock);
        var bundlesA = await service.ListActiveBundleOffersAsync(tenantA);

        Assert.Equal(3, allAgingA.Count);
        Assert.Equal(2, deadAgingA.Count);
        Assert.All(deadAgingA, item => Assert.Equal(StockAgeTier.DeadStock, item.Tier));

        Assert.Single(bundlesA);
        Assert.Equal("Bundle A1", bundlesA[0].BundleName);
    }
}
