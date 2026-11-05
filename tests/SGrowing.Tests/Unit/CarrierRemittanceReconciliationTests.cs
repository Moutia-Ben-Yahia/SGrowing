using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Gateways;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Unit;

public class CarrierRemittanceReconciliationTests
{
    private static SGrowingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task ReconcileStatementAsync_WithBalancedItems_ReturnsBalancedReport()
    {
        using var db = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();

        // Seed order
        db.CodOrders.Add(new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-101",
            TotalAmount = 4500m,
            CustomerName = "Karim Benali",
            PhoneNumber = "+213555112233"
        });
        await db.SaveChangesAsync();

        var service = new CarrierRemittanceReconciliationService(db);

        var statement = new CarrierRemittanceStatement(
            StatementId: "STMT-2026-001",
            CourierProvider: "Yalidine",
            StatementReference: "BORD-9912",
            StatementDateUtc: DateTime.UtcNow,
            DeclaredGrossAmount: 4500m,
            DeclaredFeesAmount: 750m,
            DeclaredNetAmount: 3750m,
            Items: new List<RemittanceStatementLineItem>
            {
                new("YAL-001", "ORD-101", 4500m, 4500m, 750m, 0m, "Delivered", DateTime.UtcNow)
            }
        );

        var report = await service.ReconcileStatementAsync(tenantId, statement);

        Assert.Equal("STMT-2026-001", report.StatementId);
        Assert.Equal("Balanced", report.SettlementStatus);
        Assert.Equal(4500m, report.TotalExpectedCod);
        Assert.Equal(4500m, report.TotalCollectedCod);
        Assert.Equal(750m, report.TotalShippingFees);
        Assert.Equal(3750m, report.NetSettlementPayable);
        Assert.Equal(0, report.DiscrepancyCount);
        Assert.Empty(report.Discrepancies);
    }

    [Fact]
    public async Task ReconcileStatementAsync_DetectsUndercollectedAndFeeMismatches()
    {
        using var db = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();

        db.CodOrders.Add(new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-202",
            TotalAmount = 6000m,
            CustomerName = "Amine Ziani",
            PhoneNumber = "+213777889900"
        });
        await db.SaveChangesAsync();

        var service = new CarrierRemittanceReconciliationService(db);

        var statement = new CarrierRemittanceStatement(
            StatementId: "STMT-2026-002",
            CourierProvider: "ZRExpress",
            StatementReference: "BORD-9913",
            StatementDateUtc: DateTime.UtcNow,
            DeclaredGrossAmount: 5000m,
            DeclaredFeesAmount: 1600m,
            DeclaredNetAmount: 3400m,
            Items: new List<RemittanceStatementLineItem>
            {
                // Courier collected 5000 instead of 6000, and charged 1600 instead of standard 800 fee
                new("ZR-001", "ORD-202", 6000m, 5000m, 1600m, 0m, "Delivered", DateTime.UtcNow)
            }
        );

        var report = await service.ReconcileStatementAsync(tenantId, statement);

        Assert.Equal("DiscrepancyDetected", report.SettlementStatus);
        Assert.Equal(2, report.DiscrepancyCount);

        var undercollected = report.Discrepancies.FirstOrDefault(d => d.DiscrepancyType == "Undercollected");
        Assert.NotNull(undercollected);
        Assert.Equal(-1000m, undercollected.Variance);

        var feeMismatch = report.Discrepancies.FirstOrDefault(d => d.DiscrepancyType == "FeeMismatch");
        Assert.NotNull(feeMismatch);
        Assert.Equal(800m, feeMismatch.Variance);
    }

    [Fact]
    public async Task AutoResolveDiscrepancyAsync_ResolvesSpecificItemInHistory()
    {
        using var db = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();

        var service = new CarrierRemittanceReconciliationService(db);

        var statement = new CarrierRemittanceStatement(
            StatementId: "STMT-2026-003",
            CourierProvider: "Procolis",
            StatementReference: "BORD-9914",
            StatementDateUtc: DateTime.UtcNow,
            DeclaredGrossAmount: 3000m,
            DeclaredFeesAmount: 700m,
            DeclaredNetAmount: 2300m,
            Items: new List<RemittanceStatementLineItem>
            {
                new("PRO-001", "ORD-UNKNOWN", 0m, 3000m, 700m, 0m, "Delivered", DateTime.UtcNow)
            }
        );

        await service.ReconcileStatementAsync(tenantId, statement);

        var history = await service.GetReconciliationHistoryAsync(tenantId);
        Assert.NotEmpty(history);

        var resolved = await service.AutoResolveDiscrepancyAsync(
            tenantId,
            "STMT-2026-003",
            "PRO-001",
            "Customer verified and matched to ERP internal order");

        Assert.True(resolved);
    }
}
