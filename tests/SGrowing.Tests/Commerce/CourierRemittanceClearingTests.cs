using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CourierRemittanceClearingTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"RemittanceTestDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ReconcileStatement_Should_Detect_ExactMatch_ShortPayment_And_Unaccounted_Parcels()
    {
        var db = CreateDbContext();
        var clearing = new CourierRemittanceClearingService(db);
        var tenantId = Guid.NewGuid();

        var courier = new CourierProvider
        {
            TenantId = tenantId,
            Name = "Yalidine Algeria",
            ApiCode = "YAL",
            SupportedCountries = "DZ",
            IsActive = true
        };
        db.CourierProviders.Add(courier);

        // System shipment 1: Expected 4500 DZD
        var shipment1 = new ShipmentRecord
        {
            TenantId = tenantId,
            CourierProviderId = courier.Id,
            TrackingNumber = "YAL-ALG-001",
            CashToCollect = 4500m,
            CashRemitted = 0,
            Status = CourierShipmentStatus.InTransit
        };

        // System shipment 2: Expected 6000 DZD
        var shipment2 = new ShipmentRecord
        {
            TenantId = tenantId,
            CourierProviderId = courier.Id,
            TrackingNumber = "YAL-ALG-002",
            CashToCollect = 6000m,
            CashRemitted = 0,
            Status = CourierShipmentStatus.InTransit
        };

        db.ShipmentRecords.AddRange(shipment1, shipment2);
        await db.SaveChangesAsync();

        // Courier reports statement with 3 items:
        // Item 1: Exact match (4500 DZD collected, 600 DZD fee)
        // Item 2: Short-payment (5500 DZD collected instead of 6000 DZD, 600 DZD fee)
        // Item 3: Unaccounted parcel (YAL-ALG-999 with 3000 DZD collected)
        var reportedItems = new List<ReportedRemittanceItemDto>
        {
            new ReportedRemittanceItemDto("YAL-ALG-001", "Delivered", 4500m, 600m),
            new ReportedRemittanceItemDto("YAL-ALG-002", "Delivered", 5500m, 600m),
            new ReportedRemittanceItemDto("YAL-ALG-999", "Delivered", 3000m, 600m)
        };

        var result = await clearing.ReconcileStatementAsync(tenantId, courier.Id, "BVR-YAL-2026-W43", reportedItems);

        Assert.Equal(3, result.Statement.TotalParcelsReported);
        Assert.Equal(13000m, result.Statement.TotalCashCollected);
        Assert.Equal(1800m, result.Statement.TotalCourierFeesDeducted);
        Assert.Equal(11200m, result.Statement.NetRemittedAmount);
        Assert.Equal(RemittanceStatus.DiscrepancyFlagged, result.Statement.Status);

        // Match 1: Exact
        var match1 = result.Matches.First(m => m.TrackingNumber == "YAL-ALG-001");
        Assert.Equal(RemittanceDiscrepancyType.ExactMatch, match1.DiscrepancyType);
        Assert.True(match1.IsResolved);

        // Match 2: Short Payment
        var match2 = result.Matches.First(m => m.TrackingNumber == "YAL-ALG-002");
        Assert.Equal(RemittanceDiscrepancyType.ShortPayment, match2.DiscrepancyType);
        Assert.Equal(-500m, match2.DifferenceAmount);
        Assert.False(match2.IsResolved);

        // Match 3: Unaccounted
        var match3 = result.Matches.First(m => m.TrackingNumber == "YAL-ALG-999");
        Assert.Equal(RemittanceDiscrepancyType.UnaccountedParcel, match3.DiscrepancyType);
        Assert.False(match3.IsResolved);

        // Resolve discrepancy
        var resolvedMatch2 = await clearing.ResolveDiscrepancyAsync(tenantId, match2.Id, "Courier acknowledged mistake, 500 DZD added to next transfer");
        Assert.True(resolvedMatch2.IsResolved);
    }

    [Fact]
    public async Task CalculateNetProfitability_Should_Compute_Realtime_Ledger()
    {
        var db = CreateDbContext();
        var clearing = new CourierRemittanceClearingService(db);
        var tenantId = Guid.NewGuid();

        // Remittance statement: 10,000 cash collected, 1,200 courier fees
        db.CourierRemittanceStatements.Add(new CourierRemittanceStatement
        {
            TenantId = tenantId,
            StatementReference = "STMT-01",
            StatementDateUtc = DateTime.UtcNow,
            TotalCashCollected = 10000m,
            TotalCourierFeesDeducted = 1200m,
            NetRemittedAmount = 8800m
        });

        // Ad spend touchpoint: 2,500 cost
        db.AttributionTouchpoints.Add(new AttributionTouchpoint
        {
            TenantId = tenantId,
            Channel = "MetaAds",
            Cost = 2500m,
            OccurredAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var profit = await clearing.CalculateNetProfitabilityAsync(tenantId, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(1), estimatedCogsPercent: 30.0m);

        Assert.Equal(10000m, profit.GrossRevenueCollected);
        Assert.Equal(3000m, profit.TotalCogs); // 30% of 10000
        Assert.Equal(2500m, profit.TotalAdSpend);
        Assert.Equal(1200m, profit.TotalCourierFees);
        // Net Profit = 10000 - 3000 - 2500 - 1200 = 3300
        Assert.Equal(3300m, profit.NetProfit);
        Assert.Equal(33.0, profit.NetProfitMarginPercent);
    }
}
