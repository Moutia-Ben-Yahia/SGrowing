namespace SGrowing.Api.Modules.Finance.OrderFlowAuctionMev;

public class OrderFlowAuctionMevEngine
{
    public OrderFlowAuctionResult ResolveAuction(OrderFlowAuctionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.AuctionId))
        {
            throw new ArgumentException("Auction ID cannot be null or empty.", nameof(request));
        }

        if (request.UserSlippageTolerancePercent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Slippage tolerance must be positive.");
        }

        var bids = request.SearcherBids ?? [];
        bool anySandwichAttempted = bids.Any(b => b.HasSandwichPattern);

        // Filter valid bids: must not be a sandwich attack and must respect user slippage tolerance
        var validBids = bids
            .Where(b => !b.HasSandwichPattern && b.SimulatedPriceImpactPercent <= request.UserSlippageTolerancePercent && b.BidAmountUsd > 0m)
            .OrderByDescending(b => b.BidAmountUsd)
            .ToList();

        if (validBids.Count == 0)
        {
            string emptySummary = anySandwichAttempted
                ? "Malicious sandwich bundle detected and rejected. No compliant searcher bids remained; user transaction routed securely through private RPC."
                : "No valid searcher bids received. Transaction routed directly to private builder RPC with zero MEV leak.";

            return new OrderFlowAuctionResult(
                request.AuctionId,
                "NONE",
                0m,
                0m,
                0m,
                anySandwichAttempted,
                "NO_VALID_BIDS_ROUTED_PRIVATE_RPC",
                emptySummary);
        }

        var winner = validBids[0];

        // Second-price sealed-bid rule: clearing price is second highest valid bid, or highest if single bidder
        decimal clearingPrice = validBids.Count > 1 ? validBids[1].BidAmountUsd : winner.BidAmountUsd;

        decimal rebateShare = Math.Clamp(request.UserRebateSharePercent, 50.0m, 100.0m) / 100.0m;
        decimal userRebate = Math.Round(clearingPrice * rebateShare, 2);
        decimal validatorReward = clearingPrice - userRebate;

        string advisory = anySandwichAttempted
            ? $"Auction cleared! Sandwich exploit neutralized. Winning searcher {winner.SearcherAddress} paid ${clearingPrice:N2}. User refunded ${userRebate:N2} ({rebateShare:P0} rebate)."
            : $"Order Flow Auction settled! Winning searcher {winner.SearcherAddress} executed backrun at clearing price ${clearingPrice:N2}. User received ${userRebate:N2} MEV rebate.";

        return new OrderFlowAuctionResult(
            request.AuctionId,
            winner.SearcherAddress,
            clearingPrice,
            userRebate,
            validatorReward,
            anySandwichAttempted,
            "CLEARED_REBATE_DISTRIBUTED",
            advisory);
    }
}
