using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface IInventoryLiquidationService
{
    Task<StockAgingLedger> RecordStockIntakeAgingAsync(
        Guid tenantId,
        Guid productId,
        string sku,
        string productName,
        Guid warehouseId,
        int quantity,
        decimal costPrice,
        decimal retailPrice,
        int daysInWarehouse);

    Task<List<StockAgingLedger>> EvaluateWarehouseAgingAsync(Guid tenantId, Guid warehouseId);

    Task<LiquidationBundleOffer> GenerateLiquidationBundleAsync(
        Guid tenantId,
        string winningSku,
        string agingSku,
        string bundleName,
        decimal bundleDiscountPct = 30m);

    Task<List<StockAgingLedger>> ListAgingLedgerAsync(Guid tenantId, StockAgeTier? tier = null);
    Task<List<LiquidationBundleOffer>> ListActiveBundleOffersAsync(Guid tenantId);
}

public class InventoryLiquidationService : IInventoryLiquidationService
{
    private readonly SGrowingDbContext _db;

    public InventoryLiquidationService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<StockAgingLedger> RecordStockIntakeAgingAsync(
        Guid tenantId,
        Guid productId,
        string sku,
        string productName,
        Guid warehouseId,
        int quantity,
        decimal costPrice,
        decimal retailPrice,
        int daysInWarehouse)
    {
        var existing = await _db.StockAgingLedgers
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.WarehouseId == warehouseId && s.Sku == sku);

        if (existing != null)
        {
            existing.Quantity = quantity;
            existing.DaysInWarehouse = daysInWarehouse;
            existing.UnitCostPrice = costPrice;
            existing.OriginalRetailPrice = retailPrice;

            existing.Tier = daysInWarehouse switch
            {
                < 30 => StockAgeTier.Fresh,
                <= 60 => StockAgeTier.Mature,
                <= 90 => StockAgeTier.Stagnant,
                _ => StockAgeTier.DeadStock
            };

            decimal markdown = existing.Tier switch
            {
                StockAgeTier.Fresh => 0m,
                StockAgeTier.Mature => 10m,
                StockAgeTier.Stagnant => 25m,
                StockAgeTier.DeadStock => 45m,
                _ => 0m
            };

            decimal effective = Math.Round(retailPrice * (1m - (markdown / 100m)), 2);
            existing.CurrentMarkdownPercentage = markdown;
            existing.CurrentEffectivePrice = Math.Max(costPrice * 1.05m, effective);
            existing.LastEvaluatedUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return existing;
        }

        var ledger = StockAgingLedger.Create(
            tenantId,
            productId,
            sku,
            productName,
            warehouseId,
            quantity,
            costPrice,
            retailPrice,
            daysInWarehouse);

        _db.StockAgingLedgers.Add(ledger);
        await _db.SaveChangesAsync();
        return ledger;
    }

    public async Task<List<StockAgingLedger>> EvaluateWarehouseAgingAsync(Guid tenantId, Guid warehouseId)
    {
        var ledgers = await _db.StockAgingLedgers
            .Where(s => s.TenantId == tenantId && s.WarehouseId == warehouseId)
            .ToListAsync();

        foreach (var item in ledgers)
        {
            var tier = item.DaysInWarehouse switch
            {
                < 30 => StockAgeTier.Fresh,
                <= 60 => StockAgeTier.Mature,
                <= 90 => StockAgeTier.Stagnant,
                _ => StockAgeTier.DeadStock
            };

            decimal markdown = tier switch
            {
                StockAgeTier.Fresh => 0m,
                StockAgeTier.Mature => 10m,
                StockAgeTier.Stagnant => 25m,
                StockAgeTier.DeadStock => 45m,
                _ => 0m
            };

            decimal effective = Math.Round(item.OriginalRetailPrice * (1m - (markdown / 100m)), 2);
            item.Tier = tier;
            item.CurrentMarkdownPercentage = markdown;
            item.CurrentEffectivePrice = Math.Max(item.UnitCostPrice * 1.05m, effective);
            item.LastEvaluatedUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return ledgers;
    }

    public async Task<LiquidationBundleOffer> GenerateLiquidationBundleAsync(
        Guid tenantId,
        string winningSku,
        string agingSku,
        string bundleName,
        decimal bundleDiscountPct = 30m)
    {
        var winningItem = await _db.StockAgingLedgers
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Sku == winningSku);

        var agingItem = await _db.StockAgingLedgers
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Sku == agingSku);

        decimal winningPrice = winningItem?.OriginalRetailPrice ?? 120m;
        decimal agingPrice = agingItem?.OriginalRetailPrice ?? 80m;
        decimal combined = winningPrice + agingPrice;

        decimal discounted = Math.Round(combined * (1m - (bundleDiscountPct / 100m)), 2);

        var bundle = new LiquidationBundleOffer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BundleName = bundleName.Trim(),
            WinningProductSku = winningSku.Trim(),
            AgingProductSku = agingSku.Trim(),
            OriginalCombinedPrice = combined,
            DiscountedBundlePrice = discounted,
            IncentiveDiscountPercentage = bundleDiscountPct,
            ActiveInChatCpq = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.LiquidationBundleOffers.Add(bundle);
        await _db.SaveChangesAsync();
        return bundle;
    }

    public async Task<List<StockAgingLedger>> ListAgingLedgerAsync(Guid tenantId, StockAgeTier? tier = null)
    {
        var query = _db.StockAgingLedgers.Where(s => s.TenantId == tenantId);
        if (tier.HasValue)
        {
            query = query.Where(s => s.Tier == tier.Value);
        }
        return await query.OrderByDescending(s => s.DaysInWarehouse).ToListAsync();
    }

    public async Task<List<LiquidationBundleOffer>> ListActiveBundleOffersAsync(Guid tenantId)
    {
        return await _db.LiquidationBundleOffers
            .Where(b => b.TenantId == tenantId && b.ActiveInChatCpq)
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToListAsync();
    }
}
