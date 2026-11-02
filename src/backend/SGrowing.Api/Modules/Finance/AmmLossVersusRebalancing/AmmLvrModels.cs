namespace SGrowing.Api.Modules.Finance.AmmLossVersusRebalancing;

public record AmmLvrTelemetry(
    string PoolId,
    string TokenPair,
    decimal TotalValueLockedUsd,
    double CurrentAnnualizedVolatility,
    int BaseSwapFeeBps,
    double ObservationWindowHours,
    decimal CumulativeTradingVolumeUsd,
    double ToxicOrderFlowFraction,
    double PriceTickConcentrationWidthPercent);

public record AmmLvrAnalysisResult(
    string PoolId,
    decimal EstimatedLvrDrainUsd,
    decimal AccumulatedFeeRevenueUsd,
    decimal NetLpPnLUsd,
    int OptimalDynamicFeeBps,
    bool IsLvrBleedExceedingFees,
    string RebalancingState,
    string RecommendedAmmAction,
    string ExecutiveSummary);
