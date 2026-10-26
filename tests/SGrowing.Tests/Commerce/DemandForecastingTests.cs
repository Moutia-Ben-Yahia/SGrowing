using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class DemandForecastingTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"DemandForecastingDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task DemandForecasting_Velocity_Stockout_Alert_And_RestockOrder_Should_Succeed()
    {
        var db = CreateDbContext();
        var service = new DemandForecastingService(db);
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        string sku = "PERFUME-ROYAL-AMBER-100ML";

        // 1. Calculate Product Sales Velocity with 1.8x Meta Ad Scaling Multiplier
        // Sold 70 units over trailing 7 days -> 10 units/day base run rate * 1.8x = 18 units/day projected
        var velocity = await service.CalculateProductVelocityAsync(
            tenantId,
            productId,
            sku,
            "Royal Amber Eau De Parfum 100ml",
            trailing7dUnits: 70m,
            trailing30dUnits: 210m,
            adSpendMultiplier: 1.8m);

        Assert.Equal(10.0m, velocity.DailyRunRateUnits);
        Assert.Equal(18.0m, velocity.WeightedProjectedDailyDemand);

        // 2. Evaluate Stockout Alert: Warehouse has only 54 units left (0 reserved)
        // At 18 units/day, 54 units = exactly 3.0 days of supply remaining!
        // Supplier lead time is 5 days -> Critical stockout imminent!
        var alert = await service.EvaluateStockoutAlertAsync(
            tenantId,
            productId,
            sku,
            currentPhysicalStock: 54,
            currentReservedStock: 0,
            supplierLeadTimeDays: 5,
            safetyStockDays: 3,
            targetCycleDays: 14);

        Assert.Equal(3.0m, alert.EstimatedDaysOfSupplyRemaining);
        Assert.Equal(RestockPriority.CriticalStockoutImminent, alert.Priority);
        Assert.True(alert.RecommendedReorderQuantity > 250); // Covers 22 days of buffer minus 54 available

        // 3. Register Supplier Scorecard (Sousse Fragrance Labs, 5 days lead time, 97.5 quality score)
        var supplier = SupplierScorecard.Create(
            tenantId,
            name: "Sousse Fragrance Labs",
            email: "orders@soussefragrance.tn",
            phone: "+21673889900",
            country: "Tunisia",
            promisedLeadTimeDays: 5,
            actualAvgLeadDays: 5.2m,
            defectRate: 0.3m);

        await service.RegisterSupplierScorecardAsync(tenantId, supplier);
        Assert.True(supplier.QualityScoreOutOf100 >= 90m);

        // 4. Generate Automated Restock Purchase Order
        var restockOrder = await service.GenerateAutomatedRestockOrderAsync(
            tenantId,
            alert.Id,
            supplier.Id,
            unitCost: 18.5m);

        Assert.Equal(RestockOrderStatus.Suggested, restockOrder.Status);
        Assert.Equal(alert.RecommendedReorderQuantity, restockOrder.QuantityToOrder);

        // 5. Approve Restock PO and Place with Supplier
        var approvedOrder = await service.ApproveRestockOrderAsync(tenantId, restockOrder.Id);
        Assert.Equal(RestockOrderStatus.PlacedWithSupplier, approvedOrder.Status);
        Assert.NotNull(approvedOrder.PlacedAtUtc);
        Assert.NotNull(approvedOrder.ExpectedDeliveryDateUtc);
    }
}
