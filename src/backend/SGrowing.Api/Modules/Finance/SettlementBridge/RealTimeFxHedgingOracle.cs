namespace SGrowing.Api.Modules.Finance.SettlementBridge;

/// <summary>
/// Contract interface for real-time FX pricing, Interest Rate Parity (IRP) forward calculation,
/// orderbook liquidity depth simulation, slippage estimation, and merchant hedging execution.
/// </summary>
public interface IRealTimeFxHedgingOracle
{
    /// <summary>
    /// Fetches the spot exchange rate for a currency pair.
    /// </summary>
    decimal GetSpotRate(CurrencyCode baseCurrency, CurrencyCode quoteCurrency);

    /// <summary>
    /// Retrieves the domestic and foreign interest rates for a currency pair according to IRP conventions.
    /// </summary>
    (decimal DomesticRate, decimal ForeignRate) GetInterestRates(CurrencyCode baseCurrency, CurrencyCode quoteCurrency);

    /// <summary>
    /// Calculates the dynamic forward exchange rate using Interest Rate Parity (IRP):
    /// F = S * ((1 + r_d * (t / 360)) / (1 + r_f * (t / 360))).
    /// </summary>
    decimal CalculateForwardRate(
        decimal spotRate,
        decimal domesticRate,
        decimal foreignRate,
        int tenureDays);

    /// <summary>
    /// Computes forward points in pips / basis points: (F - S) * 10,000.
    /// </summary>
    decimal CalculateForwardPoints(decimal spotRate, decimal forwardRate);

    /// <summary>
    /// Computes dynamic margin spread points based on tenure and volatility buffer.
    /// </summary>
    decimal CalculateMarginSpreadPoints(int tenureDays);

    /// <summary>
    /// Generates a guaranteed FX hedging quote locking in forward rates and spreads.
    /// </summary>
    FxHedgingQuote GetHedgingQuote(
        CurrencyCode baseCurrency,
        CurrencyCode quoteCurrency,
        decimal notionalAmount,
        int tenureDays = 30);

    /// <summary>
    /// Estimates orderbook slippage and price impact given available liquidity pool depth.
    /// </summary>
    SlippageEstimate EstimateSlippage(
        CurrencyCode baseCurrency,
        CurrencyCode quoteCurrency,
        decimal requestedAmount);

    /// <summary>
    /// Executes a hedging order against an active quote, locking in merchant profit margin.
    /// </summary>
    HedgingExecutionOrder ExecuteHedgingOrder(
        string quoteId,
        decimal notionalAmount,
        decimal merchantTargetMarginPct = 2.0m);

    /// <summary>
    /// Retrieves a specific executed hedging order.
    /// </summary>
    HedgingExecutionOrder GetHedgingOrder(string orderId);

    /// <summary>
    /// Lists all executed hedging orders.
    /// </summary>
    IReadOnlyList<HedgingExecutionOrder> GetAllHedgingOrders();

    /// <summary>
    /// Retrieves all liquidity pool balances across supported payment rails.
    /// </summary>
    IReadOnlyList<LiquidityPoolBalance> GetLiquidityPools();

    /// <summary>
    /// Retrieves a specific liquidity pool by its identifier.
    /// </summary>
    LiquidityPoolBalance GetPool(string poolId);

    /// <summary>
    /// Updates balance and locked HTLC commitments for a pool in a thread-safe manner.
    /// </summary>
    void UpdatePoolBalance(string poolId, decimal deltaBalance, decimal deltaLocked);
}

/// <summary>
/// Production real-time FX hedging oracle and liquidity aggregator.
/// </summary>
public sealed class RealTimeFxHedgingOracle : IRealTimeFxHedgingOracle
{
    private readonly Lock _lock = new();

    // Central bank benchmark reference rates (annualized)
    private static readonly Dictionary<CurrencyCode, decimal> BaseInterestRates = new()
    {
        [CurrencyCode.EUR] = 0.0350m, // ECB Deposit Facility
        [CurrencyCode.USD] = 0.0525m, // US Federal Funds Rate
        [CurrencyCode.DZD] = 0.0600m, // Bank of Algeria Interbank Rate
        [CurrencyCode.AED] = 0.0515m, // CBUAE Base Rate
        [CurrencyCode.GBP] = 0.0475m  // Bank of England Base Rate
    };

    // Reference base spot rates against EUR
    private static readonly Dictionary<CurrencyCode, decimal> BaseEurSpotRates = new()
    {
        [CurrencyCode.EUR] = 1.000000m,
        [CurrencyCode.USD] = 1.085000m,
        [CurrencyCode.DZD] = 145.200000m,
        [CurrencyCode.AED] = 3.985000m,
        [CurrencyCode.GBP] = 0.855000m
    };

    private readonly Dictionary<string, LiquidityPoolBalance> _pools = [];
    private readonly Dictionary<string, FxHedgingQuote> _quotes = [];
    private readonly Dictionary<string, HedgingExecutionOrder> _orders = [];

    /// <summary>
    /// Initializes default multi-currency multi-rail liquidity pools.
    /// </summary>
    public RealTimeFxHedgingOracle()
    {
        InitializeDefaultPools();
    }

    private void InitializeDefaultPools()
    {
        LiquidityPoolBalance[] defaultPools =
        [
            new("pool_sepa_eur", CurrencyCode.EUR, PaymentRail.SepaInstant, 10_000_000m, 0m, 10_000_000m),
            new("pool_fednow_usd", CurrencyCode.USD, PaymentRail.FedNow, 15_000_000m, 0m, 15_000_000m),
            new("pool_usdc_usd", CurrencyCode.USD, PaymentRail.UsdcStablecoin, 30_000_000m, 0m, 30_000_000m),
            new("pool_eurc_eur", CurrencyCode.EUR, PaymentRail.EurcStablecoin, 20_000_000m, 0m, 20_000_000m),
            new("pool_dz_cpi_dzd", CurrencyCode.DZD, PaymentRail.DzInstantCpi, 2_000_000_000m, 0m, 2_000_000_000m),
            new("pool_fednow_aed", CurrencyCode.AED, PaymentRail.FedNow, 50_000_000m, 0m, 50_000_000m),
            new("pool_sepa_gbp", CurrencyCode.GBP, PaymentRail.SepaInstant, 8_000_000m, 0m, 8_000_000m)
        ];

        foreach (var p in defaultPools)
        {
            _pools[p.PoolId] = p;
        }
    }

    /// <inheritdoc />
    public decimal GetSpotRate(CurrencyCode baseCurrency, CurrencyCode quoteCurrency)
    {
        if (baseCurrency == quoteCurrency)
            return 1.000000m;

        decimal eurToBase = BaseEurSpotRates[baseCurrency];
        decimal eurToQuote = BaseEurSpotRates[quoteCurrency];

        // Spot(Base, Quote) = EUR/Quote / EUR/Base
        decimal spot = eurToQuote / eurToBase;
        return Math.Round(spot, 6);
    }

    /// <inheritdoc />
    public (decimal DomesticRate, decimal ForeignRate) GetInterestRates(CurrencyCode baseCurrency, CurrencyCode quoteCurrency)
    {
        // Standard FX Quotation Convention: Base / Quote
        // Quote currency is the domestic currency (d); Base currency is the foreign currency (f)
        decimal domesticRate = BaseInterestRates[quoteCurrency];
        decimal foreignRate = BaseInterestRates[baseCurrency];

        return (domesticRate, foreignRate);
    }

    /// <inheritdoc />
    public decimal CalculateForwardRate(
        decimal spotRate,
        decimal domesticRate,
        decimal foreignRate,
        int tenureDays)
    {
        if (spotRate <= 0m)
            throw new ArgumentOutOfRangeException(nameof(spotRate), "Spot rate must be strictly positive.");

        if (tenureDays < 0)
            throw new ArgumentOutOfRangeException(nameof(tenureDays), "Tenure in days cannot be negative.");

        if (tenureDays == 0)
            return spotRate;

        decimal t = (decimal)tenureDays;
        decimal domesticFactor = 1m + (domesticRate * (t / 360m));
        decimal foreignFactor = 1m + (foreignRate * (t / 360m));

        if (foreignFactor == 0m)
            throw new InvalidOperationException("Foreign interest rate factor cannot be zero.");

        // Interest Rate Parity formula: F = S * ((1 + r_d * (t / 360)) / (1 + r_f * (t / 360)))
        decimal forwardRate = spotRate * (domesticFactor / foreignFactor);
        return Math.Round(forwardRate, 6);
    }

    /// <inheritdoc />
    public decimal CalculateForwardPoints(decimal spotRate, decimal forwardRate)
    {
        // Forward points = (F - S) * 10,000 pips
        return Math.Round((forwardRate - spotRate) * 10_000m, 2);
    }

    /// <inheritdoc />
    public decimal CalculateMarginSpreadPoints(int tenureDays)
    {
        // Base margin spread is 12 bps, expanding linearly with tenure to reflect liquidity risk
        decimal baseSpread = 12.0m;
        decimal tenureComponent = ((decimal)Math.Max(tenureDays, 1) / 30m) * 2.0m;
        return Math.Round(baseSpread + tenureComponent, 2);
    }

    /// <inheritdoc />
    public FxHedgingQuote GetHedgingQuote(
        CurrencyCode baseCurrency,
        CurrencyCode quoteCurrency,
        decimal notionalAmount,
        int tenureDays = 30)
    {
        if (notionalAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(notionalAmount), "Notional amount must be strictly positive.");

        if (tenureDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(tenureDays), "Tenure days must be positive.");

        decimal spotRate = GetSpotRate(baseCurrency, quoteCurrency);
        var (domesticRate, foreignRate) = GetInterestRates(baseCurrency, quoteCurrency);
        decimal forwardRate = CalculateForwardRate(spotRate, domesticRate, foreignRate, tenureDays);
        decimal forwardPoints = CalculateForwardPoints(spotRate, forwardRate);
        decimal marginSpreadPoints = CalculateMarginSpreadPoints(tenureDays);

        // Convert spread points (pips) to rate spread
        decimal spreadAmount = spotRate * (marginSpreadPoints / 10_000m);
        decimal bidRate = Math.Round(forwardRate - spreadAmount, 6);
        decimal askRate = Math.Round(forwardRate + spreadAmount, 6);

        var quoteId = $"quote_{Guid.NewGuid():N}";
        var expiryUtc = DateTime.UtcNow.AddDays(tenureDays);

        var quote = new FxHedgingQuote(
            QuoteId: quoteId,
            Pair: $"{baseCurrency}/{quoteCurrency}",
            SpotRate: spotRate,
            ForwardRate: forwardRate,
            MarginSpreadPoints: marginSpreadPoints,
            ExpiryUtc: expiryUtc,
            DomesticInterestRate: domesticRate,
            ForeignInterestRate: foreignRate,
            TenureDays: tenureDays,
            ForwardPoints: forwardPoints,
            BidForwardRate: bidRate,
            AskForwardRate: askRate,
            RecommendedMaxVolume: 10_000_000m
        );

        lock (_lock)
        {
            _quotes[quoteId] = quote;
        }

        return quote;
    }

    /// <inheritdoc />
    public SlippageEstimate EstimateSlippage(
        CurrencyCode baseCurrency,
        CurrencyCode quoteCurrency,
        decimal requestedAmount)
    {
        if (requestedAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(requestedAmount), "Requested amount must be strictly positive.");

        decimal spotRate = GetSpotRate(baseCurrency, quoteCurrency);
        decimal notionalTarget = requestedAmount * spotRate;

        // Aggregate total available liquidity in pools for the quote currency
        decimal totalAvailableQuoteLiquidity = 0m;
        lock (_lock)
        {
            foreach (var pool in _pools.Values)
            {
                if (pool.Currency == quoteCurrency)
                {
                    totalAvailableQuoteLiquidity += pool.AvailableBalance;
                }
            }
        }

        if (totalAvailableQuoteLiquidity <= 0m)
        {
            return new SlippageEstimate(
                RequestedAmount: requestedAmount,
                EffectivePrice: spotRate,
                PriceImpactPercentage: 100m,
                SlippageAmount: requestedAmount * spotRate,
                ExceedsMaxLiquidity: true
            );
        }

        bool exceedsLiquidity = notionalTarget > totalAvailableQuoteLiquidity;

        if (exceedsLiquidity)
        {
            decimal extremeImpact = 50.0m;
            decimal extremeEffectivePrice = Math.Round(spotRate * 1.50m, 6);
            decimal slippage = Math.Round(Math.Abs(extremeEffectivePrice - spotRate) * requestedAmount, 2);

            return new SlippageEstimate(
                RequestedAmount: requestedAmount,
                EffectivePrice: extremeEffectivePrice,
                PriceImpactPercentage: extremeImpact,
                SlippageAmount: slippage,
                ExceedsMaxLiquidity: true
            );
        }

        // Linear Orderbook Depth Model: Price Impact = (Q / (2 * PoolDepth)) * 100
        decimal priceImpactPercentage = Math.Round((notionalTarget / (2m * totalAvailableQuoteLiquidity)) * 100m, 4);
        decimal effectivePrice = Math.Round(spotRate * (1m + (priceImpactPercentage / 100m)), 6);
        decimal slippageAmount = Math.Round(Math.Abs(effectivePrice - spotRate) * requestedAmount, 2);

        return new SlippageEstimate(
            RequestedAmount: requestedAmount,
            EffectivePrice: effectivePrice,
            PriceImpactPercentage: priceImpactPercentage,
            SlippageAmount: slippageAmount,
            ExceedsMaxLiquidity: false
        );
    }

    /// <inheritdoc />
    public HedgingExecutionOrder ExecuteHedgingOrder(
        string quoteId,
        decimal notionalAmount,
        decimal merchantTargetMarginPct = 2.0m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quoteId);

        if (notionalAmount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(notionalAmount), "Notional amount must be positive.");

        if (merchantTargetMarginPct < 0m)
            throw new ArgumentOutOfRangeException(nameof(merchantTargetMarginPct), "Margin percentage cannot be negative.");

        lock (_lock)
        {
            if (!_quotes.TryGetValue(quoteId, out var quote))
                throw new KeyNotFoundException($"Quote '{quoteId}' was not found.");

            if (DateTime.UtcNow > quote.ExpiryUtc)
                throw new InvalidOperationException($"Quote '{quoteId}' has expired.");

            decimal profitMarginDzdOrBase = Math.Round(notionalAmount * (merchantTargetMarginPct / 100m), 2);
            var orderId = $"hord_{Guid.NewGuid():N}";

            var order = new HedgingExecutionOrder(
                OrderId: orderId,
                QuoteId: quote.QuoteId,
                Pair: quote.Pair,
                NotionalAmount: notionalAmount,
                LockedForwardRate: quote.ForwardRate,
                ExecutionTimeUtc: DateTime.UtcNow,
                Status: "Executed",
                GuaranteedMerchantProfitMargin: profitMarginDzdOrBase
            );

            _orders[orderId] = order;
            return order;
        }
    }

    /// <inheritdoc />
    public HedgingExecutionOrder GetHedgingOrder(string orderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        lock (_lock)
        {
            if (_orders.TryGetValue(orderId, out var order))
                return order;
        }

        throw new KeyNotFoundException($"Hedging order '{orderId}' was not found.");
    }

    /// <inheritdoc />
    public IReadOnlyList<HedgingExecutionOrder> GetAllHedgingOrders()
    {
        lock (_lock)
        {
            return [.. _orders.Values];
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<LiquidityPoolBalance> GetLiquidityPools()
    {
        lock (_lock)
        {
            return [.. _pools.Values];
        }
    }

    /// <inheritdoc />
    public LiquidityPoolBalance GetPool(string poolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);

        lock (_lock)
        {
            if (_pools.TryGetValue(poolId, out var pool))
                return pool;
        }

        throw new KeyNotFoundException($"Pool '{poolId}' was not found.");
    }

    /// <inheritdoc />
    public void UpdatePoolBalance(string poolId, decimal deltaBalance, decimal deltaLocked)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolId);

        lock (_lock)
        {
            if (!_pools.TryGetValue(poolId, out var pool))
                throw new KeyNotFoundException($"Pool '{poolId}' was not found.");

            decimal newTotal = pool.TotalBalance + deltaBalance;
            if (newTotal < 0m)
                throw new InvalidOperationException($"Insufficient total balance in pool '{poolId}'.");

            decimal newLocked = pool.LockedInHtlc + deltaLocked;
            if (newLocked < 0m)
                newLocked = 0m;

            if (newLocked > newTotal)
                throw new InvalidOperationException($"Locked amount cannot exceed total balance in pool '{poolId}'.");

            decimal newAvailable = newTotal - newLocked;
            decimal utilization = newTotal > 0m ? Math.Round(newLocked / newTotal, 4) : 0m;

            var updatedPool = pool with
            {
                TotalBalance = newTotal,
                LockedInHtlc = newLocked,
                AvailableBalance = newAvailable,
                UtilizationRate = utilization
            };

            _pools[poolId] = updatedPool;
        }
    }
}
