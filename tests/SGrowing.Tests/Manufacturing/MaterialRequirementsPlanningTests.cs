using FluentAssertions;
using SGrowing.Api.Modules.Manufacturing.MaterialRequirementsPlanning;
using Xunit;

namespace SGrowing.Tests.Manufacturing;

public class MaterialRequirementsPlanningTests
{
    private readonly MrpEngine _engine = new();

    [Fact]
    public void ExplodeRequirements_WithZeroDemand_ProducesNoPlannedOrders()
    {
        var request = new MrpExplosionRequest(
            TenantId: "tenant-erp-01",
            PlanStartDate: new DateTime(2026, 11, 1),
            PlanningHorizonDays: 30,
            BucketSizeDays: 7,
            Items: new List<MrpItemRecord>
            {
                new("item-1", "WIDGET-A", "Premium Widget", LeadTimeDays: 7, SafetyStock: 10, CurrentOnHand: 50, AllocatedStock: 0, MinimumOrderQuantity: 20, BatchMultiple: 5)
            },
            BillOfMaterials: new List<MrpBomComponent>(),
            GrossDemands: new List<MrpGrossDemand>(),
            ScheduledReceipts: new List<MrpScheduledReceipt>()
        );

        var result = _engine.ExplodeRequirements(request);

        result.Should().NotBeNull();
        result.TotalPlannedOrdersCount.Should().Be(0);
        result.ActionMessages.Should().BeEmpty();
        result.ItemBuckets["WIDGET-A"].Should().HaveCount(5);
        result.ItemBuckets["WIDGET-A"][0].ProjectedOnHand.Should().Be(50);
    }

    [Fact]
    public void ExplodeRequirements_WithGrossDemandExceedingStock_GeneratesPlannedOrders()
    {
        var request = new MrpExplosionRequest(
            TenantId: "tenant-erp-01",
            PlanStartDate: new DateTime(2026, 11, 1),
            PlanningHorizonDays: 14,
            BucketSizeDays: 7,
            Items: new List<MrpItemRecord>
            {
                new("item-1", "MOTOR-X", "Electric Drive Motor", LeadTimeDays: 7, SafetyStock: 5, CurrentOnHand: 10, AllocatedStock: 0, MinimumOrderQuantity: 20, BatchMultiple: 10)
            },
            BillOfMaterials: new List<MrpBomComponent>(),
            GrossDemands: new List<MrpGrossDemand>
            {
                new("d-1", "MOTOR-X", new DateTime(2026, 11, 2), 25, "Sales Order #1001")
            },
            ScheduledReceipts: new List<MrpScheduledReceipt>()
        );

        var result = _engine.ExplodeRequirements(request);

        result.TotalPlannedOrdersCount.Should().BeGreaterThan(0);
        result.ActionMessages.Should().Contain(m => m.Sku == "MOTOR-X" && m.ActionType == "ReleaseOrder");
        var bucket0 = result.ItemBuckets["MOTOR-X"][0];
        bucket0.GrossRequirements.Should().Be(25);
        bucket0.PlannedOrderReceipts.Should().Be(20); // MOQ 20 and batch 10
    }

    [Fact]
    public void ExplodeRequirements_RespectsMinimumOrderQuantityAndBatchMultiples()
    {
        var request = new MrpExplosionRequest(
            TenantId: "tenant-erp-01",
            PlanStartDate: new DateTime(2026, 11, 1),
            PlanningHorizonDays: 7,
            BucketSizeDays: 7,
            Items: new List<MrpItemRecord>
            {
                new("item-1", "CHASSIS-1", "Aluminum Chassis", LeadTimeDays: 0, SafetyStock: 0, CurrentOnHand: 0, AllocatedStock: 0, MinimumOrderQuantity: 50, BatchMultiple: 25)
            },
            BillOfMaterials: new List<MrpBomComponent>(),
            GrossDemands: new List<MrpGrossDemand>
            {
                new("d-1", "CHASSIS-1", new DateTime(2026, 11, 3), 60, "Urgent Build")
            },
            ScheduledReceipts: new List<MrpScheduledReceipt>()
        );

        var result = _engine.ExplodeRequirements(request);

        // Demand is 60, MOQ is 50, but multiple is 25 => rounded up to 75
        result.ActionMessages.Should().ContainSingle();
        result.ActionMessages[0].Quantity.Should().Be(75);
    }

    [Fact]
    public void ExplodeRequirements_ExplodesMultiLevelBomIntoComponentDemands()
    {
        var request = new MrpExplosionRequest(
            TenantId: "tenant-erp-01",
            PlanStartDate: new DateTime(2026, 11, 1),
            PlanningHorizonDays: 21,
            BucketSizeDays: 7,
            Items: new List<MrpItemRecord>
            {
                new("parent-1", "BIKE-PRO", "Pro Road Bike", LeadTimeDays: 7, SafetyStock: 0, CurrentOnHand: 0, AllocatedStock: 0, MinimumOrderQuantity: 10, BatchMultiple: 1),
                new("comp-1", "WHEEL-700C", "700c Wheel Assembly", LeadTimeDays: 7, SafetyStock: 0, CurrentOnHand: 0, AllocatedStock: 0, MinimumOrderQuantity: 20, BatchMultiple: 2)
            },
            BillOfMaterials: new List<MrpBomComponent>
            {
                new("BIKE-PRO", "WHEEL-700C", QuantityPerParent: 2, ScrapPercentage: 5.0)
            },
            GrossDemands: new List<MrpGrossDemand>
            {
                new("d-1", "BIKE-PRO", new DateTime(2026, 11, 8), 10, "Dealer Order")
            },
            ScheduledReceipts: new List<MrpScheduledReceipt>()
        );

        var result = _engine.ExplodeRequirements(request);

        result.ItemBuckets.Should().ContainKey("BIKE-PRO");
        result.ItemBuckets.Should().ContainKey("WHEEL-700C");
        // Component demand = 10 bikes * 2 wheels * 1.05 scrap = 21 wheels
        var wheelBuckets = result.ItemBuckets["WHEEL-700C"];
        wheelBuckets.Sum(b => b.PlannedOrderReceipts).Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void ExplodeRequirements_WithScheduledReceipt_OffsetsNetRequirements()
    {
        var request = new MrpExplosionRequest(
            TenantId: "tenant-erp-01",
            PlanStartDate: new DateTime(2026, 11, 1),
            PlanningHorizonDays: 14,
            BucketSizeDays: 7,
            Items: new List<MrpItemRecord>
            {
                new("item-1", "GEAR-BOX", "Industrial Gear Box", LeadTimeDays: 7, SafetyStock: 10, CurrentOnHand: 5, AllocatedStock: 0, MinimumOrderQuantity: 10, BatchMultiple: 1)
            },
            BillOfMaterials: new List<MrpBomComponent>(),
            GrossDemands: new List<MrpGrossDemand>
            {
                new("d-1", "GEAR-BOX", new DateTime(2026, 11, 3), 30, "Assembly Line A")
            },
            ScheduledReceipts: new List<MrpScheduledReceipt>
            {
                new("po-99", "GEAR-BOX", new DateTime(2026, 11, 2), 25, "PO-8821 Incoming")
            }
        );

        var result = _engine.ExplodeRequirements(request);

        // On hand 5 + Receipt 25 = 30. Demand is 30.
        // Safety stock target is 10, so deficit is 10.
        var bucket0 = result.ItemBuckets["GEAR-BOX"][0];
        bucket0.ScheduledReceipts.Should().Be(25);
        bucket0.GrossRequirements.Should().Be(30);
        bucket0.NetRequirements.Should().Be(10);
    }
}
