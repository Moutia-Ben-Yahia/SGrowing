using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class SmartCourierRoutingTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"SmartRoutingTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task SmartRouting_Should_Rank_Carrier_With_Highest_Expected_Net_Margin()
    {
        var db = CreateDbContext();
        var service = new SmartCourierRoutingService(db);
        var tenantId = Guid.NewGuid();

        var carrierAId = Guid.NewGuid();
        var carrierBId = Guid.NewGuid();

        // 1. Setup Rate Cards
        // Carrier A: Base 7 DT, Return 3.5 DT
        db.CarrierRateCards.Add(new CarrierRateCard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = carrierAId,
            CourierName = "Yalidine Express",
            BaseFee = 7.0m,
            ExtraKgFee = 1.0m,
            RemoteZoneSurcharge = 2.0m,
            IsActive = true
        });

        // Carrier B: Base 8 DT, Return 4.0 DT
        db.CarrierRateCards.Add(new CarrierRateCard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = carrierBId,
            CourierName = "Aramex Logistics",
            BaseFee = 8.0m,
            ExtraKgFee = 1.0m,
            RemoteZoneSurcharge = 1.0m,
            IsActive = true
        });

        // 2. Setup Performance Metrics in Tunis
        // Yalidine has 95% delivery rate in Tunis
        db.CarrierPerformanceMetrics.Add(CarrierPerformanceMetric.Create(
            tenantId, carrierAId, "Yalidine Express", "Tunis", dispatched: 100, delivered: 95, returned: 5, returnFee: 3.5m));

        // Aramex has only 80% delivery rate in Tunis
        db.CarrierPerformanceMetrics.Add(CarrierPerformanceMetric.Create(
            tenantId, carrierBId, "Aramex Logistics", "Tunis", dispatched: 100, delivered: 80, returned: 20, returnFee: 4.0m));

        await db.SaveChangesAsync();

        // 3. Evaluate ranking for 100 TND parcel in Tunis
        var ranking = await service.RankCarriersForOrderAsync(tenantId, "Tunis", packageWeightKg: 1.0m, codAmount: 100.0m);

        Assert.Equal(2, ranking.Count);
        // Carrier A (Yalidine) should rank #1 because EV = (0.95 * 100) - 7 - (0.05 * 3.5) = 95 - 7 - 0.175 = 87.825
        // Carrier B EV = (0.80 * 100) - 8 - (0.20 * 4.0) = 80 - 8 - 0.8 = 71.2
        Assert.Equal("Yalidine Express", ranking.First().CourierName);
        Assert.True(ranking.First().ExpectedNetMargin > ranking.Last().ExpectedNetMargin);
        Assert.Equal(95.0m, ranking.First().HistoricalDeliveryRate);
    }

    [Fact]
    public async Task SlaBreach_Should_Fallback_To_Second_Best_Carrier_With_New_Tracking()
    {
        var db = CreateDbContext();
        var service = new SmartCourierRoutingService(db);
        var tenantId = Guid.NewGuid();

        var carrierAId = Guid.NewGuid();
        var carrierBId = Guid.NewGuid();

        db.CarrierRateCards.Add(new CarrierRateCard { Id = Guid.NewGuid(), TenantId = tenantId, CourierProviderId = carrierAId, CourierName = "Yalidine", BaseFee = 7.0m, IsActive = true });
        db.CarrierRateCards.Add(new CarrierRateCard { Id = Guid.NewGuid(), TenantId = tenantId, CourierProviderId = carrierBId, CourierName = "Aramex", BaseFee = 8.0m, IsActive = true });
        db.CarrierPerformanceMetrics.Add(CarrierPerformanceMetric.Create(tenantId, carrierAId, "Yalidine", "Sousse", 100, 90, 10));
        db.CarrierPerformanceMetrics.Add(CarrierPerformanceMetric.Create(tenantId, carrierBId, "Aramex", "Sousse", 100, 85, 15));

        var order = CodOrder.Create(tenantId, "ORD-ROUTED-01", "Walid Mejri", "+21622334455", "Sousse Ville", "Sousse", "Sousse", 120.0m);
        order.Notes = "Assigned Tracking #YAL-TN-BREACH-998877";
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // Simulate 24h pickup breach
        var fallbackOrder = await service.HandlePickupSlaBreachFallbackAsync(tenantId, "YAL-TN-BREACH-998877");

        Assert.Contains("SLA Breach on YAL-TN-BREACH-998877", fallbackOrder.Notes);
        Assert.Contains("Auto-Reassigned to Aramex", fallbackOrder.Notes);
        Assert.Contains("ARA-TN-FALLBACK", fallbackOrder.Notes);
    }
}
