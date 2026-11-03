using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.CtvAdInsertion;

public interface ICtvAdInsertionEngine
{
    CtvAdInsertionOverview GetOverview();
    CtvBidResponse ProcessBidStream(ProcessBidStreamCommand command);
    AttributeScanResult AttributeScan(AttributeScanCommand command);
}

public class CtvAdInsertionEngine : ICtvAdInsertionEngine
{
    private readonly List<CtvAdPod> _pods =
    [
        new("POD-ROKU-881", "Bloomberg TV+", "Market Open Live", 30, 24.50m, 6, "QR-SESS-9921", "Active"),
        new("POD-APPLE-402", "Paramount+ Sports", "Champions League Pre-Show", 15, 32.00m, 8, "QR-SESS-8814", "Streaming"),
        new("POD-FIRE-519", "Peacock News", "Global Tech Hour", 30, 21.00m, 5, "QR-SESS-7731", "Active"),
        new("POD-TIZEN-109", "Samsung TV Plus", "Automotive Weekly", 15, 18.50m, 4, "QR-SESS-6612", "Filled")
    ];

    private readonly List<QrScanAttributionEvent> _attributions =
    [
        new("ATTR-901", "QR-SESS-9921", "BCST-NYC-2026", "iOS", DateTime.UtcNow.AddMinutes(-12), 480.00m, "AttributedPurchase"),
        new("ATTR-902", "QR-SESS-8814", "BCST-LON-2026", "Android", DateTime.UtcNow.AddMinutes(-4), 1250.00m, "AttributedPurchase"),
        new("ATTR-903", "QR-SESS-7731", "BCST-SFO-2026", "iOS", DateTime.UtcNow.AddMinutes(-1), 180.00m, "PendingCheckout")
    ];

    public CtvAdInsertionOverview GetOverview()
    {
        var activePods = _pods.Count;
        var avgCpm = Math.Round(_pods.Average(p => p.FloorCpm), 2);
        var impressions = 425000;
        var conversionRate = 4.85;

        return new CtvAdInsertionOverview(
            activePods,
            avgCpm,
            impressions,
            conversionRate,
            _pods,
            _attributions
        );
    }

    public CtvBidResponse ProcessBidStream(ProcessBidStreamCommand command)
    {
        var pod = _pods.FirstOrDefault(p => p.PodId == command.PodId) ?? _pods[0];
        var isCleared = command.BidCpm >= pod.FloorCpm;
        var clearingStatus = isCleared ? "Cleared" : "Outbid";
        var bidId = $"BID-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var vastTag = $"https://ads.sgrowing.io/vast/v42/creative?bid={bidId}&pod={pod.PodId}";

        return new CtvBidResponse(
            bidId,
            $"REQ-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            command.AdvertiserId,
            command.CampaignName,
            command.BidCpm,
            vastTag,
            command.EnableQrWatermark,
            clearingStatus
        );
    }

    public AttributeScanResult AttributeScan(AttributeScanCommand command)
    {
        var attrId = $"ATTR-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var matchedPod = _pods.FirstOrDefault(p => p.QrCodeSessionId == command.QrCodeSessionId) ?? _pods[0];

        return new AttributeScanResult(
            attrId,
            command.QrCodeSessionId,
            matchedPod.PodId,
            $"{matchedPod.ChannelName} CTV Ad Pod",
            command.CheckoutAmount,
            $"RECEIPT-CTV-QR-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}"
        );
    }
}
