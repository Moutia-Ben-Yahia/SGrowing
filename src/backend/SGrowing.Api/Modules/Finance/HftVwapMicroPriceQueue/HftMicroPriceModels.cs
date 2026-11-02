using System;

namespace SGrowing.Api.Modules.Finance.HftVwapMicroPriceQueue;

public sealed record LimitOrderBookQueueTelemetry(
    string InstrumentId,
    double BestBidPrice,
    double BestAskPrice,
    double BestBidQuantity,
    double BestAskQuantity,
    double ChildOrderQuantity,
    double QueueDepthAheadOfOrder,
    double HistoricalCancellationRatePerSec,
    double ExpectedTradedVolumeBeforeTickShift,
    string OrderSide);

public sealed record LimitOrderBookQueueAnalysisResult(
    string InstrumentId,
    double StoikovMicroPrice,
    double BookImbalanceFraction,
    double AdverseSelectionIndex,
    double EstimatedQueueFillProbabilityPercent,
    string OrderExecutionRegime,
    string OptimalExecutionAction,
    bool IsImmediateCrossRecommended,
    DateTimeOffset Timestamp);
