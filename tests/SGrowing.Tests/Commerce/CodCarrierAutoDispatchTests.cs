using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Commerce;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class CodCarrierAutoDispatchTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"CodCarrierDispatchDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    #region 1. Auto-Routing Matrix & Expected Net Value Scoring

    [Fact]
    public async Task ExpectedNetValue_Formula_Should_Rank_Carrier_With_Highest_Expected_Net_Value()
    {
        // Formula: E[Value(c, g)] = P_delivered(c, g) * CodAmount - Fee_delivery(c) - (1 - P_delivered(c, g)) * Fee_return(c)
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        var carrierAId = Guid.NewGuid();
        var carrierBId = Guid.NewGuid();

        // Courier A: First Delivery (Higher delivery rate in Tunis, lower fee)
        // Rate: 95%, Base delivery fee: 6.50 TND, Return fee: 3.00 TND
        // For COD = 100 TND:
        // E[Value] = (0.95 * 100) - 6.50 - (0.05 * 3.00) = 95 - 6.50 - 0.15 = 88.35 TND
        db.CourierProviders.Add(new CourierProvider
        {
            Id = carrierAId,
            TenantId = tenantId,
            Name = "First Delivery Express",
            ApiCode = "FST",
            SupportedCountries = "TN",
            IsActive = true
        });
        db.CarrierRateCards.Add(new CarrierRateCard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = carrierAId,
            CourierName = "First Delivery Express",
            BaseFee = 6.50m,
            IsActive = true
        });
        db.CarrierPerformanceMetrics.Add(CarrierPerformanceMetric.Create(
            tenantId, carrierAId, "First Delivery Express", "Tunis",
            dispatched: 100, delivered: 95, returned: 5, avgTransitDays: 1.0m, returnFee: 3.00m));

        // Courier B: Aramex Tunisia (Lower delivery rate in Tunis, higher fee)
        // Rate: 85%, Base delivery fee: 8.00 TND, Return fee: 4.00 TND
        // For COD = 100 TND:
        // E[Value] = (0.85 * 100) - 8.00 - (0.15 * 4.00) = 85 - 8.00 - 0.60 = 76.40 TND
        db.CourierProviders.Add(new CourierProvider
        {
            Id = carrierBId,
            TenantId = tenantId,
            Name = "Aramex Tunisia",
            ApiCode = "ARX",
            SupportedCountries = "TN",
            IsActive = true
        });
        db.CarrierRateCards.Add(new CarrierRateCard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = carrierBId,
            CourierName = "Aramex Tunisia",
            BaseFee = 8.00m,
            IsActive = true
        });
        db.CarrierPerformanceMetrics.Add(CarrierPerformanceMetric.Create(
            tenantId, carrierBId, "Aramex Tunisia", "Tunis",
            dispatched: 100, delivered: 85, returned: 15, avgTransitDays: 2.0m, returnFee: 4.00m));

        await db.SaveChangesAsync();

        // Act
        var ranked = await service.EvaluateCarrierRankingAsync(tenantId, "Tunis", 100.0m, "TN");

        // Assert
        Assert.NotNull(ranked);
        Assert.NotEmpty(ranked);

        var top = ranked.First();
        Assert.Equal("First Delivery Express", top.CourierName);
        Assert.Equal(88.35m, top.ExpectedNetValue);
        Assert.Equal(95.0m, top.HistoricalDeliveryRatePercent);
        Assert.Equal(6.50m, top.BaseDeliveryFee);
        Assert.Equal(3.00m, top.ReturnFeePenalty);

        var second = ranked.FirstOrDefault(r => r.CourierProviderId == carrierBId);
        Assert.NotNull(second);
        Assert.Equal(76.40m, second.ExpectedNetValue);
        Assert.True(top.ExpectedNetValue > second.ExpectedNetValue);
    }

    [Fact]
    public async Task AutoRouteAndAssignOrder_Should_Assign_Optimal_Carrier_And_Persist_Decision()
    {
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        var carrierId = Guid.NewGuid();
        db.CourierProviders.Add(new CourierProvider
        {
            Id = carrierId,
            TenantId = tenantId,
            Name = "Livri Express Sahel",
            ApiCode = "LVR",
            SupportedCountries = "TN",
            IsActive = true
        });
        db.CarrierRateCards.Add(new CarrierRateCard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierProviderId = carrierId,
            CourierName = "Livri Express Sahel",
            BaseFee = 7.00m,
            IsActive = true
        });
        db.CarrierPerformanceMetrics.Add(CarrierPerformanceMetric.Create(
            tenantId, carrierId, "Livri Express Sahel", "Sousse",
            dispatched: 200, delivered: 190, returned: 10, avgTransitDays: 1.2m, returnFee: 3.50m));

        var order = CodOrder.Create(
            tenantId: tenantId,
            orderNumber: "MLT-TN-9901",
            customerName: "Anis Ben Amor",
            phoneNumber: "+21698765432",
            shippingAddress: "Boulevard 14 Janvier, Kantaoui",
            governorateOrWilaya: "Sousse",
            delegationOrCommune: "Hammam Sousse",
            totalAmount: 120.0m);
        order.Status = CodOrderStatus.Confirmed;
        db.CodOrders.Add(order);
        await db.SaveChangesAsync();

        // Act
        var result = await service.AutoRouteAndAssignOrderAsync(tenantId, order.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal("Livri Express Sahel", result.SelectedCourierName);
        Assert.Equal(7.00m, result.AssignedShippingFee);
        Assert.True(result.ExpectedNetValue > 0);

        // Check persisted order changes
        var updatedOrder = await db.CodOrders.FindAsync(order.Id);
        Assert.NotNull(updatedOrder);
        Assert.Equal(7.00m, updatedOrder.ShippingFee);
        Assert.Contains("Auto-Dispatched: Livri Express Sahel", updatedOrder.Notes);
    }

    [Fact]
    public async Task GetCarrierRoutingMatrix_Should_Cover_Tunisian_Governorates()
    {
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        // Act
        var matrix = await service.GetCarrierRoutingMatrixAsync(tenantId, "TN");

        // Assert
        Assert.NotNull(matrix);
        Assert.Equal(24, matrix.Count); // Full 24 Tunisian Gouvernorats
        Assert.Contains(matrix, g => g.Governorate == "Tunis");
        Assert.Contains(matrix, g => g.Governorate == "Sfax");
        Assert.Contains(matrix, g => g.Governorate == "Sousse");
        Assert.Contains(matrix, g => g.Governorate == "Bizerte");
        Assert.Contains(matrix, g => g.Governorate == "Tataouine");

        foreach (var entry in matrix)
        {
            Assert.NotEmpty(entry.RankedCarriers);
            Assert.True(entry.RankedCarriers.First().ExpectedNetValue > 0);
        }
    }

    #endregion

    #region 2. Shipping Manifest Generation

    [Fact]
    public async Task GenerateManifestBatch_Should_Lock_Orders_And_Generate_Valid_MNF_And_AWBs()
    {
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        var courier = new CourierProvider
        {
            TenantId = tenantId,
            Name = "Aramex Express",
            ApiCode = "ARX",
            SupportedCountries = "TN",
            IsActive = true
        };
        db.CourierProviders.Add(courier);

        var order1 = CodOrder.Create(tenantId, "ORD-001", "Mohamed Trabelsi", "+21620123456", "Rue Habib Bourguiba", "Tunis", "La Marsa", 75.0m, shippingFee: 7.0m);
        var order2 = CodOrder.Create(tenantId, "ORD-002", "Sarra Mansour", "+21622345678", "Avenue de l'Independance", "Tunis", "Carthage", 120.0m, shippingFee: 7.0m);
        var order3 = CodOrder.Create(tenantId, "ORD-003", "Youssef Gharbi", "+21698765432", "Cité Ennasr 2", "Ariana", "Ennasr", 95.0m, shippingFee: 7.0m);

        order1.Status = CodOrderStatus.Confirmed;
        order2.Status = CodOrderStatus.Confirmed;
        order3.Status = CodOrderStatus.Confirmed;

        db.CodOrders.AddRange(order1, order2, order3);
        await db.SaveChangesAsync();

        var orderIds = new List<Guid> { order1.Id, order2.Id, order3.Id };

        // Act
        var manifestResult = await service.GenerateManifestBatchAsync(tenantId, courier.Id, orderIds, "LogisticsLead");

        // Assert
        Assert.NotNull(manifestResult);
        Assert.Matches(@"^MNF-\d{8}-[A-Z0-9]{5}$", manifestResult.ManifestNumber);
        Assert.Equal("PendingPickup", manifestResult.Status);
        Assert.Equal(3, manifestResult.TotalParcels);
        Assert.Equal(290.0m, manifestResult.TotalDeclaredCodAmount); // 75 + 120 + 95
        Assert.Equal(21.0m, manifestResult.ExpectedFreightCost); // 7 * 3
        Assert.Equal($"*{manifestResult.ManifestNumber}*", manifestResult.BarcodeString);
        Assert.Equal(3, manifestResult.Parcels.Count);

        // Verify sequential AWBs and barcodes
        foreach (var p in manifestResult.Parcels)
        {
            Assert.Matches(@"^AWB-ARX-\d{8}-\d{4}$", p.TrackingNumber);
            Assert.Equal($"*{p.TrackingNumber}*", p.AwbBarcode);
        }

        // Verify orders are locked into Dispatched status
        var updatedOrders = await db.CodOrders.Where(o => orderIds.Contains(o.Id)).ToListAsync();
        Assert.All(updatedOrders, o => Assert.Equal(CodOrderStatus.Dispatched, o.Status));
        Assert.All(updatedOrders, o => Assert.Contains(manifestResult.ManifestNumber, o.Notes));

        // Verify ShipmentRecords exist in DB
        var shipments = await db.ShipmentRecords.Where(s => orderIds.Contains(s.OrderId)).ToListAsync();
        Assert.Equal(3, shipments.Count);
        Assert.All(shipments, s => Assert.Equal(CourierShipmentStatus.Created, s.Status));

        // Verify ListManifestsAsync
        var manifestsList = await service.ListManifestsAsync(tenantId);
        Assert.Single(manifestsList);
        Assert.Equal(manifestResult.ManifestNumber, manifestsList[0].ManifestNumber);
    }

    [Fact]
    public async Task GenerateManifestBatch_Empty_OrderIds_Should_Throw_ArgumentException()
    {
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateManifestBatchAsync(tenantId, Guid.NewGuid(), new List<Guid>()));
    }

    #endregion

    #region 3. Cash Remittance Statement CSV Reconciler & Phantom Return Detection

    [Fact]
    public async Task ReconcileRemittance_Should_Categorize_FullyCleared_And_ShortCollection()
    {
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        var courier = new CourierProvider
        {
            TenantId = tenantId,
            Name = "Livri Express",
            ApiCode = "LVR",
            SupportedCountries = "TN",
            IsActive = true
        };
        db.CourierProviders.Add(courier);

        // Pre-create shipment 1: Expected 90.00 TND
        var order1 = CodOrder.Create(tenantId, "ORD-101", "Amine Khelil", "+21650111222", "Tunis", "Tunis", "El Menzah", 90.0m);
        db.CodOrders.Add(order1);
        var shipment1 = new ShipmentRecord
        {
            TenantId = tenantId,
            OrderId = order1.Id,
            CourierProviderId = courier.Id,
            TrackingNumber = "AWB-LVR-20261029-0001",
            CashToCollect = 90.0m,
            Status = CourierShipmentStatus.InTransit
        };
        db.ShipmentRecords.Add(shipment1);

        // Pre-create shipment 2: Expected 150.00 TND, Courier collects only 120.00 TND (Short payment discrepancy)
        var order2 = CodOrder.Create(tenantId, "ORD-102", "Meriem Chaari", "+21698333444", "Sfax", "Sfax", "Sakiet Ezzit", 150.0m);
        db.CodOrders.Add(order2);
        var shipment2 = new ShipmentRecord
        {
            TenantId = tenantId,
            OrderId = order2.Id,
            CourierProviderId = courier.Id,
            TrackingNumber = "AWB-LVR-20261029-0002",
            CashToCollect = 150.0m,
            Status = CourierShipmentStatus.InTransit
        };
        db.ShipmentRecords.Add(shipment2);

        await db.SaveChangesAsync();

        // Sample CSV Statement from Livri Express
        var csvStatement = @"TrackingNumber,ReportedStatus,CashCollected,CourierFee
AWB-LVR-20261029-0001,Delivered,90.00,7.00
AWB-LVR-20261029-0002,Delivered,120.00,7.00";

        // Act
        var audit = await service.ReconcileRemittanceStatementAsync(
            tenantId, courier.Id, "STM-LVR-2026-W44", csvStatement, reconciledBy: "FinanceOfficer");

        // Assert
        Assert.NotNull(audit);
        Assert.Equal(2, audit.TotalParcelsProcessed);
        Assert.Equal(210.00m, audit.TotalCollectedCash); // 90 + 120
        Assert.Equal(1, audit.ClearedCount);
        Assert.Equal(1, audit.DiscrepancyCount);

        // Match 1: Fully Cleared
        var match1 = audit.MatchDetails.First(m => m.TrackingNumber == "AWB-LVR-20261029-0001");
        Assert.True(match1.IsResolved);
        Assert.Equal("ExactMatch", match1.DiscrepancyType);
        Assert.Equal(0m, match1.CashVariance);

        // Match 2: Short Payment Discrepancy
        var match2 = audit.MatchDetails.First(m => m.TrackingNumber == "AWB-LVR-20261029-0002");
        Assert.False(match2.IsResolved);
        Assert.Equal("ShortPayment", match2.DiscrepancyType);
        Assert.Equal(-30.00m, match2.CashVariance);
        Assert.Equal(30.00m, match2.HoldbackAmount);
        Assert.NotNull(match2.DisputeTicketNumber);

        // Dispute tickets
        Assert.Single(audit.DisputeTickets);
        Assert.Equal(30.00m, audit.DisputeTickets[0].DisputedAmount);

        // Net Cash Settlement formula verification:
        // TotalCollected (210) - ApprovedDeliveryFees (7.00) - ReturnPenalties (0) - DiscrepancyHoldback (30.00) = 173.00 TND
        Assert.Equal(7.00m, audit.ApprovedDeliveryFees);
        Assert.Equal(30.00m, audit.DiscrepancyHoldback);
        Assert.Equal(173.00m, audit.NetCashSettlement);
    }

    [Fact]
    public async Task ReconcileRemittance_Should_Detect_Phantom_Return_And_Generate_Dispute()
    {
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        var courier = new CourierProvider
        {
            TenantId = tenantId,
            Name = "First Delivery",
            ApiCode = "FST",
            SupportedCountries = "TN",
            IsActive = true
        };
        db.CourierProviders.Add(courier);

        // Merchant tele-operator confirmed order with customer
        var order = CodOrder.Create(tenantId, "ORD-PHANTOM-1", "Hamza Bouazizi", "+21625999888", "Bizerte", "Bizerte", "Zarzouna", 110.0m);
        order.Status = CodOrderStatus.Confirmed;
        order.ConfirmedAtUtc = DateTime.UtcNow.AddDays(-2);
        db.CodOrders.Add(order);

        // Call log: Agent verified with customer, buyer was eagerly waiting for package
        db.CallOutcomeRecords.Add(new CallOutcomeRecord
        {
            TenantId = tenantId,
            OrderId = order.Id,
            AgentId = "agent_01",
            AgentName = "Sarah",
            Outcome = CallOutcome.Confirmed,
            Notes = "Customer confirmed order, waiting for delivery tomorrow afternoon.",
            RecordedAtUtc = DateTime.UtcNow.AddDays(-2)
        });

        var shipment = new ShipmentRecord
        {
            TenantId = tenantId,
            OrderId = order.Id,
            CourierProviderId = courier.Id,
            TrackingNumber = "AWB-FST-20261029-7777",
            CashToCollect = 110.0m,
            Status = CourierShipmentStatus.InTransit
        };
        db.ShipmentRecords.Add(shipment);
        await db.SaveChangesAsync();

        // Courier statement: Driver marked "Customer Refused" without ever attempting contact
        var csvStatement = @"TrackingNumber,ReportedStatus,CashCollected,CourierFee,Notes
AWB-FST-20261029-7777,Customer Refused,0.00,3.00,Client refused package";

        // Act
        var audit = await service.ReconcileRemittanceStatementAsync(
            tenantId, courier.Id, "STM-FST-PHANTOM-01", csvStatement, reconciledBy: "AuditSupervisor");

        // Assert
        Assert.NotNull(audit);
        Assert.Equal(1, audit.DiscrepancyCount);
        Assert.Equal(0, audit.ClearedCount);

        var match = audit.MatchDetails.First();
        Assert.Equal("PhantomReturn", match.DiscrepancyType);
        Assert.False(match.IsResolved);
        Assert.Equal(110.0m, match.HoldbackAmount);
        Assert.Contains("Phantom Return Detected", match.ResolutionNotes);

        // Dispute ticket
        Assert.Single(audit.DisputeTickets);
        var dispute = audit.DisputeTickets[0];
        Assert.Equal("PhantomReturnDiscrepancy", dispute.DiscrepancyType);
        Assert.Equal(110.0m, dispute.DisputedAmount);
        Assert.Contains("courier never contacted recipient", dispute.DisputeReason);

        // Net settlement math: 0 collected - 0 approved - 0 return fees (disputed) - 110 holdback = -110 (or holdback applied)
        Assert.Equal(110.0m, audit.DiscrepancyHoldback);
    }

    [Fact]
    public async Task ReconcileRemittance_Should_Detect_Unaccounted_Parcels()
    {
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        var courier = new CourierProvider { TenantId = tenantId, Name = "Express Courier", ApiCode = "EXP" };
        db.CourierProviders.Add(courier);
        await db.SaveChangesAsync();

        var csv = @"TrackingNumber,ReportedStatus,CashCollected,CourierFee
AWB-UNKNOWN-99999,Delivered,85.00,7.00";

        // Act
        var audit = await service.ReconcileRemittanceStatementAsync(
            tenantId, courier.Id, "STM-UNACC-01", csv);

        // Assert
        Assert.Equal(1, audit.DiscrepancyCount);
        var match = audit.MatchDetails[0];
        Assert.Equal("UnaccountedParcel", match.DiscrepancyType);
        Assert.Equal(85.00m, match.HoldbackAmount);
        Assert.False(match.IsResolved);
    }

    #endregion

    #region 4. Discrepancy Resolution & Financial KPIs

    [Fact]
    public async Task ResolveDiscrepancy_Should_Clear_Holdback_And_Record_Audit_Notes()
    {
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        var match = new RemittanceItemMatch
        {
            TenantId = tenantId,
            StatementId = Guid.NewGuid(),
            TrackingNumber = "AWB-TEST-0001",
            ReportedStatus = "Delivered",
            ReportedCashCollected = 80.0m,
            SystemExpectedAmount = 100.0m,
            DifferenceAmount = -20.0m,
            DiscrepancyType = RemittanceDiscrepancyType.ShortPayment,
            HoldbackAmount = 20.0m,
            IsResolved = false,
            ResolutionNotes = "Short-collection flagged"
        };
        db.RemittanceItemMatches.Add(match);
        await db.SaveChangesAsync();

        // Act
        var resolved = await service.ResolveDiscrepancyAsync(tenantId, match.Id, new DiscrepancyResolutionRequest(
            ReasonCode: "Courier clerical error",
            Notes: "Courier wire transferred the missing 20 TND directly to merchant account.",
            ResolvedBy: "FinanceManager"));

        // Assert
        Assert.NotNull(resolved);
        Assert.True(resolved.IsResolved);
        Assert.Equal(0m, resolved.HoldbackAmount);
        Assert.Contains("Courier clerical error", resolved.ResolutionNotes);
        Assert.Contains("FinanceManager", resolved.ResolutionNotes);

        var dbMatch = await db.RemittanceItemMatches.FindAsync(match.Id);
        Assert.NotNull(dbMatch);
        Assert.True(dbMatch.IsResolved);
        Assert.Equal(0m, dbMatch.HoldbackAmount);
    }

    [Fact]
    public async Task GetReconciliationKpis_Should_Accurately_Aggregate_Financial_Totals()
    {
        var db = CreateDbContext();
        var service = new CodCarrierAutoDispatchService(db);
        var tenantId = Guid.NewGuid();

        // 1. In transit shipment: 150 TND
        db.ShipmentRecords.Add(new ShipmentRecord
        {
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            TrackingNumber = "AWB-TR-01",
            CashToCollect = 150.0m,
            Status = CourierShipmentStatus.InTransit
        });

        // 2. Delivered shipment with collected cash: 200 TND
        db.ShipmentRecords.Add(new ShipmentRecord
        {
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            TrackingNumber = "AWB-TR-02",
            CashToCollect = 200.0m,
            CashRemitted = 200.0m,
            Status = CourierShipmentStatus.Delivered
        });

        // 3. Settled statement: 350 TND
        db.CourierRemittanceStatements.Add(new CourierRemittanceStatement
        {
            TenantId = tenantId,
            CourierProviderId = Guid.NewGuid(),
            StatementReference = "STM-SETTLED-01",
            NetRemittedAmount = 350.0m,
            Status = RemittanceStatus.Reconciled
        });

        // 4. Unresolved match with holdback: 45 TND
        db.RemittanceItemMatches.Add(new RemittanceItemMatch
        {
            TenantId = tenantId,
            StatementId = Guid.NewGuid(),
            TrackingNumber = "AWB-DISP-01",
            HoldbackAmount = 45.0m,
            DifferenceAmount = -45.0m,
            IsResolved = false
        });

        await db.SaveChangesAsync();

        // Act
        var kpis = await service.GetReconciliationKpisAsync(tenantId);

        // Assert
        Assert.NotNull(kpis);
        Assert.Equal(150.0m, kpis.TotalCodInTransit);
        Assert.Equal(200.0m, kpis.TotalCashCollectedByCouriers);
        Assert.Equal(350.0m, kpis.ClearedMerchantSettlement);
        Assert.Equal(1, kpis.UnresolvedDiscrepanciesCount);
        Assert.Equal(45.0m, kpis.UnresolvedDiscrepanciesValue);
    }

    #endregion
}
