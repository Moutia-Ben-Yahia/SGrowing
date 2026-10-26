namespace SGrowing.Core.Entities;

public enum StockMovementType
{
    Receipt = 1,
    Reservation = 2,
    CancellationRestock = 3,
    Commitment = 4,
    Dispatch = 5,
    CustomerDelivered = 6,
    CourierReturnQuarantine = 7,
    InspectionRestock = 8,
    DamageWriteoff = 9
}

public class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsPrimary { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class WarehouseInventory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    
    // Defensive Multi-State COD Model:
    // Physical = In-Warehouse actual inventory
    // Reserved = Placed orders waiting phone/WhatsApp confirmation
    // Committed = Confirmed orders packed & awaiting/manifested for courier
    // InTransit = Currently with courier on road
    // QuarantineReturned = Returned from courier, awaiting inspection/barcode scan
    // Damaged = Broken or unsellable
    public int PhysicalStock { get; set; }
    public int ReservedStock { get; set; }
    public int CommittedStock { get; set; }
    public int InTransitStock { get; set; }
    public int QuarantineReturnedStock { get; set; }
    public int DamagedStock { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

    public int AvailableStock => Math.Max(0, PhysicalStock - ReservedStock - CommittedStock);
}

public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public Guid? OrderId { get; set; }
    public StockMovementType MovementType { get; set; }
    public int QuantityChange { get; set; }
    public int PreviousPhysical { get; set; }
    public int NewPhysical { get; set; }
    public string PerformedBy { get; set; } = "System";
    public string? Notes { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
