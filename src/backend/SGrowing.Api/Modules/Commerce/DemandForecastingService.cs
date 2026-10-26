using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Commerce;

public interface IDemandForecastingService
{
    Task<ProductSalesVelocity> CalculateProductVelocityAsync(
        Guid tenantId,
        Guid productId,
        string sku,
        string productName,
        decimal trailing7dUnits,
        decimal trailing30dUnits,
        decimal adSpendMultiplier = 1.0m);

    Task<StockoutProjectionAlert> EvaluateStockoutAlertAsync(
        Guid tenantId,
        Guid productId,
        string sku,
        int currentPhysicalStock,
        int currentReservedStock,
        int supplierLeadTimeDays = 5,
        int safetyStockDays = 3,
        int targetCycleDays = 14);

    Task<SupplierScorecard> RegisterSupplierScorecardAsync(Guid tenantId, SupplierScorecard scorecard);

    Task<AutomatedRestockOrder> GenerateAutomatedRestockOrderAsync(
        Guid tenantId,
        Guid alertId,
        Guid supplierId,
        decimal unitCost);

    Task<AutomatedRestockOrder> ApproveRestockOrderAsync(Guid tenantId, Guid restockOrderId);

    Task<List<StockoutProjectionAlert>> ListStockoutAlertsAsync(Guid tenantId, RestockPriority? minPriority = null);
    Task<List<SupplierScorecard>> ListSuppliersAsync(Guid tenantId);
    Task<List<AutomatedRestockOrder>> ListRestockOrdersAsync(Guid tenantId);
}

public class DemandForecastingService : IDemandForecastingService
{
    private readonly SGrowingDbContext _db;

    public DemandForecastingService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<ProductSalesVelocity> CalculateProductVelocityAsync(
        Guid tenantId,
        Guid productId,
        string sku,
        string productName,
        decimal trailing7dUnits,
        decimal trailing30dUnits,
        decimal adSpendMultiplier = 1.0m)
    {
        var existing = await _db.ProductSalesVelocities
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Sku == sku);

        decimal runRate = trailing7dUnits > 0
            ? (trailing7dUnits / 7.0m)
            : (trailing30dUnits > 0 ? trailing30dUnits / 30.0m : 0m);

        decimal projected = Math.Round(runRate * Math.Max(0.1m, adSpendMultiplier), 2);

        if (existing != null)
        {
            existing.Trailing7DaysUnitsSold = trailing7dUnits;
            existing.Trailing30DaysUnitsSold = trailing30dUnits;
            existing.DailyRunRateUnits = Math.Round(runRate, 2);
            existing.ActivePaidAdSpendMultiplier = adSpendMultiplier;
            existing.WeightedProjectedDailyDemand = projected;
            existing.LastCalculatedUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }

        var velocity = ProductSalesVelocity.Create(
            tenantId,
            productId,
            sku,
            productName,
            trailing7dUnits,
            trailing30dUnits,
            adSpendMultiplier);

        _db.ProductSalesVelocities.Add(velocity);
        await _db.SaveChangesAsync();
        return velocity;
    }

    public async Task<StockoutProjectionAlert> EvaluateStockoutAlertAsync(
        Guid tenantId,
        Guid productId,
        string sku,
        int currentPhysicalStock,
        int currentReservedStock,
        int supplierLeadTimeDays = 5,
        int safetyStockDays = 3,
        int targetCycleDays = 14)
    {
        var velocity = await _db.ProductSalesVelocities
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Sku == sku);

        decimal dailyDemand = velocity?.WeightedProjectedDailyDemand ?? 5.0m;
        if (dailyDemand <= 0m) dailyDemand = 1.0m;

        int available = Math.Max(0, currentPhysicalStock - currentReservedStock);
        decimal daysOfSupply = Math.Round(available / dailyDemand, 1);
        DateTime projectedStockout = DateTime.UtcNow.AddDays((double)daysOfSupply);

        RestockPriority priority = RestockPriority.Normal;
        if (daysOfSupply <= supplierLeadTimeDays)
        {
            priority = RestockPriority.CriticalStockoutImminent;
        }
        else if (daysOfSupply <= (supplierLeadTimeDays + safetyStockDays))
        {
            priority = RestockPriority.High;
        }
        else if (daysOfSupply <= (supplierLeadTimeDays + (safetyStockDays * 2)))
        {
            priority = RestockPriority.Elevated;
        }

        // Recommended reorder = Total demand over (LeadTime + TargetCycle + SafetyStock) minus current available
        int targetCoverageDays = supplierLeadTimeDays + targetCycleDays + safetyStockDays;
        int targetUnits = (int)Math.Ceiling(targetCoverageDays * dailyDemand);
        int recommendedReorder = Math.Max(0, targetUnits - available);

        var alert = await _db.StockoutProjectionAlerts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Sku == sku);

        if (alert != null)
        {
            alert.CurrentPhysicalStock = currentPhysicalStock;
            alert.CurrentReservedStock = currentReservedStock;
            alert.ProjectedDailyDemand = dailyDemand;
            alert.EstimatedDaysOfSupplyRemaining = daysOfSupply;
            alert.ProjectedStockoutDateUtc = projectedStockout;
            alert.RecommendedReorderQuantity = recommendedReorder;
            alert.Priority = priority;
            alert.GeneratedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return alert;
        }

        alert = new StockoutProjectionAlert
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductId = productId,
            Sku = sku.Trim(),
            CurrentPhysicalStock = currentPhysicalStock,
            CurrentReservedStock = currentReservedStock,
            ProjectedDailyDemand = dailyDemand,
            EstimatedDaysOfSupplyRemaining = daysOfSupply,
            ProjectedStockoutDateUtc = projectedStockout,
            RecommendedReorderQuantity = recommendedReorder,
            Priority = priority,
            GeneratedAtUtc = DateTime.UtcNow
        };

        _db.StockoutProjectionAlerts.Add(alert);
        await _db.SaveChangesAsync();
        return alert;
    }

    public async Task<SupplierScorecard> RegisterSupplierScorecardAsync(Guid tenantId, SupplierScorecard scorecard)
    {
        scorecard.TenantId = tenantId;
        _db.SupplierScorecards.Add(scorecard);
        await _db.SaveChangesAsync();
        return scorecard;
    }

    public async Task<AutomatedRestockOrder> GenerateAutomatedRestockOrderAsync(
        Guid tenantId,
        Guid alertId,
        Guid supplierId,
        decimal unitCost)
    {
        var alert = await _db.StockoutProjectionAlerts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == alertId);

        if (alert == null)
            throw new InvalidOperationException($"Alert {alertId} not found.");

        var supplier = await _db.SupplierScorecards
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == supplierId);

        string supplierName = supplier?.SupplierName ?? "Primary Factory Supplier";

        var order = new AutomatedRestockOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierId = supplierId,
            SupplierName = supplierName,
            ProductId = alert.ProductId,
            Sku = alert.Sku,
            ProductName = $"Restock for {alert.Sku}",
            QuantityToOrder = Math.Max(20, alert.RecommendedReorderQuantity),
            UnitPurchaseCost = unitCost,
            Status = RestockOrderStatus.Suggested,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.AutomatedRestockOrders.Add(order);
        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<AutomatedRestockOrder> ApproveRestockOrderAsync(Guid tenantId, Guid restockOrderId)
    {
        var order = await _db.AutomatedRestockOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == restockOrderId);

        if (order == null)
            throw new InvalidOperationException($"Restock order {restockOrderId} not found.");

        var supplier = await _db.SupplierScorecards
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == order.SupplierId);

        int leadDays = supplier?.PromisedLeadTimeDays ?? 5;

        order.Status = RestockOrderStatus.PlacedWithSupplier;
        order.PlacedAtUtc = DateTime.UtcNow;
        order.ExpectedDeliveryDateUtc = DateTime.UtcNow.AddDays(leadDays);

        await _db.SaveChangesAsync();
        return order;
    }

    public async Task<List<StockoutProjectionAlert>> ListStockoutAlertsAsync(Guid tenantId, RestockPriority? minPriority = null)
    {
        var query = _db.StockoutProjectionAlerts.Where(a => a.TenantId == tenantId && !a.AlertDismissed);
        if (minPriority.HasValue)
        {
            query = query.Where(a => a.Priority >= minPriority.Value);
        }
        return await query.OrderByDescending(a => a.Priority).ToListAsync();
    }

    public async Task<List<SupplierScorecard>> ListSuppliersAsync(Guid tenantId)
    {
        return await _db.SupplierScorecards
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .OrderByDescending(s => s.QualityScoreOutOf100)
            .ToListAsync();
    }

    public async Task<List<AutomatedRestockOrder>> ListRestockOrdersAsync(Guid tenantId)
    {
        return await _db.AutomatedRestockOrders
            .Where(o => o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .ToListAsync();
    }
}
