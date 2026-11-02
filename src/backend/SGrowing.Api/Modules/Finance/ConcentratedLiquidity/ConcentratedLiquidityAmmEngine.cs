namespace SGrowing.Api.Modules.Finance.ConcentratedLiquidity;

/// <summary>
/// Autonomous concentrated liquidity Automated Market Maker (AMM) engine.
/// Provides Uniswap v3-style bounded virtual reserves with volatility-adjusted dynamic fee tiering.
/// </summary>
public sealed class ConcentratedLiquidityAmmEngine
{
    public (ConcentratedPoolState NewPool, ConcentratedLiquidityPosition Position) AddLiquidity(
        ConcentratedPoolState pool,
        AddLiquidityRequest request)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(request);

        if (request.LowerPrice >= request.UpperPrice)
        {
            throw new ArgumentException("Lower price must be strictly less than upper price.");
        }

        var pa = request.LowerPrice;
        var pb = request.UpperPrice;
        var p = pool.CurrentPrice;

        var sqrtPa = Math.Sqrt(pa);
        var sqrtPb = Math.Sqrt(pb);
        var sqrtP = Math.Sqrt(p);

        double l;
        double realX = request.AmountX;
        double realY = request.AmountY;

        if (p <= pa)
        {
            // Below range: 100% token X
            l = (request.AmountX * sqrtPa * sqrtPb) / (sqrtPb - sqrtPa);
            realY = 0;
        }
        else if (p >= pb)
        {
            // Above range: 100% token Y
            l = request.AmountY / (sqrtPb - sqrtPa);
            realX = 0;
        }
        else
        {
            // Within range: Both tokens
            var lx = (request.AmountX * sqrtP * sqrtPb) / (sqrtPb - sqrtP);
            var ly = request.AmountY / (sqrtP - sqrtPa);
            l = Math.Min(lx, ly);

            // Recompute real reserves corresponding to L
            realX = l * (sqrtPb - sqrtP) / (sqrtP * sqrtPb);
            realY = l * (sqrtP - sqrtPa);
        }

        var position = new ConcentratedLiquidityPosition(
            PositionId: $"POS-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            OwnerId: request.OwnerId,
            LowerPrice: pa,
            UpperPrice: pb,
            LiquidityL: l,
            RealReserveX: realX,
            RealReserveY: realY,
            CreatedAtUtc: DateTime.UtcNow
        );

        var updatedPositions = new List<ConcentratedLiquidityPosition>(pool.Positions) { position };
        var updatedPool = pool with
        {
            TotalLiquidityL = pool.TotalLiquidityL + l,
            Positions = updatedPositions
        };

        return (updatedPool, position);
    }

    public (ConcentratedPoolState NewPool, SwapExecutionResult Result) ExecuteSwap(
        ConcentratedPoolState pool,
        SwapExactInputRequest request)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(request);

        if (pool.TotalLiquidityL <= 0)
        {
            throw new InvalidOperationException("Pool has insufficient liquidity to execute swap.");
        }

        // Calculate volatility-adjusted fee
        var baseFeeRate = pool.FeeTierBps / 10000.0;
        var dynamicFeeRate = pool.InstantaneousVolatility > 0.01
            ? baseFeeRate * (1.0 + (5.0 * pool.InstantaneousVolatility))
            : baseFeeRate;

        var amountIn = request.AmountIn;
        var feePaid = amountIn * dynamicFeeRate;
        var effectiveIn = amountIn - feePaid;

        var l = pool.TotalLiquidityL;
        var currentP = pool.CurrentPrice;
        var sqrtP = Math.Sqrt(currentP);

        double amountOut;
        double newPrice;
        string tokenOutSymbol;

        var isTokenXIn = request.TokenInSymbol.Equals(pool.TokenXSymbol, StringComparison.OrdinalIgnoreCase);

        if (isTokenXIn)
        {
            // Swapping X for Y (Price decreases: sqrtP_new = (L * sqrtP) / (L + effectiveIn * sqrtP))
            tokenOutSymbol = pool.TokenYSymbol;
            var denominator = l + (effectiveIn * sqrtP);
            var sqrtPNew = (l * sqrtP) / denominator;
            newPrice = sqrtPNew * sqrtPNew;

            amountOut = l * (sqrtP - sqrtPNew);
        }
        else
        {
            // Swapping Y for X (Price increases: sqrtP_new = sqrtP + (effectiveIn / L))
            tokenOutSymbol = pool.TokenXSymbol;
            var sqrtPNew = sqrtP + (effectiveIn / l);
            newPrice = sqrtPNew * sqrtPNew;

            amountOut = l * ((1.0 / sqrtP) - (1.0 / sqrtPNew));
        }

        var effectivePrice = amountIn > 0 ? amountOut / amountIn : 0.0;
        var priceImpact = Math.Abs((newPrice - currentP) / currentP) * 100.0;

        if (priceImpact > request.MaxSlippagePercent)
        {
            throw new InvalidOperationException($"Price impact of {priceImpact:F2}% exceeds maximum allowed slippage of {request.MaxSlippagePercent:F2}%.");
        }

        var swapResult = new SwapExecutionResult(
            SwapId: $"SWP-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            TokenInSymbol: request.TokenInSymbol,
            TokenOutSymbol: tokenOutSymbol,
            AmountIn: amountIn,
            AmountOut: amountOut,
            EffectivePrice: effectivePrice,
            FeePaid: feePaid,
            PriceImpactPercent: priceImpact,
            NewPoolPrice: newPrice,
            ExecutedAtUtc: DateTime.UtcNow
        );

        var updatedPool = pool with
        {
            CurrentPrice = newPrice
        };

        return (updatedPool, swapResult);
    }

    public double CalculateCapitalEfficiencyMultiplier(double lowerPrice, double upperPrice)
    {
        if (lowerPrice <= 0 || upperPrice <= lowerPrice)
            return 1.0;

        var ratio = Math.Pow(lowerPrice / upperPrice, 0.25);
        if (ratio >= 1.0)
            return 1.0;

        return 1.0 / (1.0 - ratio);
    }
}
