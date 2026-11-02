using SGrowing.Api.Modules.Commerce.FulfillmentMatrix;

namespace SGrowing.Tests.Commerce;

public class FulfillmentMatrixTests
{
    private readonly IFulfillmentEngine _engine = new FulfillmentEngine();

    [Fact]
    public void ListWarehouses_ReturnsConfiguredDistributionHubs()
    {
        var warehouses = _engine.ListWarehouses();

        Assert.NotEmpty(warehouses);
        Assert.Equal(4, warehouses.Count);
        Assert.Contains(warehouses, w => w.Code == "EWR-01");
        Assert.Contains(warehouses, w => w.Code == "FRA-01");
    }

    [Fact]
    public void PlanOrderFulfillment_RoutesToClosestRegionalWarehouseWithoutSplits()
    {
        // Destination: Brooklyn, NY (Lat 40.6782, Lon -73.9442) - close to Newark EWR-01
        var request = new PlanFulfillmentRequest(
            OrderId: "ord_enterprise_9981",
            DestinationCountry: "US",
            DestLatitude: 40.6782,
            DestLongitude: -73.9442,
            Items: new List<FulfillmentLineItem>
            {
                new("SKU_GW_VOICE", "Edge Voice Gateway", 2, 450.00m)
            }
        );

        var plan = _engine.PlanOrderFulfillment(request);

        Assert.NotNull(plan);
        Assert.Single(plan.Packages);
        Assert.Equal("wh_us_east", plan.Packages[0].WarehouseId);
        Assert.False(plan.IsSplitShipment);
        Assert.True(plan.Packages[0].DistanceKm < 50.0);
    }

    [Fact]
    public void ListTransferRecommendations_IdentifiesSafetyStockDeficits()
    {
        var transfers = _engine.ListTransferRecommendations();

        Assert.NotEmpty(transfers);
        Assert.Contains(transfers, t => t.ToWarehouseId == "wh_us_west");
        Assert.True(transfers[0].RecommendedUnits > 0);
    }
}
