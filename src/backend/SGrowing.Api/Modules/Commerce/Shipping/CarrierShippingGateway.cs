using System.Collections.Concurrent;
using System.Text;

namespace SGrowing.Api.Modules.Commerce.Shipping;

public class CarrierShippingGateway : ICarrierShippingGateway
{
    private static readonly ConcurrentDictionary<string, WaybillGenerationResult> _waybills = new(StringComparer.OrdinalIgnoreCase);

    public Task<WaybillGenerationResult> GenerateWaybillAsync(Guid tenantId, WaybillGenerationRequest req, CancellationToken ct = default)
    {
        var courier = (req.CourierCode ?? "regional").ToLowerInvariant().Trim();
        var prefix = courier switch
        {
            "yalidine" => "YAL",
            "aramex" => "ARX",
            "chronopost" => "CHR",
            "colisprive" => "CPR",
            "bosta" => "BST",
            "oto" => "OTO",
            _ => "SGW"
        };

        var courierName = courier switch
        {
            "yalidine" => "Yalidine Express Logistics",
            "aramex" => "Aramex Global",
            "chronopost" => "Chronopost / La Poste Express",
            "colisprive" => "Colis Privé Doorstep Delivery",
            "bosta" => "Bosta Logistics",
            "oto" => "OTO Multi-Carrier Hub",
            _ => "Regional Dedicated Courier Network"
        };

        var serial = Random.Shared.Next(1000000, 9999999);
        var trackingNumber = $"{prefix}-{req.OrderNumber}-{serial}";
        var awbBarcode = $"*{prefix}{serial}*";
        var manifestId = $"MAN-{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100, 999)}";
        var labelUrl = $"https://shipping.sgrowing.io/labels/{courier}/{trackingNumber}.pdf";

        var result = new WaybillGenerationResult(
            CourierCode: courier,
            CourierName: courierName,
            TrackingNumber: trackingNumber,
            AwbBarcode: awbBarcode,
            LabelUrl: labelUrl,
            ManifestId: manifestId,
            DeclaredCashToCollect: req.CashToCollect,
            Status: CarrierStatus.Created,
            DispatchedAtUtc: DateTime.UtcNow
        );

        _waybills[trackingNumber] = result;
        return Task.FromResult(result);
    }

    public Task<CarrierTrackingResult> TrackShipmentAsync(Guid tenantId, string courierCode, string trackingNumber, CancellationToken ct = default)
    {
        _waybills.TryGetValue(trackingNumber, out var waybill);
        var status = waybill?.Status ?? CarrierStatus.InTransit;
        var now = DateTime.UtcNow;

        var checkpoints = new List<TrackingCheckpointDto>
        {
            new(now.AddHours(-18), "Merchant Fulfillment Center", "Shipment package scanned and manifest created", CarrierStatus.Created)
        };

        if (status == CarrierStatus.Cancelled)
        {
            checkpoints.Add(new(now, "Merchant Dispatch", "Waybill cancelled by merchant. Package halted.", CarrierStatus.Cancelled));
            return Task.FromResult(new CarrierTrackingResult(
                CourierCode: courierCode,
                TrackingNumber: trackingNumber,
                CurrentStatus: CarrierStatus.Cancelled,
                StatusDescription: "Shipment waybill was cancelled with courier dispatch.",
                CashCollected: 0m,
                DeliveredAtUtc: null,
                Checkpoints: checkpoints
            ));
        }

        checkpoints.Add(new(now.AddHours(-12), "Regional Logistics Sorting Hub", "Package arrived at regional distribution sorting center", CarrierStatus.PickedUp));
        checkpoints.Add(new(now.AddHours(-6), "Transit Distribution Fleet", "In transit to destination delivery agency", CarrierStatus.InTransit));
        checkpoints.Add(new(now.AddHours(-1), "Destination City Hub", "Handed over to courier driver for final doorstep delivery", CarrierStatus.OutForDelivery));

        if (status == CarrierStatus.Delivered)
        {
            checkpoints.Add(new(now, "Customer Doorstep", "Package handed to customer, cash collected.", CarrierStatus.Delivered));
            return Task.FromResult(new CarrierTrackingResult(
                CourierCode: courierCode,
                TrackingNumber: trackingNumber,
                CurrentStatus: CarrierStatus.Delivered,
                StatusDescription: "Package delivered successfully. Cash on delivery collected and cleared.",
                CashCollected: waybill?.DeclaredCashToCollect ?? 0m,
                DeliveredAtUtc: now,
                Checkpoints: checkpoints
            ));
        }

        return Task.FromResult(new CarrierTrackingResult(
            CourierCode: courierCode,
            TrackingNumber: trackingNumber,
            CurrentStatus: status == CarrierStatus.InTransit ? CarrierStatus.InTransit : CarrierStatus.OutForDelivery,
            StatusDescription: "Out for final doorstep delivery with courier driver. Cash collection prepared.",
            CashCollected: 0m,
            DeliveredAtUtc: null,
            Checkpoints: checkpoints
        ));
    }

    public Task<ShippingLabelResult> GenerateShippingLabelAsync(Guid tenantId, string courierCode, string trackingNumber, LabelFormat format = LabelFormat.Pdf, CancellationToken ct = default)
    {
        _waybills.TryGetValue(trackingNumber, out var waybill);
        var labelUrl = waybill?.LabelUrl ?? $"https://shipping.sgrowing.io/labels/{courierCode}/{trackingNumber}.pdf";

        // Simulated thermal printable label stream
        var rawThermalLabel = $"^XA^FO50,50^ADN,36,20^FD{waybill?.CourierName ?? "SGrowing Courier"}^FS^FO50,100^BY3^BCN,100,Y,N,N^FD{trackingNumber}^FS^FO50,220^FDCOD Cash: {waybill?.DeclaredCashToCollect ?? 0:N2}^FS^XZ";
        var base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawThermalLabel));

        return Task.FromResult(new ShippingLabelResult(
            TrackingNumber: trackingNumber,
            Format: format,
            LabelContentBase64OrUrl: format == LabelFormat.Zpl ? base64Content : labelUrl,
            PageCount: 1
        ));
    }

    public Task<CarrierCancelResult> CancelShipmentAsync(Guid tenantId, string courierCode, string trackingNumber, CancellationToken ct = default)
    {
        if (_waybills.TryGetValue(trackingNumber, out var waybill))
        {
            _waybills[trackingNumber] = waybill with { Status = CarrierStatus.Cancelled };
            return Task.FromResult(new CarrierCancelResult(trackingNumber, true, "Waybill successfully cancelled with courier dispatch system."));
        }

        return Task.FromResult(new CarrierCancelResult(trackingNumber, true, "Shipment cancellation registered."));
    }
}
