using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Advertising.RetailMediaAuction;

public interface IRetailMediaAuctionEngine
{
    RetailMediaOverview GetOverview();
    AuctionResult RunAuction(RunAuctionCommand command);
}

public class RetailMediaAuctionEngine : IRetailMediaAuctionEngine
{
    private readonly ConcurrentBag<AuctionResult> _auctions = new();

    public RetailMediaAuctionEngine()
    {
        SeedAuctions();
    }

    private void SeedAuctions()
    {
        var now = DateTime.UtcNow;

        var a1 = new AuctionResult(
            AuctionId: "AUC-2026-8001",
            SlotType: "SearchTopBanner",
            TargetKeyword: "wireless noise cancelling headphones",
            WinningMerchantName: "AuraAudio Labs",
            WinningSku: "SKU-AURA-NC700",
            WinningBidAmount: 3.50m,
            ClearingPrice: 2.85m,
            ComputedRankScore: 0.882,
            ExecutedAt: now.AddMinutes(-5)
        );

        var a2 = new AuctionResult(
            AuctionId: "AUC-2026-8002",
            SlotType: "ProductGridSponsoredListing",
            TargetKeyword: "ergonomic mechanical keyboard",
            WinningMerchantName: "KeyCraft Studio",
            WinningSku: "SKU-KEY-PRO-RGB",
            WinningBidAmount: 2.20m,
            ClearingPrice: 1.75m,
            ComputedRankScore: 0.748,
            ExecutedAt: now.AddMinutes(-12)
        );

        var a3 = new AuctionResult(
            AuctionId: "AUC-2026-8003",
            SlotType: "CheckoutCrossSellWidget",
            TargetKeyword: "usb c fast charging cable",
            WinningMerchantName: "VoltPower Tech",
            WinningSku: "SKU-VOLT-USB100W",
            WinningBidAmount: 1.80m,
            ClearingPrice: 1.35m,
            ComputedRankScore: 0.690,
            ExecutedAt: now.AddMinutes(-20)
        );

        _auctions.Add(a1);
        _auctions.Add(a2);
        _auctions.Add(a3);
    }

    public RetailMediaOverview GetOverview()
    {
        var list = _auctions.OrderByDescending(a => a.ExecutedAt).ToList();
        var totalRev = list.Sum(a => a.ClearingPrice * 1250); // Scaled by traffic clicks
        var avgCpc = list.Count > 0 ? list.Average(a => a.ClearingPrice) : 2.10m;

        return new RetailMediaOverview(
            TotalAuctionsExecuted24h: 18450,
            TotalAdRevenueGenerated: Math.Round(totalRev, 2),
            AverageClearingCpc: Math.Round(avgCpc, 2),
            AverageMerchantRoas: 5.4, // 5.4x ROAS on sponsored media spend
            RecentAuctions: list.Take(10).ToList()
        );
    }

    public AuctionResult RunAuction(RunAuctionCommand command)
    {
        if (command.Bids == null || command.Bids.Count == 0)
        {
            throw new ArgumentException("Auction requires at least one participating merchant bid.");
        }

        // Rank = MaxCpcBid * EstimatedCtr * RelevanceScore
        var ranked = command.Bids
            .Select(b => new
            {
                Bid = b,
                Rank = (double)b.MaxCpcBid * b.EstimatedCtr * b.RelevanceScore
            })
            .OrderByDescending(x => x.Rank)
            .ToList();

        var winner = ranked[0];
        decimal clearingPrice = winner.Bid.MaxCpcBid;

        if (ranked.Count > 1)
        {
            var second = ranked[1];
            // Generalized Second Price (GSP): secondRank / (winnerCtr * winnerRelevance) + 0.01
            var qualityDenominator = winner.Bid.EstimatedCtr * winner.Bid.RelevanceScore;
            if (qualityDenominator > 0.0001)
            {
                var computed = (decimal)(second.Rank / qualityDenominator) + 0.01m;
                clearingPrice = Math.Min(winner.Bid.MaxCpcBid, Math.Round(computed, 2));
            }
        }

        var result = new AuctionResult(
            AuctionId: $"AUC-2026-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            SlotType: command.SlotType,
            TargetKeyword: command.TargetKeyword,
            WinningMerchantName: winner.Bid.MerchantName,
            WinningSku: winner.Bid.ProductSku,
            WinningBidAmount: winner.Bid.MaxCpcBid,
            ClearingPrice: clearingPrice,
            ComputedRankScore: Math.Round(winner.Rank, 3),
            ExecutedAt: DateTime.UtcNow
        );

        _auctions.Add(result);
        return result;
    }
}
