namespace SGrowing.Api.Modules.Finance.HftOrderBookMicrostructure;

public record OrderBookLevel(
    decimal Price,
    decimal Quantity,
    int OrderCount
);

public record VenueOrderBook(
    string VenueId,
    string AssetSymbol,
    IReadOnlyList<OrderBookLevel> Bids,
    IReadOnlyList<OrderBookLevel> Asks,
    double OneWayLatencyMicroseconds
);

public record MicrostructureAnalysisRequest(
    string AssetSymbol,
    IReadOnlyList<VenueOrderBook> VenueBooks,
    decimal TargetOrderQuantity = 10.0m
);

public record CrossVenueArbitrageSignal(
    string BuyVenue,
    string SellVenue,
    decimal BuyPrice,
    decimal SellPrice,
    decimal GrossSpreadUsd,
    decimal MaxExecutableQuantity,
    decimal EstimatedNetProfitUsd,
    bool IsExecutable
);

public record MicrostructureEvaluationResult(
    string AssetSymbol,
    decimal ConsolidatedMidPrice,
    decimal ConsolidatedMicroPrice,
    decimal MicroPriceOffsetUsd,
    decimal OrderFlowImbalance,
    decimal KylesLambdaImpactUsd,
    IReadOnlyList<CrossVenueArbitrageSignal> ArbitrageSignals,
    string BestExecutionRouting
);
