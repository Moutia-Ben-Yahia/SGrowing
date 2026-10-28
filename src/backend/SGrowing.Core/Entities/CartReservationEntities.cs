namespace SGrowing.Core.Entities;

public class CartReservationLockRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public Guid CartId { get; set; }
    public Guid? OrderId { get; set; }
    public string Status { get; set; } = "Active"; // Active, Committed, Released, Expired
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }
}

public record AcquireCartReservationRequest(
    Guid WarehouseId,
    string Sku,
    int Quantity,
    Guid CartId,
    int TtlMinutes = 15
);

public record AcquireCartReservationResult(
    Guid ReservationId,
    Guid CartId,
    string Sku,
    int Quantity,
    string Status,
    DateTime ExpiresAtUtc,
    bool Success,
    string Message
);

public record ReleaseCartReservationRequest(
    Guid CartId,
    string Reason = "AbandonedCart"
);

public record SweepExpiredReservationsResult(
    int TotalExpiredFound,
    int StockReturnedCount,
    DateTime SweptAtUtc
);
