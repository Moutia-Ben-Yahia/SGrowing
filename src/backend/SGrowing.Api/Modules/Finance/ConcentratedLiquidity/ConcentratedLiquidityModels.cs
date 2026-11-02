namespace SGrowing.Api.Modules.Finance.ConcentratedLiquidity;

public sealed record ConcentratedLiquidityPosition(
    string PositionId,
    string OwnerId,
    double LowerPrice,
    double UpperPrice,
    double LiquidityL,
    double RealReserveX,
    double RealReserveY,
    DateTime CreatedAtUtc
);

public sealed record ConcentratedPoolState(
    string PoolId,
    string TokenXSymbol,
    string TokenYSymbol,
    double CurrentPrice,
    double TotalLiquidityL,
    int FeeTierBps,
    double InstantaneousVolatility,
    IReadOnlyList<ConcentratedLiquidityPosition> Positions
);

public sealed record AddLiquidityRequest(
    string PoolId,
    string OwnerId,
    double LowerPrice,
    double UpperPrice,
    double AmountX,
    double AmountY
);

public sealed record SwapExactInputRequest(
    string PoolId,
    string TokenInSymbol,
    double AmountIn,
    double MaxSlippagePercent
);

public sealed record SwapExecutionResult(
    string SwapId,
    string TokenInSymbol,
    string TokenOutSymbol,
    double AmountIn,
    double AmountOut,
    double EffectivePrice,
    double FeePaid,
    double PriceImpactPercent,
    double NewPoolPrice,
    DateTime ExecutedAtUtc
);

public sealed record PoolMetricsResponse(
    string PoolId,
    double CurrentPrice,
    double TotalLiquidityL,
    double CapitalEfficiencyMultiplier,
    int EffectiveFeeBps,
    DateTime TimestampUtc
);
