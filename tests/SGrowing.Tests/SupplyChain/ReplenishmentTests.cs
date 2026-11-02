using SGrowing.Api.Modules.SupplyChain.EdiReplenishment;
using Xunit;

namespace SGrowing.Tests.SupplyChain;

public sealed class ReplenishmentTests
{
    private readonly EdiReplenishmentEngine _engine = new();

    [Fact]
    public async Task GetReplenishmentRecommendations_IdentifiesCriticalDepletions()
    {
        var recommendations = await _engine.GetReplenishmentRecommendationsAsync();

        Assert.NotNull(recommendations);
        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, r => r.SkuId == "SKU-OPTIC-400G");

        var critical = recommendations.First(r => r.SkuId == "SKU-OPTIC-400G");
        Assert.True(critical.CurrentAvailable <= critical.ReorderPoint);
        Assert.Equal("Critical", critical.Severity);
        Assert.True(critical.EstimatedDaysUntilStockout < 10);
    }

    [Fact]
    public async Task GeneratePurchaseOrder_CreatesCompliantAnsiX12Edi850Payload()
    {
        var request = new GeneratePoRequest(
            SupplierId: "sup_apex_logistics",
            WarehouseId: "wh_frankfurt_eu",
            SkuIds: new[] { "SKU-OPTIC-400G", "SKU-FIBER-LC10M" }
        );

        var po = await _engine.GeneratePurchaseOrderAsync(request);

        Assert.NotNull(po);
        Assert.StartsWith("PO-", po.PoNumber);
        Assert.Equal("Staged", po.Status);
        Assert.True(po.TotalAmount > 0);
        Assert.Equal(2, po.LineItems.Count);

        // Verify ANSI X12 EDI 850 segment structures
        Assert.Contains("ISA*00*", po.Edi850RawPayload);
        Assert.Contains("GS*PO*GROWTHOS*", po.Edi850RawPayload);
        Assert.Contains("ST*850*", po.Edi850RawPayload);
        Assert.Contains("BEG*00*NE*", po.Edi850RawPayload);
        Assert.Contains("PO1*1*", po.Edi850RawPayload);
        Assert.Contains("CTT*2*", po.Edi850RawPayload);
        Assert.Contains("IEA*1*", po.Edi850RawPayload);
    }

    [Fact]
    public async Task DispatchPurchaseOrder_UpdatesStatusToDispatched()
    {
        var request = new GeneratePoRequest(
            SupplierId: "sup_apex_logistics",
            WarehouseId: "wh_frankfurt_eu",
            SkuIds: new[] { "SKU-OPTIC-400G" }
        );
        var po = await _engine.GeneratePurchaseOrderAsync(request);

        var dispatched = await _engine.DispatchPurchaseOrderAsync(po.PoNumber);

        Assert.NotNull(dispatched);
        Assert.Equal("Dispatched", dispatched.Status);
        Assert.NotNull(dispatched.DispatchedAtUtc);
    }
}
