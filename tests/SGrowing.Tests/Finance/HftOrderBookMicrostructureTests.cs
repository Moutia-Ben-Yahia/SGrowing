namespace SGrowing.Tests.Finance;

using FluentAssertions;
using SGrowing.Api.Modules.Finance.HftOrderBookMicrostructure;
using Xunit;

public class HftOrderBookMicrostructureTests
{
    private readonly HftOrderBookMicrostructureEngine _engine = new();

    [Fact]
    public void EvaluateMicrostructure_CrossVenueArbitrageOpportunity_DetectsExecutableSignal()
    {
        // Venue A has higher Bid than Venue B Ask
        var venueA = new VenueOrderBook(
            VenueId: "NASDAQ",
            AssetSymbol: "NVDA",
            Bids: [new(140.25m, 500m, 12)],
            Asks: [new(140.35m, 400m, 8)],
            OneWayLatencyMicroseconds: 15.0
        );

        var venueB = new VenueOrderBook(
            VenueId: "BATS",
            AssetSymbol: "NVDA",
            Bids: [new(140.10m, 300m, 6)],
            Asks: [new(140.15m, 500m, 10)], // Ask at 140.15 < Bid on Nasdaq at 140.25 -> 0.10 USD spread
            OneWayLatencyMicroseconds: 25.0
        );

        var request = new MicrostructureAnalysisRequest("NVDA", [venueA, venueB]);
        var result = _engine.EvaluateMicrostructure(request);

        result.ArbitrageSignals.Should().Contain(a => a.IsExecutable && a.BuyVenue == "BATS" && a.SellVenue == "NASDAQ");
        result.BestExecutionRouting.Should().Contain("ARBITRAGE_FIRE");
        var signal = result.ArbitrageSignals.First(a => a.IsExecutable);
        signal.GrossSpreadUsd.Should().Be(0.10m);
        signal.EstimatedNetProfitUsd.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void EvaluateMicrostructure_HeavyBidQueue_ShiftsMicroPriceTowardsAsk()
    {
        // 10x deeper bid queue shifts microprice towards ask
        var book = new VenueOrderBook(
            VenueId: "CME",
            AssetSymbol: "ES_FUT",
            Bids: [new(5950.00m, 1000m, 50)],
            Asks: [new(5950.25m, 100m, 5)],
            OneWayLatencyMicroseconds: 5.0
        );

        var request = new MicrostructureAnalysisRequest("ES_FUT", [book]);
        var result = _engine.EvaluateMicrostructure(request);

        result.ConsolidatedMidPrice.Should().Be(5950.125m);
        result.ConsolidatedMicroPrice.Should().BeGreaterThan(result.ConsolidatedMidPrice);
        result.MicroPriceOffsetUsd.Should().BeGreaterThan(0m);
        result.OrderFlowImbalance.Should().Be(900m);
    }

    [Fact]
    public void EvaluateMicrostructure_EmptyBooks_ReturnsSafeEmptyResult()
    {
        var request = new MicrostructureAnalysisRequest("BTCUSDT", []);
        var result = _engine.EvaluateMicrostructure(request);

        result.ConsolidatedMidPrice.Should().Be(0m);
        result.ArbitrageSignals.Should().BeEmpty();
        result.BestExecutionRouting.Should().Contain("NO_VENUE_DATA");
    }

    [Fact]
    public void EvaluateMicrostructure_ComputesKylesLambdaImpact_ProportionalToOrderSize()
    {
        var book = new VenueOrderBook(
            VenueId: "BINANCE",
            AssetSymbol: "ETHUSDT",
            Bids: [new(3500.00m, 200m, 10)],
            Asks: [new(3500.50m, 200m, 10)],
            OneWayLatencyMicroseconds: 8.0
        );

        var smallOrderRequest = new MicrostructureAnalysisRequest("ETHUSDT", [book], TargetOrderQuantity: 10m);
        var largeOrderRequest = new MicrostructureAnalysisRequest("ETHUSDT", [book], TargetOrderQuantity: 50m);

        var smallResult = _engine.EvaluateMicrostructure(smallOrderRequest);
        var largeResult = _engine.EvaluateMicrostructure(largeOrderRequest);

        largeResult.KylesLambdaImpactUsd.Should().BeGreaterThan(smallResult.KylesLambdaImpactUsd);
    }
}
