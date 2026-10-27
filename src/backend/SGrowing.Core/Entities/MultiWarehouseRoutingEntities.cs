namespace SGrowing.Core.Entities;

public class WarehouseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; } = true;
    public int CapacityUnits { get; set; } = 10000;
}

public class WarehouseStockRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public decimal UnitCost { get; set; }
}

public class StockTransferManifest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ManifestNumber { get; set; } = string.Empty;
    public Guid SourceWarehouseId { get; set; }
    public string SourceWarehouseName { get; set; } = string.Empty;
    public Guid DestinationWarehouseId { get; set; }
    public string DestinationWarehouseName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = "Draft"; // Draft, Dispatched, Received, Cancelled
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record OrderItemRequirement(
    string Sku,
    int Quantity);

public record RoutingRequest(
    Guid TenantId,
    string OrderId,
    string CustomerAddress,
    string CustomerCity,
    double CustomerLatitude,
    double CustomerLongitude,
    List<OrderItemRequirement> Items);

public record AllocatedItem(
    string Sku,
    int QuantityAllocated,
    Guid WarehouseId,
    string WarehouseName);

public record WarehouseRoutingResult(
    string OrderId,
    Guid SelectedWarehouseId,
    string SelectedWarehouseName,
    double DistanceKm,
    bool IsAllocated,
    List<AllocatedItem> AllocatedItems,
    List<StockTransferManifest> AutoTriggeredTransfers,
    string RoutingReason);

public record CreateWarehouseRequest(
    string Code,
    string Name,
    string Address,
    string City,
    string Country,
    double Latitude,
    double Longitude,
    int CapacityUnits = 10000);

public record CreateStockTransferRequest(
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    string Sku,
    int Quantity);
