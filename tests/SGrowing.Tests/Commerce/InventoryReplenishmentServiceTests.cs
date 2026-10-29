using FluentAssertions;
using SGrowing.Api.Modules.Commerce;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class InventoryReplenishmentServiceTests
{
    private readonly InventoryReplenishmentService _service;
    private readonly Guid _tenantId;

    public InventoryReplenishmentServiceTests()
    {
        _service = new InventoryReplenishmentService();
        _tenantId = Guid.NewGuid();
    }

    #region 1. Math: Daily Velocity

    [Fact]
    public void DailyVelocity_CalculatesExactThirtyDayDemandMean()
    {
        // Arrange: 30 days of 15 units = 450 total units
        var demands = Enumerable.Repeat(15, 30).ToList();

        // Act
        var velocity = InventoryReplenishmentService.CalculateDailyVelocity(demands);

        // Assert
        velocity.Should().BeApproximately(15.0, 0.001);
    }

    [Fact]
    public void DailyVelocity_EmptyOrNullDemand_ReturnsZero()
    {
        // Act & Assert
        InventoryReplenishmentService.CalculateDailyVelocity(null).Should().Be(0.0);
        InventoryReplenishmentService.CalculateDailyVelocity(new List<int>()).Should().Be(0.0);
    }

    #endregion

    #region 2. Math: Days of Inventory Remaining (DOIR)

    [Fact]
    public void DaysOfInventoryRemaining_CalculatesAccurateCoverage()
    {
        // Stock: 150 units, Velocity: 10 units/day -> DOIR = 15.0 days
        var doir = InventoryReplenishmentService.CalculateDoir(150, 10.0);
        doir.Should().Be(15.0);
    }

    [Fact]
    public void DaysOfInventoryRemaining_HandlesZeroOrNearZeroVelocity_WithoutDivisionByZero()
    {
        // Zero stock with zero velocity -> 0.0
        InventoryReplenishmentService.CalculateDoir(0, 0.0).Should().Be(0.0);

        // Positive stock with zero velocity -> capped at 999.0
        InventoryReplenishmentService.CalculateDoir(100, 0.0).Should().Be(999.0);
        InventoryReplenishmentService.CalculateDoir(50, 0.00001).Should().Be(999.0);
    }

    #endregion

    #region 3. Math: Dynamic Safety Stock

    [Fact]
    public void DynamicSafetyStock_CalculatesCorrectly_For95And99ServiceLevels()
    {
        // 95% service level: Z = 1.65
        // LeadTimeVarianceFactor = 1.5, DailyDemandMean = 16.0 (sqrt = 4.0)
        // Safety Stock = 1.65 * 1.5 * 4.0 = 9.9
        var z95 = InventoryReplenishmentService.ResolveZFactor(95.0);
        z95.Should().Be(1.65);

        var safetyStockExact95 = InventoryReplenishmentService.CalculateDynamicSafetyStock(z95, 1.5, 16.0);
        safetyStockExact95.Should().BeApproximately(9.9, 0.001);

        var safetyStockUnits95 = InventoryReplenishmentService.CalculateDynamicSafetyStockUnits(z95, 1.5, 16.0);
        safetyStockUnits95.Should().Be(10); // Ceiled

        // 99% service level: Z = 2.33
        // LeadTimeVarianceFactor = 2.0, DailyDemandMean = 25.0 (sqrt = 5.0)
        // Safety Stock = 2.33 * 2.0 * 5.0 = 23.3
        var z99 = InventoryReplenishmentService.ResolveZFactor(99.0);
        z99.Should().Be(2.33);

        var safetyStockExact99 = InventoryReplenishmentService.CalculateDynamicSafetyStock(z99, 2.0, 25.0);
        safetyStockExact99.Should().BeApproximately(23.3, 0.001);

        var safetyStockUnits99 = InventoryReplenishmentService.CalculateDynamicSafetyStockUnits(z99, 2.0, 25.0);
        safetyStockUnits99.Should().Be(24); // Ceiled
    }

    #endregion

    #region 4. Math: Reorder Point (ROP)

    [Fact]
    public void ReorderPoint_CalculatesLeadTimeDemandPlusSafetyStock()
    {
        // LeadTime: 14 days, Velocity: 12.5 units/day, SafetyStock: 25 units
        // Lead time demand = 14 * 12.5 = 175
        // ROP = 175 + 25 = 200
        var rop = InventoryReplenishmentService.CalculateReorderPoint(14, 12.5, 25.0);
        rop.Should().Be(200.0);

        var ropUnits = InventoryReplenishmentService.CalculateReorderPointUnits(14, 12.5, 25.0);
        ropUnits.Should().Be(200);
    }

    #endregion

    #region 5. Math: Economic Order Quantity (EOQ)

    [Fact]
    public void EconomicOrderQuantity_CalculatesStandardSquareRootCostFormula()
    {
        // AnnualDemand = 3650 (10 units/day * 365 days)
        // OrderCost = $100.00
        // HoldingCostPerUnitPerYear = $5.00
        // EOQ = sqrt((2 * 3650 * 100) / 5) = sqrt(730000 / 5) = sqrt(146000) = 382.099...
        var eoqExact = InventoryReplenishmentService.CalculateEoq(3650.0, 100m, 5.0m);
        eoqExact.Should().BeApproximately(382.10, 0.01);

        var eoqUnits = InventoryReplenishmentService.CalculateEoqUnits(3650.0, 100m, 5.0m);
        eoqUnits.Should().Be(383); // Ceiled
    }

    [Fact]
    public void EconomicOrderQuantity_ZeroDemandOrHoldingCost_ReturnsZeroSafely()
    {
        InventoryReplenishmentService.CalculateEoq(0.0, 100m, 5m).Should().Be(0.0);
        InventoryReplenishmentService.CalculateEoq(3650.0, 0m, 5m).Should().Be(0.0);
        InventoryReplenishmentService.CalculateEoq(3650.0, 100m, 0m).Should().Be(0.0);
    }

    #endregion

    #region 6. Stock Health Classification

    [Theory]
    [InlineData(0, 100, 0, StockHealthTier.CriticalOutOfStock)]
    [InlineData(50, 100, 10, StockHealthTier.LowStockBreach)]
    [InlineData(100, 100, 20, StockHealthTier.LowStockBreach)]
    [InlineData(120, 100, 25, StockHealthTier.AtRisk)]
    [InlineData(125, 100, 25, StockHealthTier.AtRisk)]
    [InlineData(200, 100, 40, StockHealthTier.Optimal)]
    [InlineData(350, 100, 70, StockHealthTier.Optimal)]
    [InlineData(351, 100, 70, StockHealthTier.Overstocked)]
    [InlineData(200, 100, 95, StockHealthTier.Overstocked)]
    public void StockHealthClassification_AssignsAccurateHealthTier_AcrossAllBoundaries(
        int stock, double rop, double doir, string expectedTier)
    {
        var tier = InventoryReplenishmentService.ClassifyStockHealth(stock, rop, doir);
        tier.Should().Be(expectedTier);
    }

    #endregion

    #region 7. Vendor MOQ & Batch Multiplier Snapping

    [Fact]
    public void PurchaseOrderQuantity_RespectsVendorMoq_AndSnapsToBatchMultiplier()
    {
        // Case 1: Deficit is 45, MOQ is 100, BatchMultiplier is 50 -> Snaps to 100
        var qty1 = InventoryReplenishmentService.CalculateReplenishmentQuantity(
            currentStock: 10, rop: 50, eoq: 45, safetyStock: 15, moq: 100, batchMultiplier: 50);
        qty1.Should().Be(100);

        // Case 2: Deficit is 120, MOQ is 100, BatchMultiplier is 50 -> Snaps to 150
        var qty2 = InventoryReplenishmentService.CalculateReplenishmentQuantity(
            currentStock: 0, rop: 60, eoq: 120, safetyStock: 20, moq: 100, batchMultiplier: 50);
        qty2.Should().Be(150);

        // Case 3: Deficit is 210, MOQ is 200, BatchMultiplier is 25 -> Snaps to 225
        var qty3 = InventoryReplenishmentService.CalculateReplenishmentQuantity(
            currentStock: 20, rop: 150, eoq: 210, safetyStock: 30, moq: 200, batchMultiplier: 25);
        qty3.Should().Be(225);

        // Case 4: No batch multiplier (1), MOQ is 20, Deficit is 33 -> 33
        var qty4 = InventoryReplenishmentService.CalculateReplenishmentQuantity(
            currentStock: 5, rop: 20, eoq: 33, safetyStock: 5, moq: 20, batchMultiplier: 1);
        qty4.Should().Be(33);
    }

    #endregion

    #region 8. Autonomous Purchase Order Generation

    [Fact]
    public async Task AutonomousPoGenerator_CreatesPurchaseOrders_ForBreachedOrStockoutItems()
    {
        // Act: Run autonomous generator for default seeded inventory
        var pos = await _service.GenerateAutonomousPurchaseOrdersAsync(_tenantId);

        // Assert
        pos.Should().NotBeNull();
        pos.Should().NotBeEmpty();

        foreach (var po in pos)
        {
            po.Status.Should().Be(PurchaseOrderStatus.SubmittedToVendor);
            po.Items.Should().NotBeEmpty();
            po.TotalUnits.Should().BeGreaterThan(0);
            po.SubtotalUsd.Should().BeGreaterThan(0m);
            po.TotalAmountUsd.Should().BeGreaterThan(po.SubtotalUsd);
            po.VendorContactEmail.Should().Contain("@");
            po.ExpectedDeliveryDateUtc.Should().BeAfter(DateTime.UtcNow);

            foreach (var line in po.Items)
            {
                line.Quantity.Should().BeGreaterThanOrEqualTo(line.AppliedMoq);
                if (line.AppliedBatchMultiplier > 1)
                {
                    (line.Quantity % line.AppliedBatchMultiplier).Should().Be(0);
                }
            }
        }
    }

    [Fact]
    public async Task DuplicateAutonomousGeneration_DoesNotCreateDuplicateActiveOrders()
    {
        // Act: Run twice in succession
        var firstRun = await _service.GenerateAutonomousPurchaseOrdersAsync(_tenantId);
        var secondRun = await _service.GenerateAutonomousPurchaseOrdersAsync(_tenantId);

        // Assert: Second run should find no new un-ordered breached items since first run POs are active
        secondRun.Should().BeEmpty();
    }

    #endregion

    #region 9. Purchase Order Lifecycle & State Transitions

    [Fact]
    public async Task PurchaseOrderLifecycle_ValidatesStrictStateTransitions()
    {
        // Arrange: Create a manual PO (starts in Draft)
        var createReq = new CreateManualPoRequest(
            VendorId: "VND-SHENZHEN-AUDIO",
            DestinationWarehouseId: "WH-CENTRAL",
            Sku: "ANC-HEADPHONES-PRO",
            Quantity: 50,
            Notes: "Lifecycle validation test order",
            CreatedBy: "Test Suite");

        var po = await _service.CreateManualPurchaseOrderAsync(_tenantId, createReq);
        po.Status.Should().Be(PurchaseOrderStatus.Draft);

        // Transition 1: Draft -> SubmittedToVendor (Valid)
        var update1 = await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.SubmittedToVendor));
        update1.Status.Should().Be(PurchaseOrderStatus.SubmittedToVendor);
        update1.SubmittedAtUtc.Should().NotBeNull();

        // Transition 2: SubmittedToVendor -> Acknowledged (Valid)
        var update2 = await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.Acknowledged));
        update2.Status.Should().Be(PurchaseOrderStatus.Acknowledged);

        // Transition 3: Acknowledged -> InTransit with Tracking Number (Valid)
        var update3 = await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.InTransit, TrackingNumber: "DHL-99218204"));
        update3.Status.Should().Be(PurchaseOrderStatus.InTransit);
        update3.TrackingNumber.Should().Be("DHL-99218204");

        // Invalid Transition: InTransit -> Cancelled (Invalid: once shipped, cannot cancel)
        var actInvalid = async () => await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.Cancelled));
        await actInvalid.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot transition Purchase Order*");

        // Transition 4: InTransit -> Received (Valid)
        var update4 = await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.Received));
        update4.Status.Should().Be(PurchaseOrderStatus.Received);
        update4.ReceivedAtUtc.Should().NotBeNull();

        // Terminal State Check: Received cannot transition to anything
        var actTerminal = async () => await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.Draft));
        await actTerminal.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region 10. Receiving Stock Increments Physical Inventory

    [Fact]
    public async Task ReceivingPurchaseOrder_AutomaticallyIncrementsWarehouseStock()
    {
        // Arrange
        var sku = "ANC-HEADPHONES-PRO";
        var warehouseId = "WH-CENTRAL";
        var itemBefore = await _service.GetItemBySkuAsync(_tenantId, sku);
        itemBefore.Should().NotBeNull();

        var wsBefore = itemBefore!.WarehouseBreakdown.First(w => w.WarehouseId == warehouseId);
        var stockBefore = wsBefore.CurrentStock;

        // Create PO for 100 units (snaps to MOQ 50, batch 25)
        var createReq = new CreateManualPoRequest(
            VendorId: "VND-SHENZHEN-AUDIO",
            DestinationWarehouseId: warehouseId,
            Sku: sku,
            Quantity: 75);

        var po = await _service.CreateManualPurchaseOrderAsync(_tenantId, createReq);
        var poQty = po.Items.First().Quantity;

        // Advance through lifecycle to Received
        await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.SubmittedToVendor));
        await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.Acknowledged));
        await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.InTransit, TrackingNumber: "FEDEX-88301"));
        await _service.UpdatePurchaseOrderStatusAsync(_tenantId, po.Id,
            new UpdatePoStatusRequest(PurchaseOrderStatus.Received));

        // Act: Check item stock after receipt
        var itemAfter = await _service.GetItemBySkuAsync(_tenantId, sku);
        itemAfter.Should().NotBeNull();

        var wsAfter = itemAfter!.WarehouseBreakdown.First(w => w.WarehouseId == warehouseId);

        // Assert: Stock increased by exact PO quantity
        wsAfter.CurrentStock.Should().Be(stockBefore + poQty);
        itemAfter.TotalCurrentStock.Should().Be(itemBefore.TotalCurrentStock + poQty);
    }

    #endregion

    #region 11. Multi-Warehouse Rebalancing Transfers

    [Fact]
    public async Task MultiWarehouseRebalance_GeneratesProposals_FromSurplusToDeficitWarehouse()
    {
        // Act
        var proposals = await _service.GetTransferRecommendationsAsync(_tenantId);

        // Assert
        proposals.Should().NotBeNull();
        proposals.Should().NotBeEmpty();

        foreach (var p in proposals)
        {
            p.SourceWarehouseId.Should().NotBe(p.DestinationWarehouseId);
            p.SuggestedQuantity.Should().BeGreaterThanOrEqualTo(10);
            p.EstimatedFreightCostUsd.Should().BeGreaterThan(0m);
            p.EstimatedTransitDays.Should().BeGreaterThan(0);
            p.Status.Should().Be(TransferProposalStatus.Proposed);
            TransferUrgency.Critical.Should().Match(u => p.Urgency == TransferUrgency.Critical ||
                                                         p.Urgency == TransferUrgency.High ||
                                                         p.Urgency == TransferUrgency.Medium);
        }
    }

    [Fact]
    public async Task ExecuteTransfer_AdjustsStockAcrossWarehouses_Accurately()
    {
        // Arrange
        var proposals = await _service.GetTransferRecommendationsAsync(_tenantId);
        proposals.Should().NotBeEmpty();

        var proposal = proposals.First();

        var itemBefore = await _service.GetItemBySkuAsync(_tenantId, proposal.Sku);
        var sourceStockBefore = itemBefore!.WarehouseBreakdown.First(w => w.WarehouseId == proposal.SourceWarehouseId).CurrentStock;
        var destStockBefore = itemBefore.WarehouseBreakdown.First(w => w.WarehouseId == proposal.DestinationWarehouseId).CurrentStock;

        // Act: Execute transfer
        var execResult = await _service.ExecuteTransferAsync(_tenantId, new ExecuteTransferRequest(proposal.ProposalId));

        // Assert
        execResult.Status.Should().Be(TransferProposalStatus.Completed);
        execResult.TransferredQuantity.Should().Be(proposal.SuggestedQuantity);

        var itemAfter = await _service.GetItemBySkuAsync(_tenantId, proposal.Sku);
        var sourceStockAfter = itemAfter!.WarehouseBreakdown.First(w => w.WarehouseId == proposal.SourceWarehouseId).CurrentStock;
        var destStockAfter = itemAfter.WarehouseBreakdown.First(w => w.WarehouseId == proposal.DestinationWarehouseId).CurrentStock;

        sourceStockAfter.Should().Be(sourceStockBefore - proposal.SuggestedQuantity);
        destStockAfter.Should().Be(destStockBefore + proposal.SuggestedQuantity);

        // Executing the same transfer again should fail
        var actDuplicate = async () => await _service.ExecuteTransferAsync(_tenantId, new ExecuteTransferRequest(proposal.ProposalId));
        await actDuplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been executed*");
    }

    #endregion

    #region 12. Dynamic Recalculation & Health Summary

    [Fact]
    public async Task RecalculateMetrics_UpdatesAllInventoryItems_WithCustomParameters()
    {
        // Act: Recalculate with 99% service level (Z = 2.33)
        var recalc = await _service.RecalculateMetricsAsync(_tenantId, new RecalculateInventoryRequest(
            ServiceLevelPercent: 99.0,
            OrderCostOverride: 120m,
            AnnualHoldingRateOverride: 0.25m));

        // Assert
        recalc.TotalSkusProcessed.Should().BeGreaterThan(0);
        recalc.ServiceLevelPercent.Should().Be(99.0);
        recalc.ZFactorUsed.Should().Be(2.33);
        recalc.DefaultOrderCostUsd.Should().Be(120m);
        recalc.AnnualHoldingRatePercent.Should().Be(25.0m);
    }

    [Fact]
    public async Task StockHealthSummary_AggregatesAccurateKpis()
    {
        // Act
        var summary = await _service.GetHealthSummaryAsync(_tenantId);

        // Assert
        summary.Should().NotBeNull();
        summary.TotalSkus.Should().BeGreaterThan(0);
        summary.TotalUnitsOnHand.Should().BeGreaterThan(0);
        summary.TotalAssetValueUsd.Should().BeGreaterThan(0m);
        (summary.CriticalOutOfStockCount + summary.LowStockBreachCount + summary.AtRiskCount +
         summary.OptimalCount + summary.OverstockedCount).Should().Be(summary.TotalSkus);
    }

    #endregion
}
