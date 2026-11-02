using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Commerce.FulfillmentMatrix;

public interface IFulfillmentEngine
{
    List<WarehouseFacility> ListWarehouses();
    OrderFulfillmentPlan PlanOrderFulfillment(PlanFulfillmentRequest request);
    List<StockTransferRecommendation> ListTransferRecommendations();
}

public class FulfillmentEngine : IFulfillmentEngine
{
    private readonly ConcurrentDictionary<string, WarehouseFacility> _warehouses = new();
    private readonly ConcurrentBag<OrderFulfillmentPlan> _plans = new();

    public FulfillmentEngine()
    {
        SeedWarehouses();
    }

    private void SeedWarehouses()
    {
        var whList = new[]
        {
            new WarehouseFacility("wh_us_east", "EWR-01", "Newark Regional Hub", 40.7357, -74.1724, "US", 18500, 1000),
            new WarehouseFacility("wh_us_west", "RNO-02", "Reno Pacific Distribution", 39.5296, -119.8138, "US", 14200, 800),
            new WarehouseFacility("wh_eu_central", "FRA-01", "Frankfurt Continental Hub", 50.1109, 8.6821, "DE", 22000, 1500),
            new WarehouseFacility("wh_apac", "SIN-01", "Singapore Southeast Gateway", 1.3521, 103.8198, "SG", 16800, 1200)
        };

        foreach (var wh in whList)
        {
            _warehouses[wh.WarehouseId] = wh;
        }
    }

    public List<WarehouseFacility> ListWarehouses() =>
        _warehouses.Values.OrderBy(w => w.Code).ToList();

    public OrderFulfillmentPlan PlanOrderFulfillment(PlanFulfillmentRequest request)
    {
        // Calculate geodetic distances to all warehouses
        var ranked = _warehouses.Values
            .Select(wh => new
            {
                Warehouse = wh,
                DistanceKm = CalculateHaversineDistance(request.DestLatitude, request.DestLongitude, wh.Latitude, wh.Longitude)
            })
            .OrderBy(x => x.DistanceKm)
            .ToList();

        // Single box fulfillment heuristic: select closest warehouse
        var best = ranked.First();
        var estShipping = Math.Round(12.50m + ((decimal)best.DistanceKm * 0.008m), 2);

        var package = new FulfillmentPackage(
            PackageId: "pkg_" + Guid.NewGuid().ToString("N")[..8],
            WarehouseId: best.Warehouse.WarehouseId,
            WarehouseName: best.Warehouse.Name,
            Items: request.Items,
            DistanceKm: Math.Round(best.DistanceKm, 1),
            EstimatedShippingUsd: estShipping
        );

        var plan = new OrderFulfillmentPlan(
            PlanId: "plan_" + Guid.NewGuid().ToString("N")[..8],
            OrderId: request.OrderId,
            DestinationCountry: request.DestinationCountry,
            DestLatitude: request.DestLatitude,
            DestLongitude: request.DestLongitude,
            Packages: new List<FulfillmentPackage> { package },
            IsSplitShipment: false,
            TotalShippingUsd: estShipping,
            PlannedAtUtc: DateTime.UtcNow
        );

        _plans.Add(plan);
        return plan;
    }

    public List<StockTransferRecommendation> ListTransferRecommendations()
    {
        return new List<StockTransferRecommendation>
        {
            new(
                Sku: "SKU_HARDWARE_VOICE_GATEWAY_V2",
                FromWarehouseId: "wh_us_east",
                ToWarehouseId: "wh_us_west",
                RecommendedUnits: 450,
                Reason: "US-West regional velocity surging +35% WoW due to Silicon Valley enterprise campaigns."
            ),
            new(
                Sku: "SKU_EDGE_SDR_KEY_BOX",
                FromWarehouseId: "wh_eu_central",
                ToWarehouseId: "wh_apac",
                RecommendedUnits: 200,
                Reason: "Singapore stock below 1,200 unit safety buffer."
            )
        };
    }

    private static double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0; // Earth radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
