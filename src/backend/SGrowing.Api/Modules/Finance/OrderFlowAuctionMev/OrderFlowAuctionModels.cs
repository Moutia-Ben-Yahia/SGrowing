namespace SGrowing.Api.Modules.Finance.OrderFlowAuctionMev;

public record SearcherAuctionBid(
    string SearcherAddress,
    decimal BidAmountUsd,
    string ProposedBundleHash,
    bool HasSandwichPattern,
    double SimulatedPriceImpactPercent);

public record OrderFlowAuctionRequest(
    string AuctionId,
    string UserTransactionHash,
    double UserSlippageTolerancePercent,
    List<SearcherAuctionBid> SearcherBids,
    decimal UserRebateSharePercent);

public record OrderFlowAuctionResult(
    string AuctionId,
    string WinningSearcherAddress,
    decimal ClearingPriceUsd,
    decimal UserRebateAmountUsd,
    decimal ValidatorRewardUsd,
    bool IsSandwichAttackPrevented,
    string AuctionStatus,
    string ExecutionAdvisory);
