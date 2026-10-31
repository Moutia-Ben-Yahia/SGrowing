using FluentAssertions;
using SGrowing.Api.Modules.Commerce.Fulfillment;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class SplitShipmentRoutingServiceTests
{
    private readonly SplitShipmentRoutingService _service = new();
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public async Task CreateWarehouseNodeAsync_ShouldCreateActiveNode()
    {
        var req = new CreateWarehouseNodeRequest(
            _tenantId,
            "Sousse Coastal Micro-Hub",
            "4000",
            35.8256,
            10.6369,
            WarehouseNodeType.UrbanMicroHub,
            300
        );

        var node = await _service.CreateWarehouseNodeAsync(req);

        node.Should().NotBeNull();
        node.WarehouseId.Should().NotBeEmpty();
        node.Name.Should().Be("Sousse Coastal Micro-Hub");
        node.Type.Should().Be(WarehouseNodeType.UrbanMicroHub);
        node.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RouteOrderShipmentAsync_ShouldFulfillFromSingleClosestWarehouse_WhenStockSufficient()
    {
        // Destination near Tunis (36.85, 10.20)
        var req = new RouteOrderShipmentRequest(
            _tenantId,
            Guid.NewGuid(),
            36.8500,
            10.2000,
            "1002",
            new List<OrderItemRequest>
            {
                new("SKU-ELEC-01", "Wireless Earbuds", 2, 45.0m),
                new("SKU-FASH-02", "Cotton T-Shirt", 1, 25.0m)
            }
        );

        var result = await _service.RouteOrderShipmentAsync(req);

        result.Should().NotBeNull();
        result.IsSplitShipment.Should().BeFalse();
        result.TotalPackagesCount.Should().Be(1);
        result.Packages.Should().ContainSingle();
        result.Packages[0].WarehouseName.Should().Contain("Tunis Central Mega-Hub");
        result.Packages[0].Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task RouteOrderShipmentAsync_ShouldSplitOrder_WhenItemsDividedAcrossWarehouses()
    {
        // Request SKU-FASH-02 (only in Tunis) and SKU-HOME-03 (only in Sfax)
        var req = new RouteOrderShipmentRequest(
            _tenantId,
            Guid.NewGuid(),
            35.8256,
            10.6369,
            "4000",
            new List<OrderItemRequest>
            {
                new("SKU-FASH-02", "Cotton T-Shirt", 1, 25.0m),
                new("SKU-HOME-03", "Aroma Diffuser", 1, 35.0m)
            }
        );

        var result = await _service.RouteOrderShipmentAsync(req);

        result.IsSplitShipment.Should().BeTrue();
        result.TotalPackagesCount.Should().Be(2);
        result.Packages.Should().HaveCount(2);
        result.TotalShippingCost.Should().Be(14.00m); // 2 * 7.00m
    }

    [Fact]
    public async Task RouteOrderShipmentAsync_ShouldThrow_WhenNoItemsProvided()
    {
        var req = new RouteOrderShipmentRequest(_tenantId, Guid.NewGuid(), 36.8, 10.1, "1000", new List<OrderItemRequest>());
        await Assert.ThrowsAsync<ArgumentException>(() => _service.RouteOrderShipmentAsync(req));
    }

    [Fact]
    public async Task EvaluateStockRebalanceAsync_ShouldGenerateTransferManifest_WhenStockBelowReserve()
    {
        var warehouses = await _service.GetWarehouseNodesAsync(_tenantId);
        var sfax = warehouses.First(w => w.Name.Contains("Sfax"));

        // Set Sfax stock of SKU-ELEC-01 to 3 (< 10 threshold)
        await _service.SetWarehouseStockAsync(new SetWarehouseStockRequest(_tenantId, sfax.WarehouseId, "SKU-ELEC-01", 3));

        var manifests = await _service.EvaluateStockRebalanceAsync(_tenantId, safetyThreshold: 10);

        manifests.Should().NotBeEmpty();
        var m = manifests.First(x => x.Sku == "SKU-ELEC-01");
        m.TargetWarehouseName.Should().Contain("Sfax");
        m.Quantity.Should().Be(25);
    }

    [Fact]
    public async Task GetMetricsAsync_ShouldTrackSplitRateAndDistance()
    {
        var req = new RouteOrderShipmentRequest(
            _tenantId, Guid.NewGuid(), 36.8, 10.1, "1000",
            new List<OrderItemRequest> { new("SKU-ELEC-01", "Earbuds", 1, 45.0m) }
        );
        await _service.RouteOrderShipmentAsync(req);

        var metrics = await _service.GetMetricsAsync(_tenantId);

        metrics.TotalOrdersRouted.Should().Be(1);
        metrics.AverageShippingCost.Should().BeGreaterThan(0.0m);
    }
}
