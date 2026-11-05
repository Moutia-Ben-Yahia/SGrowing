using System;

namespace SGrowing.Core.Entities;

/// <summary>
/// Courier field delivery reconciliation record supporting offline PWA sync.
/// Schema: commerce
/// </summary>
public class CourierDeliveryRecordEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ParcelId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string CourierDriverId { get; set; } = string.Empty;
    public string WilayaCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Delivered"; // "Delivered", "FailedAttempt", "Returned"
    public decimal CashCollected { get; set; }
    public string? CustomerSignatureHash { get; set; }
    public DateTime RecordedAtUtc { get; set; }
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsOfflineSync { get; set; } = true;
}
