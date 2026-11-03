using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Advertising.RetailMediaAuction;

public enum AuctionSlotType
{
    SearchTopBanner = 0,
    ProductGridSponsoredListing = 1,
    CheckoutCrossSellWidget = 2
}

public record MerchantAdBid(
    string BidId,
    string MerchantName,
    string ProductSku,
    string TargetKeyword,
    decimal MaxCpcBid,
    double EstimatedCtr,
    double RelevanceScore
);

public record AuctionResult(
    string AuctionId,
    string SlotType,
    string TargetKeyword,
    string WinningMerchantName,
    string WinningSku,
    decimal WinningBidAmount,
    decimal ClearingPrice, // GSP Second price + $0.01
    double ComputedRankScore,
    DateTime ExecutedAt
);

public record RetailMediaOverview(
    int TotalAuctionsExecuted24h,
    decimal TotalAdRevenueGenerated,
    decimal AverageClearingCpc,
    double AverageMerchantRoas,
    List<AuctionResult> RecentAuctions
);

public record RunAuctionCommand(
    string SlotType,
    string TargetKeyword,
    List<MerchantAdBid> Bids
);
