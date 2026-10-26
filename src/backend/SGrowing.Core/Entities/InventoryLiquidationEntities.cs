namespace SGrowing.Core.Entities;

public enum StockAgeTier
{
    Fresh,      // < 30 days
    Mature,     // 30 - 60 days
    Stagnant,   // 60 - 90 days
    DeadStock   // > 90 days
}

public class StockAgingLedger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCostPrice { get; set; }
    public decimal OriginalRetailPrice { get; set; }
    public int DaysInWarehouse { get; set; }
    public StockAgeTier Tier { get; set; } = StockAgeTier.Fresh;
    public decimal CurrentEffectivePrice { get; set; }
    public decimal CurrentMarkdownPercentage { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastEvaluatedUtc { get; set; } = DateTime.UtcNow;

    public static StockAgingLedger Create(
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
        var tier = daysInWarehouse switch
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

        decimal effectivePrice = Math.Round(retailPrice * (1m - (markdown / 100m)), 2);

        return new StockAgingLedger
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Sku = sku.Trim(),
            ProductName = productName.Trim(),
            WarehouseId = warehouseId,
            Quantity = quantity,
            UnitCostPrice = costPrice,
            OriginalRetailPrice = retailPrice,
            DaysInWarehouse = daysInWarehouse,
            Tier = tier,
            CurrentMarkdownPercentage = markdown,
            CurrentEffectivePrice = Math.Max(costPrice * 1.05m, effectivePrice), // never drop below cost + 5% floor
            ReceivedAtUtc = DateTime.UtcNow.AddDays(-daysInWarehouse),
            LastEvaluatedUtc = DateTime.UtcNow
        };
    }
}

public class DynamicMarkdownPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public StockAgeTier Tier { get; set; }
    public int MinDaysInStock { get; set; }
    public int MaxDaysInStock { get; set; }
    public decimal RecommendedDiscountPercentage { get; set; }
    public decimal MinimumMarginFloorPercentage { get; set; } = 15.0m;
    public bool AutoApplyToStorefront { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class LiquidationBundleOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string BundleName { get; set; } = string.Empty;
    public string WinningProductSku { get; set; } = string.Empty;
    public string AgingProductSku { get; set; } = string.Empty;
    public decimal OriginalCombinedPrice { get; set; }
    public decimal DiscountedBundlePrice { get; set; }
    public decimal IncentiveDiscountPercentage { get; set; }
    public bool ActiveInChatCpq { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
