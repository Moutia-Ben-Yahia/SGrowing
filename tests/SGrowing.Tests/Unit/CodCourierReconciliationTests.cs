using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce.Logistics.Reconciliation;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Unit;

public class CodCourierReconciliationTests
{
    private static SGrowingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task IngestRemittanceBatch_WithMatchedDeliveredOrders_FlagsAsMatchedWithZeroDiscrepancy()
    {
        using var db = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();
        var service = new CodCourierReconciliationService(db);

        // Seed internal merchant COD orders
        db.CodOrders.Add(new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-KSA-101",
            TotalAmount = 450.00m,
            CustomerName = "Faisal Al-Otaibi",
            PhoneNumber = "+966501234567"
        });
        await db.SaveChangesAsync();

        var request = new IngestCourierRemittanceRequest(
            TenantId: tenantId,
            BatchReference: "SMSA-BATCH-001",
            Courier: CourierCode.SMSA,
            RemittanceItems: new List<RemittanceItemInput>
            {
                new(
                    TrackingNumber: "SMSA-110022",
                    OrderNumber: "ORD-KSA-101",
                    RemittedAmountSar: 450.00m,
                    CourierFeeSar: 28.00m,
                    IsDelivered: true,
                    IsReturned: false
                )
            }
        );

        var batch = await service.IngestRemittanceBatchAsync(request);

        batch.Should().NotBeNull();
        batch.TenantId.Should().Be(tenantId);
        batch.TotalParcels.Should().Be(1);
        batch.TotalExpectedCodSar.Should().Be(450.00m);
        batch.TotalRemittedCodSar.Should().Be(450.00m);
        batch.NetDiscrepancySar.Should().Be(0.00m);

        var item = batch.Items.Single();
        item.Status.Should().Be(RemittanceMatchStatus.Matched);
        item.DiscrepancyAmountSar.Should().Be(0.00m);
        item.DiscrepancyReason.Should().BeNull();
    }

    [Fact]
    public async Task IngestRemittanceBatch_WithUnderpaidOrders_DetectsShortfallDiscrepancy()
    {
        using var db = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();
        var service = new CodCourierReconciliationService(db);

        db.CodOrders.Add(new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-KSA-102",
            TotalAmount = 600.00m,
            CustomerName = "Noura Al-Dosari",
            PhoneNumber = "+966509876543"
        });
        await db.SaveChangesAsync();

        var request = new IngestCourierRemittanceRequest(
            TenantId: tenantId,
            BatchReference: "ARX-BATCH-002",
            Courier: CourierCode.Aramex,
            RemittanceItems: new List<RemittanceItemInput>
            {
                new(
                    TrackingNumber: "ARX-998877",
                    OrderNumber: "ORD-KSA-102",
                    RemittedAmountSar: 500.00m, // 100 SAR shortfall
                    CourierFeeSar: 30.00m,
                    IsDelivered: true,
                    IsReturned: false
                )
            }
        );

        var batch = await service.IngestRemittanceBatchAsync(request);

        batch.NetDiscrepancySar.Should().Be(100.00m);
        var item = batch.Items.Single();
        item.Status.Should().Be(RemittanceMatchStatus.Underpaid);
        item.DiscrepancyAmountSar.Should().Be(100.00m);
        item.DiscrepancyReason.Should().Contain("Carrier underpaid");
    }

    [Fact]
    public async Task IngestRemittanceBatch_WithOverpaidOrders_DetectsExcessDiscrepancy()
    {
        using var db = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();
        var service = new CodCourierReconciliationService(db);

        db.CodOrders.Add(new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-KSA-103",
            TotalAmount = 300.00m,
            CustomerName = "Tariq Mansour",
            PhoneNumber = "+966555123456"
        });
        await db.SaveChangesAsync();

        var request = new IngestCourierRemittanceRequest(
            TenantId: tenantId,
            BatchReference: "SPL-BATCH-003",
            Courier: CourierCode.SPL,
            RemittanceItems: new List<RemittanceItemInput>
            {
                new(
                    TrackingNumber: "SPL-554433",
                    OrderNumber: "ORD-KSA-103",
                    RemittedAmountSar: 340.00m, // 40 SAR overpaid
                    CourierFeeSar: 22.00m,
                    IsDelivered: true,
                    IsReturned: false
                )
            }
        );

        var batch = await service.IngestRemittanceBatchAsync(request);

        var item = batch.Items.Single();
        item.Status.Should().Be(RemittanceMatchStatus.Overpaid);
        item.DiscrepancyAmountSar.Should().Be(40.00m);
        item.DiscrepancyReason.Should().Contain("Carrier overpaid");
    }

    [Fact]
    public async Task IngestRemittanceBatch_WithLostShipments_FlagsAsLostInTransitWithFullValueDiscrepancy()
    {
        using var db = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();
        var service = new CodCourierReconciliationService(db);

        db.CodOrders.Add(new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-KSA-104",
            TotalAmount = 550.00m,
            CustomerName = "Zaid Al-Harbi",
            PhoneNumber = "+966551122334"
        });
        await db.SaveChangesAsync();

        var request = new IngestCourierRemittanceRequest(
            TenantId: tenantId,
            BatchReference: "SMSA-BATCH-004",
            Courier: CourierCode.SMSA,
            RemittanceItems: new List<RemittanceItemInput>
            {
                new(
                    TrackingNumber: "SMSA-LOST-01",
                    OrderNumber: "ORD-KSA-104",
                    RemittedAmountSar: 0.00m,
                    CourierFeeSar: 0.00m,
                    IsDelivered: false,
                    IsReturned: false
                )
            }
        );

        var batch = await service.IngestRemittanceBatchAsync(request);

        var item = batch.Items.Single();
        item.Status.Should().Be(RemittanceMatchStatus.LostInTransit);
        item.ExpectedCodAmountSar.Should().Be(550.00m);
        item.DiscrepancyAmountSar.Should().Be(550.00m);
        item.DiscrepancyReason.Should().Contain("lost in transit");
    }

    [Fact]
    public async Task IngestRemittanceBatch_WithReturnedShipments_DisputesUnauthorizedRtoFee()
    {
        using var db = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();
        var service = new CodCourierReconciliationService(db);

        db.CodOrders.Add(new CodOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-KSA-105",
            TotalAmount = 400.00m,
            CustomerName = "Amal Al-Subaie",
            PhoneNumber = "+966567891234"
        });
        await db.SaveChangesAsync();

        var request = new IngestCourierRemittanceRequest(
            TenantId: tenantId,
            BatchReference: "JT-BATCH-005",
            Courier: CourierCode.JTExpress,
            RemittanceItems: new List<RemittanceItemInput>
            {
                new(
                    TrackingNumber: "JT-RET-99",
                    OrderNumber: "ORD-KSA-105",
                    RemittedAmountSar: 0.00m,
                    CourierFeeSar: 35.00m, // Disputed RTO fee
                    IsDelivered: false,
                    IsReturned: true
                )
            }
        );

        var batch = await service.IngestRemittanceBatchAsync(request);

        var item = batch.Items.Single();
        item.Status.Should().Be(RemittanceMatchStatus.RtoFeeDisputed);
        item.DiscrepancyAmountSar.Should().Be(35.00m);
        item.DiscrepancyReason.Should().Contain("RTO");
    }

    [Fact]
    public async Task IngestRemittanceBatch_WithUnknownOrder_FlagsAsUnmatchedWaybill()
    {
        using var db = CreateInMemoryDbContext();
        var tenantId = Guid.NewGuid();
        var service = new CodCourierReconciliationService(db);

        var request = new IngestCourierRemittanceRequest(
            TenantId: tenantId,
            BatchReference: "SPL-BATCH-006",
            Courier: CourierCode.SPL,
            RemittanceItems: new List<RemittanceItemInput>
            {
                new(
                    TrackingNumber: "SPL-UNMATCHED-88",
                    OrderNumber: "UNKNOWN-ORD-9999",
                    RemittedAmountSar: 250.00m,
                    CourierFeeSar: 22.00m,
                    IsDelivered: true,
                    IsReturned: false
                )
            }
        );

        var batch = await service.IngestRemittanceBatchAsync(request);

        var item = batch.Items.Single();
        item.Status.Should().Be(RemittanceMatchStatus.UnmatchedWaybill);
        item.DiscrepancyAmountSar.Should().Be(250.00m);
        item.DiscrepancyReason.Should().Contain("not recognized");
    }

    [Fact]
    public async Task FileDispute_WithValidItem_CreatesAndTracksOpenDispute()
    {
        var tenantId = Guid.NewGuid();
        var service = new CodCourierReconciliationService();

        service.RegisterExpectedOrder(tenantId, "ORD-DISPUTE-1", 700.00m);

        var batch = await service.IngestRemittanceBatchAsync(new IngestCourierRemittanceRequest(
            TenantId: tenantId,
            BatchReference: "BATCH-DISPUTE-TEST",
            Courier: CourierCode.SMSA,
            RemittanceItems: new List<RemittanceItemInput>
            {
                new("SMSA-TRK-771", "ORD-DISPUTE-1", 500.00m, 28.00m, true, false)
            }
        ));

        var dispute = await service.FileDisputeAsync(
            tenantId,
            batch.BatchId,
            "SMSA-TRK-771",
            "Discrepancy of 200 SAR short-collection by SMSA courier driver."
        );

        dispute.Should().NotBeNull();
        dispute.TenantId.Should().Be(tenantId);
        dispute.BatchId.Should().Be(batch.BatchId);
        dispute.TrackingNumber.Should().Be("SMSA-TRK-771");
        dispute.Courier.Should().Be(CourierCode.SMSA);
        dispute.DisputedAmountSar.Should().Be(200.00m);
        dispute.Status.Should().Be("Open");

        var allDisputes = await service.GetDisputesAsync(tenantId);
        allDisputes.Should().Contain(d => d.DisputeId == dispute.DisputeId);
    }

    [Fact]
    public async Task GetSummaryReport_AggregatesKpisAndCourierPerformanceAcrossCarriers()
    {
        var tenantId = Guid.NewGuid();
        var service = new CodCourierReconciliationService();

        // Seed tenant demo batches load automatically
        var report = await service.GetSummaryReportAsync(tenantId);

        report.Should().NotBeNull();
        report.TotalBatchesProcessed.Should().BeGreaterThanOrEqualTo(3);
        report.TotalParcelsReconciled.Should().BeGreaterThan(0);
        report.ReconciledRatePercent.Should().BeGreaterThan(0);
        report.CourierPerformance.Should().NotBeEmpty();

        var carrierNames = report.CourierPerformance.Select(c => c.CourierName).ToList();
        carrierNames.Should().Contain("SMSA Express");
        carrierNames.Should().Contain("Aramex");
        carrierNames.Should().Contain("SPL (Saudi Post)");

        report.DiscrepancyStatusBreakdown.Should().ContainKey(nameof(RemittanceMatchStatus.Matched));
    }

    [Fact]
    public async Task TenantIsolation_MaintainsIsolatedBatchesAndDisputesBetweenTenants()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var service = new CodCourierReconciliationService();

        service.RegisterExpectedOrder(tenantA, "ORD-A-1", 300.00m);
        service.RegisterExpectedOrder(tenantB, "ORD-B-1", 600.00m);

        var batchA = await service.IngestRemittanceBatchAsync(new IngestCourierRemittanceRequest(
            TenantId: tenantA,
            BatchReference: "BATCH-TENANT-A",
            Courier: CourierCode.SMSA,
            RemittanceItems: new List<RemittanceItemInput>
            {
                new("TRK-A-1", "ORD-A-1", 300.00m, 28.00m, true, false)
            }
        ));

        var batchB = await service.IngestRemittanceBatchAsync(new IngestCourierRemittanceRequest(
            TenantId: tenantB,
            BatchReference: "BATCH-TENANT-B",
            Courier: CourierCode.Aramex,
            RemittanceItems: new List<RemittanceItemInput>
            {
                new("TRK-B-1", "ORD-B-1", 600.00m, 32.00m, true, false)
            }
        ));

        var batchesA = await service.GetBatchesAsync(tenantA);
        var batchesB = await service.GetBatchesAsync(tenantB);

        batchesA.Should().Contain(b => b.BatchId == batchA.BatchId);
        batchesA.Should().NotContain(b => b.BatchId == batchB.BatchId);

        batchesB.Should().Contain(b => b.BatchId == batchB.BatchId);
        batchesB.Should().NotContain(b => b.BatchId == batchA.BatchId);

        var retrievedBatch = await service.GetBatchAsync(tenantA, batchB.BatchId);
        retrievedBatch.Should().BeNull();
    }
}
