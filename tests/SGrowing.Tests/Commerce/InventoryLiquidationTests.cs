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
}
