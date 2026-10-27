namespace SGrowing.Api.Modules.Commerce.Shipping;

public enum CarrierStatus
{
    Created,
    PickedUp,
    InTransit,
    OutForDelivery,
    Delivered,
    FailedAttempt,
    Returned,
    Cancelled
}

public enum LabelFormat
{
    Pdf,
    Zpl,
    Png
}

public record WaybillGenerationRequest(
    string CourierCode, // "yalidine", "aramex", "chronopost", "colisprive", "oto", "bosta"
    Guid OrderId,
    string OrderNumber,
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    string DeliveryCity,
    string DeliveryGovernorateOrState,
    decimal CashToCollect,
    string Currency,
    double WeightKg,
    int PackageCount,
    bool AllowCustomerToOpenBeforePaying,
    string? MerchantNotes = null
);

public record WaybillGenerationResult(
    string CourierCode,
    string CourierName,
    string TrackingNumber,
    string AwbBarcode,
    string LabelUrl,
    string ManifestId,
    decimal DeclaredCashToCollect,
    CarrierStatus Status,
    DateTime DispatchedAtUtc
);

public record TrackingCheckpointDto(
    DateTime TimestampUtc,
    string Location,
    string StatusDescription,
    CarrierStatus StandardizedStatus
);

public record CarrierTrackingResult(
    string CourierCode,
    string TrackingNumber,
    CarrierStatus CurrentStatus,
    string StatusDescription,
    decimal CashCollected,
    DateTime? DeliveredAtUtc,
    List<TrackingCheckpointDto> Checkpoints
);

public record ShippingLabelResult(
    string TrackingNumber,
    LabelFormat Format,
    string LabelContentBase64OrUrl,
    int PageCount
);

public record CarrierCancelResult(
    string TrackingNumber,
    bool Success,
    string Message
);

public interface ICarrierShippingGateway
{
    Task<WaybillGenerationResult> GenerateWaybillAsync(Guid tenantId, WaybillGenerationRequest req, CancellationToken ct = default);
    Task<CarrierTrackingResult> TrackShipmentAsync(Guid tenantId, string courierCode, string trackingNumber, CancellationToken ct = default);
    Task<ShippingLabelResult> GenerateShippingLabelAsync(Guid tenantId, string courierCode, string trackingNumber, LabelFormat format = LabelFormat.Pdf, CancellationToken ct = default);
    Task<CarrierCancelResult> CancelShipmentAsync(Guid tenantId, string courierCode, string trackingNumber, CancellationToken ct = default);
}
