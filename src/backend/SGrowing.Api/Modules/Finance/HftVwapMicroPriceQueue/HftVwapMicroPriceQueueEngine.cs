using System;

namespace SGrowing.Api.Modules.Finance.HftVwapMicroPriceQueue;

public interface IHftVwapMicroPriceQueueEngine
{
    LimitOrderBookQueueAnalysisResult AnalyzeQueuePosition(LimitOrderBookQueueTelemetry telemetry);
}

public sealed class HftVwapMicroPriceQueueEngine : IHftVwapMicroPriceQueueEngine
{
    public LimitOrderBookQueueAnalysisResult AnalyzeQueuePosition(LimitOrderBookQueueTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        if (telemetry.BestAskPrice <= telemetry.BestBidPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(telemetry), "Ask price must be strictly greater than bid price.");
        }

        double spread = telemetry.BestAskPrice - telemetry.BestBidPrice;
        double midPrice = (telemetry.BestBidPrice + telemetry.BestAskPrice) / 2.0;

        double totalQuantity = Math.Max(1.0, telemetry.BestBidQuantity + telemetry.BestAskQuantity);
        double bookImbalance = (telemetry.BestBidQuantity - telemetry.BestAskQuantity) / totalQuantity;

        // Stoikov Micro-Price
        double stoikovMicroPrice = ((telemetry.BestBidQuantity * telemetry.BestAskPrice) + (telemetry.BestAskQuantity * telemetry.BestBidPrice)) / totalQuantity;

        // Adverse Selection Index: |P_micro - P_mid| / (spread / 2)
        double halfSpread = spread / 2.0;
        double adverseSelectionIndex = Math.Min(1.0, Math.Abs(stoikovMicroPrice - midPrice) / halfSpread);

        // Queue fill probability estimation
        double depthAhead = Math.Max(0.0, telemetry.QueueDepthAheadOfOrder);
        double expectedVolume = Math.Max(1.0, telemetry.ExpectedTradedVolumeBeforeTickShift);
        double queueFractionAhead = depthAhead / totalQuantity;
        double volumeFractionRemaining = Math.Max(0.0, 1.0 - (depthAhead / expectedVolume));

        double cancelRate = Math.Max(0.0, telemetry.HistoricalCancellationRatePerSec);
        double fillProbabilityFraction = Math.Exp(-cancelRate * queueFractionAhead) * volumeFractionRemaining;
        double fillProbabilityPercent = Math.Clamp(fillProbabilityFraction * 100.0, 0.0, 100.0);

        string side = telemetry.OrderSide.Trim().ToUpperInvariant();
        string regime;
        string action;
        bool isImmediateCross;

        if (adverseSelectionIndex >= 0.80 && Math.Abs(bookImbalance) > 0.65)
        {
            regime = "ADVERSE_SELECTION_ALERT";
            action = "CANCEL_PASSIVE_QUOTES_AND_FADE_LIMIT_PRICES";
            isImmediateCross = false;
        }
        else if ((side == "BUY" && (stoikovMicroPrice - midPrice) > (0.30 * spread)) ||
                 (side == "SELL" && (midPrice - stoikovMicroPrice) > (0.30 * spread)))
        {
            regime = "SPREAD_CROSS_OPPORTUNITY";
            action = "EXECUTE_IMMEDIATE_CROSS_AGGRESSIVE_TAKER_ORDER";
            isImmediateCross = true;
        }
        else if (fillProbabilityPercent >= 60.0 && adverseSelectionIndex <= 0.40)
        {
            regime = "FAVORABLE_QUEUE_EXECUTION";
            action = "POST_PASSIVE_LIMIT_ORDER_AT_INSIDE_QUOTE";
            isImmediateCross = false;
        }
        else
        {
            regime = "NEUTRAL_QUEUE_WAIT";
            action = "HOLD_PASSIVE_POSITION_IN_QUEUE";
            isImmediateCross = false;
        }

        return new LimitOrderBookQueueAnalysisResult(
            telemetry.InstrumentId,
            Math.Round(stoikovMicroPrice, 4),
            Math.Round(bookImbalance, 4),
            Math.Round(adverseSelectionIndex, 4),
            Math.Round(fillProbabilityPercent, 2),
            regime,
            action,
            isImmediateCross,
            DateTimeOffset.UtcNow);
    }
}
