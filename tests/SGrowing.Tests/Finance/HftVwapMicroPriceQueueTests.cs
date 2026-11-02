using System;
using SGrowing.Api.Modules.Finance.HftVwapMicroPriceQueue;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class HftVwapMicroPriceQueueTests
{
    private readonly HftVwapMicroPriceQueueEngine _engine = new();

    [Fact]
    public void AnalyzeQueuePosition_FavorableQueuePlacement_PostsPassiveOrder()
    {
        var telemetry = new LimitOrderBookQueueTelemetry(
            InstrumentId: "ES-FUTURES-2026M",
            BestBidPrice: 5800.00,
            BestAskPrice: 5800.25,
            BestBidQuantity: 2500.0,
            BestAskQuantity: 2500.0,
            ChildOrderQuantity: 50.0,
            QueueDepthAheadOfOrder: 200.0,
            HistoricalCancellationRatePerSec: 0.20,
            ExpectedTradedVolumeBeforeTickShift: 2000.0,
            OrderSide: "BUY");

        var result = _engine.AnalyzeQueuePosition(telemetry);

        Assert.Equal("FAVORABLE_QUEUE_EXECUTION", result.OrderExecutionRegime);
        Assert.Equal("POST_PASSIVE_LIMIT_ORDER_AT_INSIDE_QUOTE", result.OptimalExecutionAction);
        Assert.False(result.IsImmediateCrossRecommended);
        Assert.True(result.EstimatedQueueFillProbabilityPercent >= 60.0);
        Assert.True(result.AdverseSelectionIndex <= 0.40);
    }

    [Fact]
    public void AnalyzeQueuePosition_SevereAdverseSelection_FadesPassiveLimit()
    {
        // Bid qty 9000 vs Ask qty 1000 -> Imbalance = 0.80 > 0.65
        // Spread = 0.25, Mid = 5800.125
        // MicroPrice = (9000 * 5800.25 + 1000 * 5800.00) / 10000 = 5800.225
        // ASI = |5800.225 - 5800.125| / 0.125 = 0.100 / 0.125 = 0.80 >= 0.80
        var telemetry = new LimitOrderBookQueueTelemetry(
            InstrumentId: "NQ-FUTURES-2026M",
            BestBidPrice: 5800.00,
            BestAskPrice: 5800.25,
            BestBidQuantity: 9000.0,
            BestAskQuantity: 1000.0,
            ChildOrderQuantity: 100.0,
            QueueDepthAheadOfOrder: 500.0,
            HistoricalCancellationRatePerSec: 0.25,
            ExpectedTradedVolumeBeforeTickShift: 3000.0,
            OrderSide: "SELL");

        var result = _engine.AnalyzeQueuePosition(telemetry);

        Assert.Equal("ADVERSE_SELECTION_ALERT", result.OrderExecutionRegime);
        Assert.Equal("CANCEL_PASSIVE_QUOTES_AND_FADE_LIMIT_PRICES", result.OptimalExecutionAction);
        Assert.False(result.IsImmediateCrossRecommended);
        Assert.True(result.AdverseSelectionIndex >= 0.80);
    }

    [Fact]
    public void AnalyzeQueuePosition_MicroPriceSkewTowardOrderSide_ExecutesImmediateCross()
    {
        // Spread = 0.50, Mid = 5800.25. Bid qty 4500 vs Ask qty 1500 (Total 6000).
        // Imbalance = 3000 / 6000 = 0.50 (< 0.65 so not adverse alert)
        // MicroPrice = (4500 * 5800.50 + 1500 * 5800.00) / 6000 = 5800.375
        // MicroPrice - Mid = 0.125 > 0.30 * 0.50 (0.150)? Let's check:
        // If Bid qty = 5000, Ask qty = 1500 (Total 6500) -> Imbalance = 3500/6500 = 0.538 (< 0.65)
        // MicroPrice = (5000 * 5800.50 + 1500 * 5800.00) / 6500 = 5800.3846
        // MicroPrice - Mid = 5800.3846 - 5800.25 = 0.1346 <= 0.150
        // If Bid qty = 5500, Ask qty = 1500 (Total 7000) -> Imbalance = 4000/7000 = 0.571 (< 0.65)
        // MicroPrice = (5500 * 5800.50 + 1500 * 5800.00) / 7000 = 5800.3928
        // MicroPrice - Mid = 0.1428
        // If Spread = 0.20 (Bid 100.00, Ask 100.20, Mid 100.10)
        // 0.30 * 0.20 = 0.06.
        // Bid qty 4000, Ask qty 1500 (Total 5500) -> Imbalance = 2500 / 5500 = 0.454 (< 0.65)
        // MicroPrice = (4000 * 100.20 + 1500 * 100.00) / 5500 = 100.1454
        // MicroPrice - Mid = 100.1454 - 100.10 = 0.0454 (need > 0.06)
        // Let's use Bid qty 5000, Ask qty 1500, spread 0.10 (Bid 100.00, Ask 100.10, Mid 100.05).
        // 0.30 * 0.10 = 0.030.
        // MicroPrice = (5000 * 100.10 + 1500 * 100.00) / 6500 = 100.0769
        // MicroPrice - Mid = 100.0769 - 100.05 = 0.0269 (need > 0.030)
        // If Bid qty 4800, Ask qty 1200 (Total 6000) -> Imbalance = 3600 / 6000 = 0.60 (< 0.65)
        // MicroPrice = (4800 * 100.10 + 1200 * 100.00) / 6000 = 100.0800
        // MicroPrice - Mid = 100.0800 - 100.05 = 0.0300
        // If Bid qty 5000, Ask qty 1200 (Total 6200) -> Imbalance = 3800 / 6200 = 0.6129 (< 0.65)
        // MicroPrice = (5000 * 100.10 + 1200 * 100.00) / 6200 = 100.08064
        // MicroPrice - Mid = 0.03064 > 0.0300!
        var telemetry = new LimitOrderBookQueueTelemetry(
            InstrumentId: "AAPL-EQUITY-LOB",
            BestBidPrice: 100.00,
            BestAskPrice: 100.10,
            BestBidQuantity: 5000.0,
            BestAskQuantity: 1200.0,
            ChildOrderQuantity: 100.0,
            QueueDepthAheadOfOrder: 250.0,
            HistoricalCancellationRatePerSec: 0.15,
            ExpectedTradedVolumeBeforeTickShift: 2000.0,
            OrderSide: "BUY");

        var result = _engine.AnalyzeQueuePosition(telemetry);

        Assert.Equal("SPREAD_CROSS_OPPORTUNITY", result.OrderExecutionRegime);
        Assert.Equal("EXECUTE_IMMEDIATE_CROSS_AGGRESSIVE_TAKER_ORDER", result.OptimalExecutionAction);
        Assert.True(result.IsImmediateCrossRecommended);
    }

    [Fact]
    public void AnalyzeQueuePosition_DeepQueueWithLowProbability_HoldsNeutralPosition()
    {
        // QueueDepthAhead = 3500 vs ExpectedVolume = 2000 -> volume fraction = 0 -> fill prob = 0%
        var telemetry = new LimitOrderBookQueueTelemetry(
            InstrumentId: "EURUSD-FX-LOB",
            BestBidPrice: 1.0850,
            BestAskPrice: 1.0852,
            BestBidQuantity: 2000.0,
            BestAskQuantity: 2000.0,
            ChildOrderQuantity: 200.0,
            QueueDepthAheadOfOrder: 3500.0,
            HistoricalCancellationRatePerSec: 0.40,
            ExpectedTradedVolumeBeforeTickShift: 2000.0,
            OrderSide: "BUY");

        var result = _engine.AnalyzeQueuePosition(telemetry);

        Assert.Equal("NEUTRAL_QUEUE_WAIT", result.OrderExecutionRegime);
        Assert.Equal("HOLD_PASSIVE_POSITION_IN_QUEUE", result.OptimalExecutionAction);
        Assert.False(result.IsImmediateCrossRecommended);
        Assert.Equal(0.0, result.EstimatedQueueFillProbabilityPercent);
    }
}
