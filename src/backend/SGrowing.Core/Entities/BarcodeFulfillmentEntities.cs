namespace SGrowing.Core.Entities;

public enum PickSlipStatus
{
    Pending = 1,
    Picking = 2,
    Picked = 3,
    Packed = 4,
    Dispatched = 5
}

public enum ManifestStatus
{
    Draft = 1,
    Closed = 2,
    HandedOverToDriver = 3
}

public enum RmaItemCondition
{
    IntactRestock = 1,
    DamagedWriteoff = 2,
    WrongItemReturned = 3
}

public class WarehousePickSlip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string PickSlipNumber { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string? AssignedPickerId { get; set; }
    public PickSlipStatus Status { get; set; } = PickSlipStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public List<PickSlipItem> Items { get; set; } = new();

    public static WarehousePickSlip Create(Guid tenantId, Guid orderId, string orderNumber, Guid warehouseId)
    {
        return new WarehousePickSlip
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            OrderNumber = orderNumber,
            WarehouseId = warehouseId,
            PickSlipNumber = $"PICK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            Status = PickSlipStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class PickSlipItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PickSlipId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public int QuantityRequested { get; set; } = 1;
    public int QuantityPicked { get; set; } = 0;
    public bool IsVerified => QuantityPicked >= QuantityRequested;
}

public class CourierManifestRunSheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public string ManifestReference { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? DriverSignatureData { get; set; }
    public int TotalParcels { get; set; }
    public decimal TotalCodValue { get; set; }
    public ManifestStatus Status { get; set; } = ManifestStatus.Draft;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? HandedOverAtUtc { get; set; }
    public List<ManifestParcelItem> Parcels { get; set; } = new();

    public static CourierManifestRunSheet Create(Guid tenantId, string courierName)
    {
        return new CourierManifestRunSheet
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CourierName = courierName,
            ManifestReference = $"MAN-{courierName.ToUpperInvariant()[..Math.Min(3, courierName.Length)]}-{DateTime.UtcNow:yyyyMMdd-HHmm}",
            Status = ManifestStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class ManifestParcelItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ManifestId { get; set; }
    public Guid OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Governorate { get; set; } = string.Empty;
    public decimal CodAmount { get; set; }
    public DateTime ScannedAtUtc { get; set; } = DateTime.UtcNow;
}

public class WarehouseRmaInspection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string InspectorId { get; set; } = string.Empty;
    public RmaItemCondition Condition { get; set; } = RmaItemCondition.IntactRestock;
    public string? InspectorNotes { get; set; }
    public Guid? RestockedToWarehouseId { get; set; }
    public DateTime InspectedAtUtc { get; set; } = DateTime.UtcNow;
}
