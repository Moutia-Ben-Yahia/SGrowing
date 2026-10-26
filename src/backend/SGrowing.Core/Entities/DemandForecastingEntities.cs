namespace SGrowing.Core.Entities;

public enum RestockPriority
{
    Normal,
    Elevated,
    High,
    CriticalStockoutImminent
}

public enum RestockOrderStatus
{
    Suggested,
    Approved,
    PlacedWithSupplier,
    PartiallyReceived,
    ReceivedInWarehouse,
    Canceled
}

public class ProductSalesVelocity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Trailing7DaysUnitsSold { get; set; }
    public decimal Trailing30DaysUnitsSold { get; set; }
    public decimal DailyRunRateUnits { get; set; } // e.g. 15.5 units/day
    public decimal ActivePaidAdSpendMultiplier { get; set; } = 1.0m; // 1.0x = organic/steady, 1.8x = active ad scale
    public decimal WeightedProjectedDailyDemand { get; set; } // DailyRunRateUnits * ActivePaidAdSpendMultiplier
    public DateTime LastCalculatedUtc { get; set; } = DateTime.UtcNow;

    public static ProductSalesVelocity Create(
        Guid tenantId,
        Guid productId,
        string sku,
        string productName,
        decimal sold7d,
        decimal sold30d,
        decimal adMultiplier = 1.0m)
    {
        decimal runRate = sold7d > 0 ? (sold7d / 7.0m) : (sold30d > 0 ? sold30d / 30.0m : 0m);
        decimal projected = Math.Round(runRate * adMultiplier, 2);

        return new ProductSalesVelocity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Sku = sku.Trim(),
            ProductName = productName.Trim(),
            Trailing7DaysUnitsSold = sold7d,
            Trailing30DaysUnitsSold = sold30d,
            DailyRunRateUnits = Math.Round(runRate, 2),
            ActivePaidAdSpendMultiplier = adMultiplier,
            WeightedProjectedDailyDemand = projected,
            LastCalculatedUtc = DateTime.UtcNow
        };
    }
}

public class StockoutProjectionAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int CurrentPhysicalStock { get; set; }
    public int CurrentReservedStock { get; set; }
    public int AvailableStock => Math.Max(0, CurrentPhysicalStock - CurrentReservedStock);
    public decimal ProjectedDailyDemand { get; set; }
    public decimal EstimatedDaysOfSupplyRemaining { get; set; }
    public DateTime ProjectedStockoutDateUtc { get; set; }
    public int RecommendedReorderQuantity { get; set; }
    public RestockPriority Priority { get; set; } = RestockPriority.Normal;
    public bool AlertDismissed { get; set; } = false;
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SupplierScorecard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Country { get; set; } = "Tunisia";
    public int PromisedLeadTimeDays { get; set; } = 5;
    public decimal ActualAvgLeadTimeDays { get; set; } = 5.2m;
    public decimal DefectRatePercentage { get; set; } = 0.8m;
    public decimal QualityScoreOutOf100 { get; set; } = 95.0m;
    public int TotalPurchaseOrdersCompleted { get; set; } = 12;
    public bool IsActive { get; set; } = true;
    public DateTime LastEvaluatedUtc { get; set; } = DateTime.UtcNow;

    public static SupplierScorecard Create(
        Guid tenantId,
        string name,
        string email,
        string phone,
        string country = "Tunisia",
        int promisedLeadTimeDays = 5,
        decimal actualAvgLeadDays = 5.0m,
        decimal defectRate = 0.5m)
    {
        decimal score = Math.Max(0m, 100m - (defectRate * 10m) - ((actualAvgLeadDays - promisedLeadTimeDays) * 5m));
        return new SupplierScorecard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierName = name.Trim(),
            ContactEmail = email.Trim().ToLowerInvariant(),
            ContactPhone = phone.Trim(),
            Country = country.Trim(),
            PromisedLeadTimeDays = promisedLeadTimeDays,
            ActualAvgLeadTimeDays = actualAvgLeadDays,
            DefectRatePercentage = defectRate,
            QualityScoreOutOf100 = Math.Min(100m, score),
            IsActive = true,
            LastEvaluatedUtc = DateTime.UtcNow
        };
    }
}

public class AutomatedRestockOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int QuantityToOrder { get; set; }
    public decimal UnitPurchaseCost { get; set; }
    public decimal TotalCost => QuantityToOrder * UnitPurchaseCost;
    public RestockOrderStatus Status { get; set; } = RestockOrderStatus.Suggested;
    public DateTime? PlacedAtUtc { get; set; }
    public DateTime? ExpectedDeliveryDateUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
