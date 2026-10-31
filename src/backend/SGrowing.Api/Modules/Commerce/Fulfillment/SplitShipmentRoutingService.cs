using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Commerce.Fulfillment;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FulfillmentStrategy
{
    SingleLowestCost,
    SingleFastestDelivery,
    SplitConsolidated,
    ProximityNearestWarehouse
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShipmentStatus
{
    PendingRouting,
    Allocated,
    SplitDispatched,
    PackedReady,
    InTransit,
    Delivered
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WarehouseNodeType
{
    CentralHub,
    RegionalFulfillmentCenter,
    UrbanMicroHub,
    DropShipper
}

#endregion

#region Models & DTOs

public record WarehouseNode(
    Guid WarehouseId,
    Guid TenantId,
    string Name,
    string PostalCode,
    double Latitude,
    double Longitude,
    WarehouseNodeType Type,
    int DailyCapacityOrders,
    bool IsActive,
    ConcurrentDictionary<string, int> InventoryStock
);

public record OrderItemRequest(
    string Sku,
    string Title,
    int Quantity,
    decimal UnitPrice
);

public record OrderItemAllocation(
    string Sku,
    string Title,
    int Quantity,
    Guid AssignedWarehouseId,
    string AssignedWarehouseName,
    double DistanceKm,
    decimal EstimatedShippingCost
);

public record ShipmentPackage(
    Guid PackageId,
    Guid WarehouseId,
    string WarehouseName,
    List<OrderItemAllocation> Items,
    decimal CarrierTariff,
    int EstimatedTransitDays,
    string TrackingCode
);

public record SplitShipmentRoutingResult(
    Guid OrderId,
    Guid TenantId,
    FulfillmentStrategy StrategyUsed,
    bool IsSplitShipment,
    int TotalPackagesCount,
    decimal TotalShippingCost,
    List<ShipmentPackage> Packages,
    DateTime EvaluatedAtUtc
);

public record StockTransferManifest(
    Guid TransferId,
    Guid TenantId,
    Guid SourceWarehouseId,
    string SourceWarehouseName,
    Guid TargetWarehouseId,
    string TargetWarehouseName,
    string Sku,
    int Quantity,
    string Reason,
    DateTime CreatedAtUtc
);

public record CreateWarehouseNodeRequest(
    Guid TenantId,
    string Name,
    string PostalCode,
    double Latitude,
    double Longitude,
    WarehouseNodeType Type,
    int DailyCapacityOrders = 500
);

public record SetWarehouseStockRequest(
    Guid TenantId,
    Guid WarehouseId,
    string Sku,
    int Quantity
);

public record RouteOrderShipmentRequest(
    Guid TenantId,
    Guid OrderId,
    double DestinationLatitude,
    double DestinationLongitude,
    string DestinationPostalCode,
    List<OrderItemRequest> Items,
    FulfillmentStrategy Strategy = FulfillmentStrategy.ProximityNearestWarehouse
);

public record FulfillmentMetrics(
    Guid TenantId,
    int TotalOrdersRouted,
    int SplitShipmentsCount,
    double SplitShipmentRatePercent,
    decimal AverageShippingCost,
    double AverageDistanceKm
);

#endregion

public interface ISplitShipmentRoutingService
{
    Task<WarehouseNode> CreateWarehouseNodeAsync(CreateWarehouseNodeRequest request, CancellationToken ct = default);
    Task<List<WarehouseNode>> GetWarehouseNodesAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> SetWarehouseStockAsync(SetWarehouseStockRequest request, CancellationToken ct = default);

    Task<SplitShipmentRoutingResult> RouteOrderShipmentAsync(RouteOrderShipmentRequest request, CancellationToken ct = default);
    Task<List<StockTransferManifest>> EvaluateStockRebalanceAsync(Guid tenantId, int safetyThreshold = 10, CancellationToken ct = default);
    Task<FulfillmentMetrics> GetMetricsAsync(Guid tenantId, CancellationToken ct = default);
}

public class SplitShipmentRoutingService : ISplitShipmentRoutingService
{
    private readonly ConcurrentDictionary<Guid, WarehouseNode> _warehouses = new();
    private readonly ConcurrentDictionary<Guid, SplitShipmentRoutingResult> _routedOrders = new();
    private readonly ConcurrentDictionary<Guid, StockTransferManifest> _transfers = new();

    public SplitShipmentRoutingService()
    {
        // Default seed: Central Hub in Tunis, Regional in Sfax
        SeedDefaultWarehouses(Guid.Empty);
    }

    private void SeedDefaultWarehouses(Guid tenantId)
    {
        var w1 = new WarehouseNode(
            Guid.NewGuid(), tenantId, "Tunis Central Mega-Hub", "1000", 36.8065, 10.1815,
            WarehouseNodeType.CentralHub, 2000, true, new ConcurrentDictionary<string, int>()
        );
        w1.InventoryStock["SKU-ELEC-01"] = 100;
        w1.InventoryStock["SKU-FASH-02"] = 150;

        var w2 = new WarehouseNode(
            Guid.NewGuid(), tenantId, "Sfax Regional Hub", "3000", 34.7406, 10.7603,
            WarehouseNodeType.RegionalFulfillmentCenter, 800, true, new ConcurrentDictionary<string, int>()
        );
        w2.InventoryStock["SKU-ELEC-01"] = 15;
        w2.InventoryStock["SKU-HOME-03"] = 80;

        _warehouses[w1.WarehouseId] = w1;
        _warehouses[w2.WarehouseId] = w2;
    }

    public Task<WarehouseNode> CreateWarehouseNodeAsync(CreateWarehouseNodeRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Name is required.", nameof(request));

        var node = new WarehouseNode(
            Guid.NewGuid(),
            request.TenantId,
            request.Name.Trim(),
            request.PostalCode?.Trim() ?? "0000",
            request.Latitude,
            request.Longitude,
            request.Type,
            request.DailyCapacityOrders > 0 ? request.DailyCapacityOrders : 500,
            true,
            new ConcurrentDictionary<string, int>()
        );

        _warehouses[node.WarehouseId] = node;
        return Task.FromResult(node);
    }

    public Task<List<WarehouseNode>> GetWarehouseNodesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _warehouses.Values
            .Where(w => (w.TenantId == tenantId || w.TenantId == Guid.Empty) && w.IsActive)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<bool> SetWarehouseStockAsync(SetWarehouseStockRequest request, CancellationToken ct = default)
    {
        if (_warehouses.TryGetValue(request.WarehouseId, out var node))
        {
            node.InventoryStock[request.Sku.Trim()] = request.Quantity;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<SplitShipmentRoutingResult> RouteOrderShipmentAsync(RouteOrderShipmentRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (request.Items == null || request.Items.Count == 0)
        {
            throw new ArgumentException("At least one item is required to route shipment.", nameof(request));
        }

        var availableWarehouses = _warehouses.Values
            .Where(w => (w.TenantId == request.TenantId || w.TenantId == Guid.Empty) && w.IsActive)
            .ToList();

        if (availableWarehouses.Count == 0)
        {
            throw new InvalidOperationException("No active warehouses configured for fulfillment.");
        }

        // Sort warehouses by geographic proximity to buyer destination
        var sortedByDistance = availableWarehouses
            .Select(w => new
            {
                Warehouse = w,
                DistanceKm = CalculateDistanceKm(w.Latitude, w.Longitude, request.DestinationLatitude, request.DestinationLongitude)
            })
            .OrderBy(x => x.DistanceKm)
            .ToList();

        // 1. Check if a single nearest warehouse can fulfill 100% of the order items
        var singleFulfilled = sortedByDistance.FirstOrDefault(cand =>
            request.Items.All(item => cand.Warehouse.InventoryStock.TryGetValue(item.Sku, out int stock) && stock >= item.Quantity)
        );

        var packages = new List<ShipmentPackage>();
        bool isSplit = false;

        if (singleFulfilled != null && request.Strategy != FulfillmentStrategy.SplitConsolidated)
        {
            // Fulfill entirely from single warehouse
            var pkgItems = new List<OrderItemAllocation>();
            foreach (var it in request.Items)
            {
                // Decrement stock
                singleFulfilled.Warehouse.InventoryStock.AddOrUpdate(it.Sku, 0, (_, current) => Math.Max(0, current - it.Quantity));

                pkgItems.Add(new OrderItemAllocation(
                    it.Sku,
                    it.Title,
                    it.Quantity,
                    singleFulfilled.Warehouse.WarehouseId,
                    singleFulfilled.Warehouse.Name,
                    singleFulfilled.DistanceKm,
                    8.50m
                ));
            }

            var pkg = new ShipmentPackage(
                Guid.NewGuid(),
                singleFulfilled.Warehouse.WarehouseId,
                singleFulfilled.Warehouse.Name,
                pkgItems,
                8.50m,
                singleFulfilled.DistanceKm < 50 ? 1 : 2,
                $"TRK-{Random.Shared.Next(100000, 999999)}"
            );
            packages.Add(pkg);
        }
        else
        {
            // Split-Shipment fulfillment across closest available warehouses
            isSplit = true;
            var remainingItems = request.Items.ToDictionary(i => i.Sku, i => (Item: i, Remaining: i.Quantity));

            foreach (var cand in sortedByDistance)
            {
                var allocatedHere = new List<OrderItemAllocation>();

                foreach (var kvp in remainingItems.ToList())
                {
                    if (kvp.Value.Remaining <= 0) continue;

                    if (cand.Warehouse.InventoryStock.TryGetValue(kvp.Key, out int currentStock) && currentStock > 0)
                    {
                        int toTake = Math.Min(kvp.Value.Remaining, currentStock);
                        cand.Warehouse.InventoryStock[kvp.Key] = currentStock - toTake;
                        remainingItems[kvp.Key] = (kvp.Value.Item, kvp.Value.Remaining - toTake);

                        allocatedHere.Add(new OrderItemAllocation(
                            kvp.Value.Item.Sku,
                            kvp.Value.Item.Title,
                            toTake,
                            cand.Warehouse.WarehouseId,
                            cand.Warehouse.Name,
                            cand.DistanceKm,
                            7.00m
                        ));
                    }
                }

                if (allocatedHere.Count > 0)
                {
                    var pkg = new ShipmentPackage(
                        Guid.NewGuid(),
                        cand.Warehouse.WarehouseId,
                        cand.Warehouse.Name,
                        allocatedHere,
                        7.00m,
                        cand.DistanceKm < 50 ? 1 : 2,
                        $"SPLIT-{Random.Shared.Next(100000, 999999)}"
                    );
                    packages.Add(pkg);
                }

                if (remainingItems.Values.All(v => v.Remaining <= 0))
                {
                    break;
                }
            }
        }

        decimal totalShippingCost = packages.Sum(p => p.CarrierTariff);

        var result = new SplitShipmentRoutingResult(
            request.OrderId,
            request.TenantId,
            request.Strategy,
            isSplit,
            packages.Count,
            totalShippingCost,
            packages,
            DateTime.UtcNow
        );

        _routedOrders[result.OrderId] = result;
        return Task.FromResult(result);
    }

    public Task<List<StockTransferManifest>> EvaluateStockRebalanceAsync(Guid tenantId, int safetyThreshold = 10, CancellationToken ct = default)
    {
        var tenantWarehouses = _warehouses.Values
            .Where(w => (w.TenantId == tenantId || w.TenantId == Guid.Empty) && w.IsActive)
            .ToList();

        var central = tenantWarehouses.FirstOrDefault(w => w.Type == WarehouseNodeType.CentralHub);
        if (central == null) return Task.FromResult(new List<StockTransferManifest>());

        var newManifests = new List<StockTransferManifest>();

        var regional = tenantWarehouses.Where(w => w.Type != WarehouseNodeType.CentralHub);
        foreach (var reg in regional)
        {
            foreach (var kvp in reg.InventoryStock)
            {
                if (kvp.Value < safetyThreshold)
                {
                    // Check if central hub has stock
                    if (central.InventoryStock.TryGetValue(kvp.Key, out int centralStock) && centralStock > 20)
                    {
                        int transferQty = 25;
                        var manifest = new StockTransferManifest(
                            Guid.NewGuid(),
                            tenantId,
                            central.WarehouseId,
                            central.Name,
                            reg.WarehouseId,
                            reg.Name,
                            kvp.Key,
                            transferQty,
                            $"Automatic micro-hub rebalancing: stock ({kvp.Value}) below safety reserve ({safetyThreshold}).",
                            DateTime.UtcNow
                        );

                        _transfers[manifest.TransferId] = manifest;
                        newManifests.Add(manifest);
                    }
                }
            }
        }

        return Task.FromResult(newManifests);
    }

    public Task<FulfillmentMetrics> GetMetricsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var orders = _routedOrders.Values.Where(o => o.TenantId == tenantId).ToList();

        int total = orders.Count;
        int split = orders.Count(o => o.IsSplitShipment);
        double splitRate = total > 0 ? ((double)split / total) * 100.0 : 0.0;
        decimal avgCost = total > 0 ? orders.Average(o => o.TotalShippingCost) : 0.0m;

        var allDistances = orders
            .SelectMany(o => o.Packages)
            .SelectMany(p => p.Items)
            .Select(i => i.DistanceKm)
            .ToList();

        double avgDist = allDistances.Count > 0 ? allDistances.Average() : 0.0;

        return Task.FromResult(new FulfillmentMetrics(
            tenantId,
            total,
            split,
            Math.Round(splitRate, 1),
            Math.Round(avgCost, 2),
            Math.Round(avgDist, 1)
        ));
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(R * c, 1);
    }

    private static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
