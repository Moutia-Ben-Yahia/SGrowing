namespace SGrowing.Api.Modules.Finance.HftOrderBookMicrostructure;

public interface IHftOrderBookMicrostructureEngine
{
    MicrostructureEvaluationResult EvaluateMicrostructure(MicrostructureAnalysisRequest request);
}

public class HftOrderBookMicrostructureEngine : IHftOrderBookMicrostructureEngine
{
    private const decimal StandardMakerTakerFeeRate = 0.0001m; // 1 bps institutional tier fee rate

    public MicrostructureEvaluationResult EvaluateMicrostructure(MicrostructureAnalysisRequest request)
    {
        var books = request.VenueBooks ?? [];
        if (books.Count == 0)
        {
            return new MicrostructureEvaluationResult(
                AssetSymbol: request.AssetSymbol,
                ConsolidatedMidPrice: 0m,
                ConsolidatedMicroPrice: 0m,
                MicroPriceOffsetUsd: 0m,
                OrderFlowImbalance: 0m,
                KylesLambdaImpactUsd: 0m,
                ArbitrageSignals: [],
                BestExecutionRouting: "NO_VENUE_DATA: No active order books provided."
            );
        }

        // Consolidated Top of Book across all venues
        decimal bestBid = 0m;
        decimal bestBidQty = 0m;
        decimal bestAsk = decimal.MaxValue;
        decimal bestAskQty = 0m;
        string bestBidVenue = string.Empty;
        string bestAskVenue = string.Empty;

        decimal totalBidDepth = 0m;
        decimal totalAskDepth = 0m;

        foreach (var b in books)
        {
            var topBid = b.Bids.OrderByDescending(x => x.Price).FirstOrDefault();
            var topAsk = b.Asks.OrderBy(x => x.Price).FirstOrDefault();

            if (topBid is not null)
            {
                totalBidDepth += b.Bids.Sum(x => x.Quantity);
                if (topBid.Price > bestBid)
                {
                    bestBid = topBid.Price;
                    bestBidQty = topBid.Quantity;
                    bestBidVenue = b.VenueId;
                }
            }

            if (topAsk is not null)
            {
                totalAskDepth += b.Asks.Sum(x => x.Quantity);
                if (topAsk.Price < bestAsk)
                {
                    bestAsk = topAsk.Price;
                    bestAskQty = topAsk.Quantity;
                    bestAskVenue = b.VenueId;
                }
            }
        }

        if (bestAsk == decimal.MaxValue) bestAsk = bestBid;
        if (bestBid == 0m) bestBid = bestAsk;

        decimal midPrice = (bestBid + bestAsk) / 2m;
        decimal spread = Math.Max(0.0001m, bestAsk - bestBid);

        // Queue-imbalance micro-price: P_micro = P_mid + ((Q_bid - Q_ask) / (Q_bid + Q_ask)) * (spread / 2)
        decimal totalTopQueue = bestBidQty + bestAskQty;
        decimal queueImbalanceRatio = totalTopQueue > 0 ? (bestBidQty - bestAskQty) / totalTopQueue : 0m;
        decimal microPriceOffset = queueImbalanceRatio * (spread / 2m);
        decimal microPrice = midPrice + microPriceOffset;

        // Order Flow Imbalance (OFI) proxy across aggregate order books
        decimal ofi = totalBidDepth - totalAskDepth;

        // Kyle's Lambda permanent market impact: delta P = lambda * Q
        // Empirical lambda scaled by asset spread and book depth
        decimal effectiveDepth = Math.Max(1m, (totalBidDepth + totalAskDepth) / 2m);
        decimal kylesLambda = (spread / effectiveDepth) * 0.45m;
        decimal estimatedImpact = kylesLambda * request.TargetOrderQuantity;

        // Detect Cross-Venue Arbitrage Signals (Negative Spread / Crossed Markets)
        var arbSignals = new List<CrossVenueArbitrageSignal>();

        for (int i = 0; i < books.Count; i++)
        {
            var bookA = books[i];
            var topBidA = bookA.Bids.OrderByDescending(x => x.Price).FirstOrDefault();
            if (topBidA is null) continue;

            for (int j = 0; j < books.Count; j++)
            {
                if (i == j) continue;
                var bookB = books[j];
                var topAskB = bookB.Asks.OrderBy(x => x.Price).FirstOrDefault();
                if (topAskB is null) continue;

                // If Venue A Bid > Venue B Ask, we can buy on Venue B and sell on Venue A
                if (topBidA.Price > topAskB.Price)
                {
                    decimal grossSpread = topBidA.Price - topAskB.Price;
                    decimal maxQty = Math.Min(topBidA.Quantity, topAskB.Quantity);
                    decimal grossProfit = grossSpread * maxQty;
                    decimal roundTripFees = (topBidA.Price + topAskB.Price) * maxQty * StandardMakerTakerFeeRate;
                    decimal netProfit = grossProfit - roundTripFees;

                    arbSignals.Add(new CrossVenueArbitrageSignal(
                        BuyVenue: bookB.VenueId,
                        SellVenue: bookA.VenueId,
                        BuyPrice: topAskB.Price,
                        SellPrice: topBidA.Price,
                        GrossSpreadUsd: Math.Round(grossSpread, 4),
                        MaxExecutableQuantity: Math.Round(maxQty, 4),
                        EstimatedNetProfitUsd: Math.Round(netProfit, 2),
                        IsExecutable: netProfit > 0m
                    ));
                }
            }
        }

        string routing = arbSignals.Any(a => a.IsExecutable)
            ? $"ARBITRAGE_FIRE: Cross-venue locked spread detected. Executing buy on {arbSignals.First(a => a.IsExecutable).BuyVenue} and sell on {arbSignals.First(a => a.IsExecutable).SellVenue}."
            : $"SOR_BEST_EXECUTION: Route buy orders to {bestAskVenue} ({bestAsk:F4}) and sell orders to {bestBidVenue} ({bestBid:F4}).";

        return new MicrostructureEvaluationResult(
            AssetSymbol: request.AssetSymbol,
            ConsolidatedMidPrice: Math.Round(midPrice, 4),
            ConsolidatedMicroPrice: Math.Round(microPrice, 4),
            MicroPriceOffsetUsd: Math.Round(microPriceOffset, 4),
            OrderFlowImbalance: Math.Round(ofi, 2),
            KylesLambdaImpactUsd: Math.Round(estimatedImpact, 4),
            ArbitrageSignals: arbSignals,
            BestExecutionRouting: routing
        );
    }
}
