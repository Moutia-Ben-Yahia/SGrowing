namespace SGrowing.Core.Entities;

public enum ExchangeStatus
{
    Requested,
    Approved,
    DispatchedForSwap,
    SwapCompletedAtDoorstep,
    ReturnInspectedAndRestocked,
    Rejected,
    Canceled
}

public enum SwapInspectionResult
{
    PendingIntake,
    GradeA_RestockedSellable,
    GradeB_MinorFlawDiscounted,
    GradeC_DamagedWriteOff,
    Fraud_ItemMismatchOrEmpty
}

public class OrderExchangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid OriginalOrderId { get; set; }
    public string OriginalTrackingNumber { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Reason { get; set; } = "WrongSize"; // WrongSize, DefectiveItem, ColorPreference
    public Guid OriginalProductId { get; set; }
    public string OriginalSku { get; set; } = string.Empty;
    public string OriginalVariantDescription { get; set; } = string.Empty;
    public Guid ReplacementProductId { get; set; }
    public string ReplacementSku { get; set; } = string.Empty;
    public string ReplacementVariantDescription { get; set; } = string.Empty;
    public decimal AdditionalCashAdjustment { get; set; } = 0m; // If replacement costs more or swap fee charged
    public ExchangeStatus Status { get; set; } = ExchangeStatus.Requested;
    public Guid? SwapManifestId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}

public class ExchangeCourierSwapManifest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ExchangeRequestId { get; set; }
    public Guid CourierProviderId { get; set; }
    public string CourierName { get; set; } = string.Empty;
    public string SwapTrackingBarcode { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public string Governorate { get; set; } = string.Empty;
    public decimal BundledSwapDeliveryFee { get; set; } = 9.5m;
    public decimal CashToCollectOnDelivery { get; set; } = 0m;
    public bool HandoverNewParcelConfirmed { get; set; } = false;
    public bool ReturnParcelCollectedConfirmed { get; set; } = false;
    public DateTime DispatchedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAtUtc { get; set; }
}

public class ItemReturnInspection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ExchangeRequestId { get; set; }
    public string ReturnedSku { get; set; } = string.Empty;
    public SwapInspectionResult QualityGrade { get; set; } = SwapInspectionResult.PendingIntake;
    public string InspectorNotes { get; set; } = string.Empty;
    public bool RestockedToPhysicalWms { get; set; } = false;
    public DateTime InspectedAtUtc { get; set; } = DateTime.UtcNow;
}
