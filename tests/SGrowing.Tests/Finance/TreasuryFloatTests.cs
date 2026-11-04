using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Finance;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Finance;

public class TreasuryFloatTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"TreasuryFloatTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task TreasuryFloat_Should_Valuate_InTransit_And_Delivered_Unremitted_Cash()
    {
        var db = CreateDbContext();
        var service = new TreasuryFloatService(db);
        var tenantId = Guid.NewGuid();

        // 1. Seed Orders: 2 in-transit (100 DT each), 3 delivered (150 DT each)
        for (int i = 1; i <= 2; i++)
        {
            var o = CodOrder.Create(tenantId, $"ORD-TRANSIT-{i}", $"Customer {i}", "+21620000000", "Address", "Tunis", "Tunis", 100.0m);
            o.Status = CodOrderStatus.Dispatched;
            db.CodOrders.Add(o);
        }

        for (int i = 1; i <= 3; i++)
        {
            var o = CodOrder.Create(tenantId, $"ORD-DELIVERED-{i}", $"Customer {i}", "+21620000000", "Address", "Tunis", "Tunis", 150.0m);
            o.Status = CodOrderStatus.Delivered;
            db.CodOrders.Add(o);
        }

        // Configure Remittance Schedule (Tuesday wire transfers)
        db.CarrierRemittanceScheduleConfigs.Add(new CarrierRemittanceScheduleConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = Guid.NewGuid(),
            CourierName = "Yalidine Express",
            Frequency = CarrierRemittanceFrequency.Weekly,
            RemittanceDayOfWeek = DayOfWeek.Tuesday,
            ContractFeePerParcel = 7.0m,
            IsActive = true
        });

        await db.SaveChangesAsync();

        // 2. Calculate Float Valuation
        var valuation = await service.CalculateFloatValuationAsync(tenantId);

        Assert.Equal(2, valuation.InTransitParcelCount);
        Assert.Equal(200.0m, valuation.InTransitCashValue);
        Assert.Equal(3, valuation.DeliveredUnremittedParcelCount);
        Assert.Equal(450.0m, valuation.DeliveredUnremittedCashValue);
        Assert.Equal(35.0m, valuation.EstimatedCourierFees); // 5 parcels * 7.0m
        Assert.Equal(615.0m, valuation.NetReceivableFloatValue); // (200 + 450) - 35 = 615 DT

        // 3. Generate 14-Day Cash Flow Forecast
        var forecast = await service.Generate14DayCashFlowForecastAsync(tenantId, DateTime.UtcNow);

        Assert.NotEmpty(forecast);
        Assert.All(forecast, f =>
        {
            Assert.Equal("Yalidine Express", f.CourierName);
            Assert.True(f.ExpectedNetRemittance > 0);
            Assert.Equal(DayOfWeek.Tuesday, f.ForecastDate.DayOfWeek);
        });
    }

    [Fact]
    public async Task CalculateFloatValuationAsync_ZeroOrders_ReturnsZeroValuation()
    {
        var db = CreateDbContext();
        var service = new TreasuryFloatService(db);
        var tenantId = Guid.NewGuid();

        var valuation = await service.CalculateFloatValuationAsync(tenantId);

        Assert.Equal(0, valuation.InTransitParcelCount);
        Assert.Equal(0m, valuation.InTransitCashValue);
        Assert.Equal(0, valuation.DeliveredUnremittedParcelCount);
        Assert.Equal(0m, valuation.DeliveredUnremittedCashValue);
        Assert.Equal(0m, valuation.EstimatedCourierFees);
        Assert.Equal(0m, valuation.NetReceivableFloatValue);
    }

    [Fact]
    public async Task CalculateFloatValuationAsync_ExcludesNonTransitAndNonDeliveredOrders()
    {
        var db = CreateDbContext();
        var service = new TreasuryFloatService(db);
        var tenantId = Guid.NewGuid();

        var pending = CodOrder.Create(tenantId, "ORD-PENDING", "Customer", "+21620000000", "Address", "Tunis", "Tunis", 80.0m);
        pending.Status = CodOrderStatus.PendingConfirmation;

        var confirmed = CodOrder.Create(tenantId, "ORD-CONFIRMED", "Customer", "+21620000000", "Address", "Tunis", "Tunis", 90.0m);
        confirmed.Status = CodOrderStatus.Confirmed;

        var cancelled = CodOrder.Create(tenantId, "ORD-CANCELLED", "Customer", "+21620000000", "Address", "Tunis", "Tunis", 120.0m);
        cancelled.Status = CodOrderStatus.Cancelled;

        var dispatched = CodOrder.Create(tenantId, "ORD-DISPATCHED", "Customer", "+21620000000", "Address", "Tunis", "Tunis", 150.0m);
        dispatched.Status = CodOrderStatus.Dispatched;

        db.CodOrders.AddRange(pending, confirmed, cancelled, dispatched);
        await db.SaveChangesAsync();

        var valuation = await service.CalculateFloatValuationAsync(tenantId);

        Assert.Equal(1, valuation.InTransitParcelCount);
        Assert.Equal(150.0m, valuation.InTransitCashValue);
        Assert.Equal(0, valuation.DeliveredUnremittedParcelCount);
        Assert.Equal(7.0m, valuation.EstimatedCourierFees);
        Assert.Equal(143.0m, valuation.NetReceivableFloatValue);
    }

    [Fact]
    public async Task CalculateFloatValuationAsync_MultiTenantIsolation_ExcludesOrdersOfOtherTenants()
    {
        var db = CreateDbContext();
        var service = new TreasuryFloatService(db);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var orderA = CodOrder.Create(tenantA, "ORD-A", "Cust A", "+21620000000", "Address", "Tunis", "Tunis", 100.0m);
        orderA.Status = CodOrderStatus.Dispatched;

        var orderB = CodOrder.Create(tenantB, "ORD-B", "Cust B", "+21620000000", "Address", "Tunis", "Tunis", 300.0m);
        orderB.Status = CodOrderStatus.Dispatched;

        db.CodOrders.AddRange(orderA, orderB);
        await db.SaveChangesAsync();

        var valuationA = await service.CalculateFloatValuationAsync(tenantA);

        Assert.Equal(1, valuationA.InTransitParcelCount);
        Assert.Equal(100.0m, valuationA.InTransitCashValue);
        Assert.Equal(93.0m, valuationA.NetReceivableFloatValue);
    }

    [Fact]
    public async Task Generate14DayCashFlowForecastAsync_WithFridaySchedule_AlignsToFridaysWithConfidenceLevels()
    {
        var db = CreateDbContext();
        var service = new TreasuryFloatService(db);
        var tenantId = Guid.NewGuid();

        db.CarrierRemittanceScheduleConfigs.Add(new CarrierRemittanceScheduleConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = Guid.NewGuid(),
            CourierName = "First Delivery Express",
            Frequency = CarrierRemittanceFrequency.Weekly,
            RemittanceDayOfWeek = DayOfWeek.Friday,
            ContractFeePerParcel = 6.5m,
            IsActive = true
        });

        await db.SaveChangesAsync();

        // Start from a Monday
        var startDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc); // Monday
        var forecast = await service.Generate14DayCashFlowForecastAsync(tenantId, startDate);

        Assert.NotEmpty(forecast);
        Assert.All(forecast, f =>
        {
            Assert.Equal("First Delivery Express", f.CourierName);
            Assert.Equal(DayOfWeek.Friday, f.ForecastDate.DayOfWeek);
        });

        // First Friday should be within 7 days, so confidence High
        Assert.Equal("High (Confirmed Deliveries)", forecast[0].Confidence);
        // Second Friday is in week 2, so confidence Medium
        if (forecast.Count > 1)
        {
            Assert.Equal("Medium (In-Transit Projection)", forecast[1].Confidence);
        }
    }

    [Fact]
    public async Task CalculateFloatValuationAsync_PersistsRecordInDatabase()
    {
        var db = CreateDbContext();
        var service = new TreasuryFloatService(db);
        var tenantId = Guid.NewGuid();

        var order = CodOrder.Create(tenantId, "ORD-VAL-1", "Customer", "+21620000000", "Address", "Sousse", "Sousse", 250.0m);
        order.Status = CodOrderStatus.Dispatched;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        var valuation = await service.CalculateFloatValuationAsync(tenantId);

        var persisted = await db.CourierFloatValuations.FirstOrDefaultAsync(v => v.Id == valuation.Id);
        Assert.NotNull(persisted);
        Assert.Equal(tenantId, persisted.TenantId);
        Assert.Equal(7.5m, persisted.AverageDaysSalesOutstanding);
        Assert.Equal(250.0m, persisted.InTransitCashValue);
        Assert.Equal(243.0m, persisted.NetReceivableFloatValue);
    }

    [Fact]
    public async Task Generate14DayCashFlowForecastAsync_WhenNoCarrierSchedulesConfigured_UsesDefaultYalidineTuesdaySchedule()
    {
        var db = CreateDbContext();
        var service = new TreasuryFloatService(db);
        var tenantId = Guid.NewGuid();

        // Empty carrier schedules in db
        var startDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc); // Monday
        var forecast = await service.Generate14DayCashFlowForecastAsync(tenantId, startDate);

        Assert.NotEmpty(forecast);
        Assert.All(forecast, f =>
        {
            Assert.Equal("Yalidine Express", f.CourierName);
            Assert.Equal(DayOfWeek.Tuesday, f.ForecastDate.DayOfWeek);
        });
    }

    [Fact]
    public async Task Generate14DayCashFlowForecastAsync_CalculatesNetAfterContractFeeDeduction()
    {
        var db = CreateDbContext();
        var service = new TreasuryFloatService(db);
        var tenantId = Guid.NewGuid();

        db.CarrierRemittanceScheduleConfigs.Add(new CarrierRemittanceScheduleConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = Guid.NewGuid(),
            CourierName = "SpeedEx Logistics",
            Frequency = CarrierRemittanceFrequency.Weekly,
            RemittanceDayOfWeek = DayOfWeek.Wednesday,
            ContractFeePerParcel = 10.0m,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var startDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc); // Monday
        var forecast = await service.Generate14DayCashFlowForecastAsync(tenantId, startDate);

        Assert.NotEmpty(forecast);
        Assert.All(forecast, f =>
        {
            Assert.Equal("SpeedEx Logistics", f.CourierName);
            var expectedNet = f.ExpectedGrossCash - (f.ExpectedParcelCount * 10.0m);
            Assert.Equal(expectedNet, f.ExpectedNetRemittance);
        });
    }
}
