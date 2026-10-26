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
}
