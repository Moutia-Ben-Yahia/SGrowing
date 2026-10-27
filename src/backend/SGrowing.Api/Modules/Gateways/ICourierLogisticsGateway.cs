using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Gateways;

public record CourierLabelRequestDto(
    string OrderNumber,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddress,
    string City,
    string Governorate,
    decimal CodAmountToCollect,
    string Currency,
    decimal WeightKg
);

public record CourierLabelResultDto(
    string CourierName,
    string TrackingNumber,
    string BarcodeData,
    string LabelPdfUrl,
    decimal EstimatedShippingFee,
    DateTime CreatedAtUtc
);

public record CourierTrackingWebhookPayloadDto(
    string TrackingNumber,
    string RawStatusCode,
    string Location,
    decimal? CollectedAmount,
    string? FailureReason,
    DateTime TimestampUtc
);

public record NormalizedTrackingStatusDto(
    string TrackingNumber,
    CourierShipmentStatus NormalizedStatus,
    decimal? CollectedAmount,
    string Message,
    DateTime TimestampUtc
);

public interface ICourierLogisticsGateway
{
    Task<CourierLabelResultDto> GenerateShipmentLabelAsync(string courierProviderName, CourierLabelRequestDto request, CancellationToken ct = default);
    Task<NormalizedTrackingStatusDto> TranslateWebhookEventAsync(string courierProviderName, CourierTrackingWebhookPayloadDto payload);
}

public class CourierLogisticsGateway : ICourierLogisticsGateway
{
    public Task<CourierLabelResultDto> GenerateShipmentLabelAsync(string courierProviderName, CourierLabelRequestDto request, CancellationToken ct = default)
    {
        var cleanCourier = courierProviderName.ToLowerInvariant();
        string prefix = "TRK";
        decimal fee = 8.5m;

        if (cleanCourier.Contains("yalidine"))
        {
            prefix = "YAL";
            fee = 7.5m;
        }
        else if (cleanCourier.Contains("cathedis"))
        {
            prefix = "CATH";
            fee = 8.0m;
        }
        else if (cleanCourier.Contains("dhl"))
        {
            prefix = "DHL";
            fee = 24.0m;
        }

        var trackingNumber = $"{prefix}-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(100000, 999999)}";
        var barcode = $"*{trackingNumber}*";
        var labelUrl = $"https://cdn.inspire.internal/labels/{trackingNumber}.pdf";

        return Task.FromResult(new CourierLabelResultDto(
            courierProviderName,
            trackingNumber,
            barcode,
            labelUrl,
            fee,
            DateTime.UtcNow
        ));
    }

    public Task<NormalizedTrackingStatusDto> TranslateWebhookEventAsync(string courierProviderName, CourierTrackingWebhookPayloadDto payload)
    {
        var raw = (payload.RawStatusCode ?? "").ToLowerInvariant().Trim();
        CourierShipmentStatus normalized = CourierShipmentStatus.PickedUp;
        string message = "Shipment update processed";

        if (raw.Contains("deliver") || raw.Contains("livre") || raw.Contains("recu") || raw == "completed")
        {
            normalized = CourierShipmentStatus.Delivered;
            message = $"Delivered in {payload.Location}. Cash collected: {payload.CollectedAmount:F2}";
        }
        else if (raw.Contains("transit") || raw.Contains("acheminement") || raw.Contains("hub"))
        {
            normalized = CourierShipmentStatus.InTransit;
            message = $"In Transit at {payload.Location}";
        }
        else if (raw.Contains("out") || raw.Contains("distribution") || raw.Contains("en cours"))
        {
            normalized = CourierShipmentStatus.OutForDelivery;
            message = $"Out for delivery with driver in {payload.Location}";
        }
        else if (raw.Contains("fail") || raw.Contains("echec") || raw.Contains("absent") || raw.Contains("reporte"))
        {
            normalized = CourierShipmentStatus.FailedAttempt;
            message = $"Delivery attempt failed: {payload.FailureReason ?? "Customer unavailable"}";
        }
        else if (raw.Contains("return") || raw.Contains("retour") || raw.Contains("refus"))
        {
            normalized = CourierShipmentStatus.ReturnedToSender;
            message = $"Return to origin processed: {payload.FailureReason ?? "Refused by customer"}";
        }

        return Task.FromResult(new NormalizedTrackingStatusDto(
            payload.TrackingNumber,
            normalized,
            payload.CollectedAmount,
            message,
            payload.TimestampUtc
        ));
    }
}
