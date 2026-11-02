using FluentAssertions;
using SGrowing.Api.Modules.SupplyChain.WarehouseBinCycleCounting;
using Xunit;

namespace SGrowing.Tests.SupplyChain;

public class WarehouseBinCycleCountingTests
{
    private readonly WarehouseBinAuditEngine _engine = new();

    [Fact]
    public void ProcessCycleCountAudit_WhenCountsMatchBookStock_ReturnsGradeA()
    {
        var request = new CycleCountAuditRequest(
            TenantId: "tenant-erp-01",
            WarehouseId: "WH-TUNIS-01",
            AuditSessionName: "Monthly Zone A Audit",
            Bins: new List<WarehouseBin>
            {
                new("bin-1", "WH-TUNIS-01", "ZoneA", "Aisle1", "Rack2", "Shelf3", "A-01-02-03", 2.5, 500)
            },
            BookStock: new List<BinStockItem>
            {
                new("SKU-OLIVE-OIL", "A-01-02-03", "LOT-99", DateTime.UtcNow.AddMonths(12), BookQuantity: 100, UnitCost: 15.00m, "A")
            },
            PhysicalCounts: new List<CycleCountEntry>
            {
                new("SKU-OLIVE-OIL", "A-01-02-03", "LOT-99", CountedQuantity: 100, "Auditor-Sam", DateTime.UtcNow)
            }
        );

        var result = _engine.ProcessCycleCountAudit(request);

        result.Should().NotBeNull();
        result.NetInventoryAccuracyPercentage.Should().Be(100.0);
        result.AuditGrade.Should().Be("AuditGradeA_Exceptional");
        result.Adjustments.Should().BeEmpty();
        result.TotalNetVarianceValue.Should().Be(0.0m);
    }

    [Fact]
    public void ProcessCycleCountAudit_WithShrinkage_CalculatesNegativeVarianceAndReason()
    {
        var request = new CycleCountAuditRequest(
            TenantId: "tenant-erp-01",
            WarehouseId: "WH-TUNIS-01",
            AuditSessionName: "Electronics Bin Audit",
            Bins: new List<WarehouseBin>
            {
                new("bin-2", "WH-TUNIS-01", "ZoneB", "Aisle4", "Rack1", "Shelf1", "B-04-01-01", 1.0, 200)
            },
            BookStock: new List<BinStockItem>
            {
                new("SKU-SMARTPHONE", "B-04-01-01", "LOT-88", null, BookQuantity: 20, UnitCost: 300.00m, "A")
            },
            PhysicalCounts: new List<CycleCountEntry>
            {
                // Physical count is 18 (2 units missing / shrinkage)
                new("SKU-SMARTPHONE", "B-04-01-01", "LOT-88", CountedQuantity: 18, "Auditor-Sam", DateTime.UtcNow)
            }
        );

        var result = _engine.ProcessCycleCountAudit(request);

        result.Adjustments.Should().ContainSingle();
        var adj = result.Adjustments[0];
        adj.VarianceQuantity.Should().Be(-2.0);
        adj.VarianceAmount.Should().Be(-600.00m);
        adj.AdjustmentReason.Should().Be("Shrinkage");
        adj.RequiresSupervisorSignoff.Should().BeTrue(); // High value Class A item
    }

    [Fact]
    public void ProcessCycleCountAudit_WithOverage_CalculatesPositiveVariance()
    {
        var request = new CycleCountAuditRequest(
            TenantId: "tenant-erp-01",
            WarehouseId: "WH-TUNIS-01",
            AuditSessionName: "Packaging Bins",
            Bins: new List<WarehouseBin>
            {
                new("bin-3", "WH-TUNIS-01", "ZoneC", "Aisle1", "Rack1", "Shelf1", "C-01-01-01", 5.0, 1000)
            },
            BookStock: new List<BinStockItem>
            {
                new("SKU-BOX-M", "C-01-01-01", "LOT-77", null, BookQuantity: 500, UnitCost: 0.50m, "C")
            },
            PhysicalCounts: new List<CycleCountEntry>
            {
                // Physical count is 520 (+20 overage)
                new("SKU-BOX-M", "C-01-01-01", "LOT-77", CountedQuantity: 520, "Auditor-Sam", DateTime.UtcNow)
            }
        );

        var result = _engine.ProcessCycleCountAudit(request);

        result.Adjustments.Should().ContainSingle();
        result.Adjustments[0].VarianceQuantity.Should().Be(20.0);
        result.Adjustments[0].VarianceAmount.Should().Be(10.00m);
        result.Adjustments[0].AdjustmentReason.Should().Be("Overage");
    }

    [Fact]
    public void ProcessCycleCountAudit_WithLowAccuracy_ReturnsGradeCRequiresRecount()
    {
        var request = new CycleCountAuditRequest(
            TenantId: "tenant-erp-01",
            WarehouseId: "WH-TUNIS-01",
            AuditSessionName: "Discrepancy Audit",
            Bins: new List<WarehouseBin>(),
            BookStock: new List<BinStockItem>
            {
                new("SKU-1", "A-01", "L1", null, 10, 10.0m, "B"),
                new("SKU-2", "A-02", "L2", null, 10, 10.0m, "B"),
                new("SKU-3", "A-03", "L3", null, 10, 10.0m, "B"),
                new("SKU-4", "A-04", "L4", null, 10, 10.0m, "B")
            },
            PhysicalCounts: new List<CycleCountEntry>
            {
                new("SKU-1", "A-01", "L1", 10, "Auditor-1", DateTime.UtcNow), // 1 match
                new("SKU-2", "A-02", "L2", 5, "Auditor-1", DateTime.UtcNow),  // mismatch
                new("SKU-3", "A-03", "L3", 8, "Auditor-1", DateTime.UtcNow),  // mismatch
                new("SKU-4", "A-04", "L4", 12, "Auditor-1", DateTime.UtcNow)  // mismatch
            }
        );

        var result = _engine.ProcessCycleCountAudit(request);

        // Accuracy is 1/4 = 25%
        result.NetInventoryAccuracyPercentage.Should().Be(25.0);
        result.AuditGrade.Should().Be("AuditGradeC_RequiresRecount");
    }

    [Fact]
    public void ProcessCycleCountAudit_FoundUnrecordedStockInBin_CreatesAdjustment()
    {
        var request = new CycleCountAuditRequest(
            TenantId: "tenant-erp-01",
            WarehouseId: "WH-TUNIS-01",
            AuditSessionName: "Ghost Stock Audit",
            Bins: new List<WarehouseBin>(),
            BookStock: new List<BinStockItem>(), // Book stock says 0
            PhysicalCounts: new List<CycleCountEntry>
            {
                new("SKU-FOUND-PALLET", "D-05-02-01", "LOT-UNKNOWN", 15, "Auditor-Dave", DateTime.UtcNow)
            }
        );

        var result = _engine.ProcessCycleCountAudit(request);

        result.Adjustments.Should().ContainSingle();
        result.Adjustments[0].PhysicalQuantity.Should().Be(15.0);
        result.Adjustments[0].BookQuantity.Should().Be(0.0);
        result.Adjustments[0].AdjustmentReason.Should().Be("Overage");
    }
}
