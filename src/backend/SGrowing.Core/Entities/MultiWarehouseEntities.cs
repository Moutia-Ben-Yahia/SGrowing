namespace SGrowing.Core.Entities;

public enum InterHubTransferStatus
{
    Draft = 1,
    InTransit = 2,
    Received = 3,
    Cancelled = 4
}

public class RegionalHubProximityRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public string GovernorateOrWilaya { get; set; } = string.Empty;
    public int Priority { get; set; } = 1; // 1 = highest priority
    public bool IsActive { get; set; } = true;

    public static RegionalHubProximityRule Create(Guid tenantId, Guid warehouseId, string warehouseName, string governorate, int priority = 1)
    {
        return new RegionalHubProximityRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WarehouseId = warehouseId,
            WarehouseName = warehouseName.Trim(),
            GovernorateOrWilaya = governorate.Trim(),
            Priority = priority,
            IsActive = true
        };
    }
}

public class InterHubStockTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid SourceWarehouseId { get; set; }
    public string SourceWarehouseName { get; set; } = string.Empty;
    public Guid DestinationWarehouseId { get; set; }
    public string DestinationWarehouseName { get; set; } = string.Empty;
    public InterHubTransferStatus Status { get; set; } = InterHubTransferStatus.Draft;
    public string ItemsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DispatchedAtUtc { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }

    public static InterHubStockTransfer Create(
        Guid tenantId,
        Guid sourceWarehouseId,
        string sourceName,
        Guid destWarehouseId,
        string destName,
        string itemsJson)
    {
        return new InterHubStockTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TransferNumber = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}",
            SourceWarehouseId = sourceWarehouseId,
            SourceWarehouseName = sourceName.Trim(),
            DestinationWarehouseId = destWarehouseId,
            DestinationWarehouseName = destName.Trim(),
            Status = InterHubTransferStatus.Draft,
            ItemsJson = itemsJson,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class SupplierPurchaseOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public Guid DestinationWarehouseId { get; set; }
    public decimal TotalCost { get; set; }
    public string Currency { get; set; } = "TND";
    public string Status { get; set; } = "Draft"; // "Draft", "Approved", "Received"
    public string ItemsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReceivedAtUtc { get; set; }
}

public record ProximityAllocationResult(Guid WarehouseId, string WarehouseName, int AvailableStock, string MatchReason);
