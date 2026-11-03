using FluentAssertions;
using SGrowing.Api.Modules.Advertising.RetailSignageMesh;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class RetailSignageMeshTests
{
    private readonly RetailSignageMeshEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsScreensAndShelves()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveInStoreScreensCount.Should().Be(2);
        overview.TotalPlanogramShelvesMonitored.Should().Be(2);
        overview.OutOfStockSuppressionRatePct.Should().Be(100.0);
        overview.Screens.Should().HaveCount(2);
        overview.Shelves.Should().HaveCount(2);
    }

    [Fact]
    public void EvaluateShelfBid_InStockServesAdSuccessfully()
    {
        var request = new EvaluateShelfBidRequest(
            "SCR-NYC-FLAGSHIP-01",
            "SHLF-BEV-01",
            12,
            "14:30"
        );

        var result = _engine.EvaluateShelfBid(request);

        result.Should().NotBeNull();
        result.CanServeAd.Should().BeTrue();
        result.WinningCpmUsd.Should().Be(24.50m);
        result.InventoryStatus.Should().Be("IN_STOCK_SPONSORED_SERVED");
        result.WinningBrandCreativeUrl.Should().Contain(".mp4");
    }

    [Fact]
    public void UpdateShelfStock_RestockingTogglesAdState()
    {
        var request = new UpdateShelfStockRequest(
            "SHLF-SNK-02",
            18, // Restocked above threshold 5
            "RFID_PORTAL"
        );

        var receipt = _engine.UpdateShelfStock(request);

        receipt.Should().NotBeNull();
        receipt.ShelfId.Should().Be("SHLF-SNK-02");
        receipt.UpdatedStock.Should().Be(18);
        receipt.AdStatusToggled.Should().BeTrue();
    }
}
