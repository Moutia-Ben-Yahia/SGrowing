namespace SGrowing.Tests.Finance;

using SGrowing.Api.Modules.Finance.OrderFlowAuctionMev;
using Xunit;

public class OrderFlowAuctionMevTests
{
    private readonly OrderFlowAuctionMevEngine _engine = new();

    [Fact]
    public void ResolveAuction_MultipleCompetitiveBids_ClearsSecondPriceWithUserRebate()
    {
        var request = new OrderFlowAuctionRequest(
            AuctionId: "OFA-ETH-SWAP-001",
            UserTransactionHash: "0xabc123...",
            UserSlippageTolerancePercent: 0.5,
            SearcherBids:
            [
                new SearcherAuctionBid("0xSearcherAlpha", 250.00m, "0xBundle1", false, 0.20),
                new SearcherAuctionBid("0xSearcherBeta", 180.00m, "0xBundle2", false, 0.15),
                new SearcherAuctionBid("0xSearcherGamma", 120.00m, "0xBundle3", false, 0.10)
            ],
            UserRebateSharePercent: 90.0m);

        var result = _engine.ResolveAuction(request);

        Assert.Equal("OFA-ETH-SWAP-001", result.AuctionId);
        Assert.Equal("0xSearcherAlpha", result.WinningSearcherAddress);
        Assert.Equal(180.00m, result.ClearingPriceUsd); // Second price
        Assert.Equal(162.00m, result.UserRebateAmountUsd); // 90% of $180
        Assert.Equal(18.00m, result.ValidatorRewardUsd);
        Assert.Equal("CLEARED_REBATE_DISTRIBUTED", result.AuctionStatus);
        Assert.False(result.IsSandwichAttackPrevented);
    }

    [Fact]
    public void ResolveAuction_MaliciousSandwichBid_NeutralizesSandwichAndDisqualifiesBid()
    {
        var request = new OrderFlowAuctionRequest(
            AuctionId: "OFA-SANDWICH-ATTACK-002",
            UserTransactionHash: "0xdef456...",
            UserSlippageTolerancePercent: 0.5,
            SearcherBids:
            [
                new SearcherAuctionBid("0xMaliciousPredator", 850.00m, "0xBadSandwichBundle", true, 0.45), // Sandwich!
                new SearcherAuctionBid("0xHonestBackrunner", 95.00m, "0xCleanBackrun", false, 0.10)
            ],
            UserRebateSharePercent: 90.0m);

        var result = _engine.ResolveAuction(request);

        Assert.Equal("0xHonestBackrunner", result.WinningSearcherAddress);
        Assert.True(result.IsSandwichAttackPrevented);
        Assert.Equal(95.00m, result.ClearingPriceUsd);
        Assert.Equal("CLEARED_REBATE_DISTRIBUTED", result.AuctionStatus);
        Assert.Contains("Sandwich exploit neutralized", result.ExecutionAdvisory);
    }

    [Fact]
    public void ResolveAuction_ExcessiveSlippageBidsOnly_RoutesToPrivateRpc()
    {
        var request = new OrderFlowAuctionRequest(
            AuctionId: "OFA-HIGH-SLIPPAGE-003",
            UserTransactionHash: "0x789...",
            UserSlippageTolerancePercent: 0.3, // Strict 0.3%
            SearcherBids:
            [
                new SearcherAuctionBid("0xSlippageBreaker", 500.00m, "0xHighSlip", false, 0.85) // 0.85% > 0.3%
            ],
            UserRebateSharePercent: 90.0m);

        var result = _engine.ResolveAuction(request);

        Assert.Equal("NONE", result.WinningSearcherAddress);
        Assert.Equal(0m, result.ClearingPriceUsd);
        Assert.Equal("NO_VALID_BIDS_ROUTED_PRIVATE_RPC", result.AuctionStatus);
        Assert.Contains("No valid searcher bids received", result.ExecutionAdvisory);
    }

    [Fact]
    public void ResolveAuction_ZeroSlippageTolerance_ThrowsArgumentOutOfRangeException()
    {
        var invalid = new OrderFlowAuctionRequest(
            AuctionId: "ERR-001",
            UserTransactionHash: "0x000",
            UserSlippageTolerancePercent: 0.0,
            SearcherBids: [],
            UserRebateSharePercent: 90.0m);

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.ResolveAuction(invalid));
    }
}
