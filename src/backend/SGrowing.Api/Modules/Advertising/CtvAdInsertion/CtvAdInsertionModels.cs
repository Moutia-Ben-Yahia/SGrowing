using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.CtvAdInsertion;

public record CtvAdPod(
    string PodId,
    string ChannelName,
    string ShowTitle,
    int AdDurationSeconds,
    decimal FloorCpm,
    int ActiveBiddersCount,
    string QrCodeSessionId,
    string Status // "Active", "Filled", "Streaming"
);

public record CtvBidRequest(
    string BidRequestId,
    string PodId,
    string PublisherId,
    string AppBundle,
    string DeviceMake, // "Roku", "AppleTV", "FireTV", "SamsungTizen"
    string GeoRegion,
    decimal FloorCpm
);

public record CtvBidResponse(
    string BidId,
    string BidRequestId,
    string AdvertiserId,
    string CampaignName,
    decimal CpmBid,
    string VastTagUrl,
    bool QrWatermarkEnabled,
    string ClearingStatus // "Cleared", "Outbid", "Rejected"
);

public record QrScanAttributionEvent(
    string EventId,
    string QrCodeSessionId,
    string BroadcastSessionId,
    string MobileDeviceType, // "iOS", "Android"
    DateTime ScanTimestamp,
    decimal AttributedCheckoutAmount,
    string ConversionStatus // "PendingCheckout", "AttributedPurchase", "LeadCaptured"
);

public record CtvAdInsertionOverview(
    int ActivePodsCount,
    decimal AverageClearingCpm,
    int TotalImpressionsToday,
    double QrScanConversionRatePct,
    IReadOnlyList<CtvAdPod> Pods,
    IReadOnlyList<QrScanAttributionEvent> RecentAttributions
);

public record ProcessBidStreamCommand(
    string PodId,
    string AdvertiserId,
    string CampaignName,
    decimal BidCpm,
    bool EnableQrWatermark
);

public record AttributeScanCommand(
    string QrCodeSessionId,
    string MobileDeviceType,
    decimal CheckoutAmount,
    string CustomerId
);

public record AttributeScanResult(
    string AttributionId,
    string QrCodeSessionId,
    string MatchedPodId,
    string AttributedCampaign,
    decimal AttributedRevenue,
    string AttributionMatchReceipt
);
