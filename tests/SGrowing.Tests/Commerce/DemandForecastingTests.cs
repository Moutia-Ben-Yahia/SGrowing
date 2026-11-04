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

    [Fact]
    public async Task CalculateProductVelocityAsync_WithZeroSales_ShouldReturnZeroDailyRunRate()
    {
        var db = CreateDbContext();
        var service = new DemandForecastingService(db);
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        string sku = "SKU-COLD-START";

        var velocity = await service.CalculateProductVelocityAsync(
            tenantId,
            productId,
            sku,
            "Cold Start Item",
            trailing7dUnits: 0m,
            trailing30dUnits: 0m,
            adSpendMultiplier: 1.0m);

        Assert.NotNull(velocity);
        Assert.Equal(0m, velocity.DailyRunRateUnits);
        Assert.Equal(0m, velocity.WeightedProjectedDailyDemand);
    }

    [Fact]
    public async Task EvaluateStockoutAlertAsync_WhenStockAbundant_ShouldAssignNormalPriority()
    {
        var db = CreateDbContext();
        var service = new DemandForecastingService(db);
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        string sku = "SKU-ABUNDANT-STOCK";

        // Seed velocity: 2 units per day
        await service.CalculateProductVelocityAsync(
            tenantId, productId, sku, "Abundant Item",
            trailing7dUnits: 14m, trailing30dUnits: 60m, adSpendMultiplier: 1.0m);

        // Warehouse has 200 units physical (0 reserved) -> 100 days of supply
        // Supplier lead time is 5 days, safety 3 days, target 14 days
        var alert = await service.EvaluateStockoutAlertAsync(
            tenantId,
            productId,
            sku,
            currentPhysicalStock: 200,
            currentReservedStock: 0,
            supplierLeadTimeDays: 5,
            safetyStockDays: 3,
            targetCycleDays: 14);

        Assert.Equal(100.0m, alert.EstimatedDaysOfSupplyRemaining);
        Assert.Equal(RestockPriority.Normal, alert.Priority);
        Assert.Equal(0, alert.RecommendedReorderQuantity); // Target coverage 22 days * 2 = 44 units < 200 available
    }

    [Fact]
    public async Task EvaluateStockoutAlertAsync_WhenDaysOfSupplyUnderLeadPlusSafety_ShouldAssignHighPriority()
    {
        var db = CreateDbContext();
        var service = new DemandForecastingService(db);
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        string sku = "SKU-HIGH-PRIORITY";

        // Velocity: 10 units per day
        await service.CalculateProductVelocityAsync(
            tenantId, productId, sku, "Fast Item",
            trailing7dUnits: 70m, trailing30dUnits: 300m, adSpendMultiplier: 1.0m);

        // 70 units physical (0 reserved) = exactly 7.0 days of supply
        // Lead time 5 days + Safety 3 days = 8 days cutoff. 7 days <= 8 days -> High priority!
        var alert = await service.EvaluateStockoutAlertAsync(
            tenantId,
            productId,
            sku,
            currentPhysicalStock: 70,
            currentReservedStock: 0,
            supplierLeadTimeDays: 5,
            safetyStockDays: 3,
            targetCycleDays: 14);

        Assert.Equal(7.0m, alert.EstimatedDaysOfSupplyRemaining);
        Assert.Equal(RestockPriority.High, alert.Priority);
        Assert.True(alert.RecommendedReorderQuantity > 0);
    }

    [Fact]
    public async Task GenerateAutomatedRestockOrderAsync_WithUnknownAlert_ShouldThrowInvalidOperationException()
    {
        var db = CreateDbContext();
        var service = new DemandForecastingService(db);
        var tenantId = Guid.NewGuid();

        var nonExistentAlertId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAutomatedRestockOrderAsync(tenantId, nonExistentAlertId, supplierId, 25m));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task ApproveRestockOrderAsync_WithUnknownOrder_ShouldThrowInvalidOperationException()
    {
        var db = CreateDbContext();
        var service = new DemandForecastingService(db);
        var tenantId = Guid.NewGuid();

        var nonExistentOrderId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ApproveRestockOrderAsync(tenantId, nonExistentOrderId));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task ListStockoutAlertsAndRestockOrders_ShouldFilterByTenantAndMinPriority()
    {
        var db = CreateDbContext();
        var service = new DemandForecastingService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Tenant A: 1 Normal, 1 Critical
        await service.CalculateProductVelocityAsync(tenantA, Guid.NewGuid(), "SKU-A-NORM", "Normal Item", 7m, 30m, 1.0m);
        await service.EvaluateStockoutAlertAsync(tenantA, Guid.NewGuid(), "SKU-A-NORM", 100, 0, 5, 3, 14); // Normal

        await service.CalculateProductVelocityAsync(tenantA, Guid.NewGuid(), "SKU-A-CRIT", "Critical Item", 70m, 300m, 1.0m);
        await service.EvaluateStockoutAlertAsync(tenantA, Guid.NewGuid(), "SKU-A-CRIT", 20, 0, 5, 3, 14); // Critical (2 days supply <= 5)

        // Tenant B: 1 Critical
        await service.CalculateProductVelocityAsync(tenantB, Guid.NewGuid(), "SKU-B-CRIT", "B Critical", 70m, 300m, 1.0m);
        await service.EvaluateStockoutAlertAsync(tenantB, Guid.NewGuid(), "SKU-B-CRIT", 10, 0, 5, 3, 14);

        var allAlertsA = await service.ListStockoutAlertsAsync(tenantA);
        var criticalAlertsA = await service.ListStockoutAlertsAsync(tenantA, RestockPriority.CriticalStockoutImminent);

        Assert.Equal(2, allAlertsA.Count);
        Assert.Single(criticalAlertsA);
        Assert.Equal("SKU-A-CRIT", criticalAlertsA[0].Sku);
    }
}
