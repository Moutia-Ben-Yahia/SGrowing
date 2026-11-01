namespace SGrowing.Api.Modules.SupplyChain.Replenishment;

/// <summary>
/// Service contract for supply chain demand forecasting, safety stock, ROP, and EOQ calculations.
/// </summary>
public interface ISupplyDemandForecastingEngine
{
    /// <summary>
    /// Executes Holt-Winters Triple Exponential Smoothing (Level, Trend, Seasonality) over historical demand series.
    /// </summary>
    ForecastResult ForecastDemand(
        string sku,
        IReadOnlyList<decimal> historicalDemand,
        int forecastHorizonDays = 7,
        int period = 7,
        decimal alpha = 0.2m,
        decimal beta = 0.1m,
        decimal gamma = 0.3m);

    /// <summary>
    /// Calculates dynamic safety stock accounting for both daily demand variability and supplier lead time variability:
    /// SS = Z * sqrt( (L_bar * sigma_d^2) + (d_bar^2 * sigma_L^2) )
    /// </summary>
    decimal CalculateSafetyStock(
        decimal averageDailyDemand,
        decimal demandStdDev,
        decimal leadTimeDays,
        decimal leadTimeStdDev,
        ServiceLevelConfidence confidence = ServiceLevelConfidence.Sla99);

    /// <summary>
    /// Calculates dynamic Reorder Point: ROP = (d_bar * L_bar) + SafetyStock.
    /// </summary>
    decimal CalculateReorderPoint(
        decimal averageDailyDemand,
        decimal leadTimeDays,
        decimal safetyStock);

    /// <summary>
    /// Calculates Wilson Economic Order Quantity: EOQ = sqrt( (2 * D * S) / H ).
    /// </summary>
    decimal CalculateEoq(
        decimal annualDemand,
        decimal orderSetupCost,
        decimal unitHoldingCost);

    /// <summary>
    /// Convenience helper calculating EOQ directly from average daily demand (annualizing demand: D = d_bar * 365).
    /// </summary>
    decimal CalculateEoqFromDailyDemand(
        decimal averageDailyDemand,
        decimal orderSetupCost,
        decimal unitHoldingCost);

    /// <summary>
    /// Calculates mean and sample standard deviation for a historical demand series.
    /// </summary>
    (decimal Mean, decimal StdDev) CalculateDemandStatistics(IReadOnlyList<decimal> historicalDemand);

    /// <summary>
    /// Performs end-to-end forecast and replenishment parameter analysis for an SKU.
    /// </summary>
    CalculateForecastResponse AnalyzeSkuDemand(CalculateForecastRequest request);
}

/// <summary>
/// Autonomous demand forecasting engine implementing Holt-Winters Triple Exponential Smoothing,
/// dual-variability dynamic safety stock, ROP, and Wilson EOQ.
/// </summary>
public class SupplyDemandForecastingEngine : ISupplyDemandForecastingEngine
{
    private const decimal Z_99_PERCENT = 2.33m;
    private const decimal Z_95_PERCENT = 1.64m;

    /// <inheritdoc />
    public ForecastResult ForecastDemand(
        string sku,
        IReadOnlyList<decimal> historicalDemand,
        int forecastHorizonDays = 7,
        int period = 7,
        decimal alpha = 0.2m,
        decimal beta = 0.1m,
        decimal gamma = 0.3m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentNullException.ThrowIfNull(historicalDemand);

        if (forecastHorizonDays <= 0)
        {
            forecastHorizonDays = 7;
        }

        if (period <= 1)
        {
            period = 7;
        }

        alpha = Math.Clamp(alpha, 0.01m, 1.0m);
        beta = Math.Clamp(beta, 0.01m, 1.0m);
        gamma = Math.Clamp(gamma, 0.01m, 1.0m);

        int n = historicalDemand.Count;
        if (n == 0)
        {
            // No history: return flat zeros
            return new ForecastResult(
                Sku: sku,
                ForecastHorizonDays: forecastHorizonDays,
                ProjectedDemandSeries: [.. Enumerable.Repeat(0m, forecastHorizonDays)],
                Trend: 0m,
                SeasonalityFactors: [.. Enumerable.Repeat(0m, period)],
                ComputedAtUtc: DateTime.UtcNow,
                CurrentLevel: 0m,
                Alpha: alpha,
                Beta: beta,
                Gamma: gamma,
                Period: period,
                MeanSquaredError: 0m);
        }

        if (n == 1)
        {
            decimal singleVal = Math.Max(0m, historicalDemand[0]);
            return new ForecastResult(
                Sku: sku,
                ForecastHorizonDays: forecastHorizonDays,
                ProjectedDemandSeries: [.. Enumerable.Repeat(singleVal, forecastHorizonDays)],
                Trend: 0m,
                SeasonalityFactors: [.. Enumerable.Repeat(0m, period)],
                ComputedAtUtc: DateTime.UtcNow,
                CurrentLevel: singleVal,
                Alpha: alpha,
                Beta: beta,
                Gamma: gamma,
                Period: period,
                MeanSquaredError: 0m);
        }

        // Case A: History has fewer points than one seasonal period (Double Exponential Smoothing)
        if (n < period)
        {
            return ForecastDoubleExponential(sku, historicalDemand, forecastHorizonDays, period, alpha, beta, gamma);
        }

        // Case B: Triple Exponential Smoothing (Additive Holt-Winters)
        return ForecastHoltWintersAdditive(sku, historicalDemand, forecastHorizonDays, period, alpha, beta, gamma);
    }

    private static ForecastResult ForecastDoubleExponential(
        string sku,
        IReadOnlyList<decimal> history,
        int horizon,
        int period,
        decimal alpha,
        decimal beta,
        decimal gamma)
    {
        int n = history.Count;
        decimal level = history[0];
        decimal trend = (history[n - 1] - history[0]) / (n - 1);

        decimal sse = 0m;
        for (int i = 1; i < n; i++)
        {
            decimal y = history[i];
            decimal prevLevel = level;
            decimal prevTrend = trend;

            level = (alpha * y) + ((1m - alpha) * (prevLevel + prevTrend));
            trend = (beta * (level - prevLevel)) + ((1m - beta) * prevTrend);

            decimal oneStepHat = prevLevel + prevTrend;
            sse += (y - oneStepHat) * (y - oneStepHat);
        }

        decimal mse = sse / Math.Max(1, n - 1);

        var projections = new List<decimal>(horizon);
        for (int m = 1; m <= horizon; m++)
        {
            decimal point = level + (m * trend);
            projections.Add(Math.Round(Math.Max(0m, point), 2));
        }

        return new ForecastResult(
            Sku: sku,
            ForecastHorizonDays: horizon,
            ProjectedDemandSeries: projections,
            Trend: Math.Round(trend, 4),
            SeasonalityFactors: [.. Enumerable.Repeat(0m, period)],
            ComputedAtUtc: DateTime.UtcNow,
            CurrentLevel: Math.Round(level, 4),
            Alpha: alpha,
            Beta: beta,
            Gamma: gamma,
            Period: period,
            MeanSquaredError: Math.Round(mse, 4));
    }

    private static ForecastResult ForecastHoltWintersAdditive(
        string sku,
        IReadOnlyList<decimal> history,
        int horizon,
        int period,
        decimal alpha,
        decimal beta,
        decimal gamma)
    {
        int n = history.Count;

        // 1. Initial Level L0: mean of first cycle
        decimal initialLevel = 0m;
        for (int i = 0; i < period; i++)
        {
            initialLevel += history[i];
        }
        initialLevel /= period;

        // 2. Initial Trend T0: average slope between cycles
        decimal initialTrend;
        if (n >= 2 * period)
        {
            decimal sumSlope = 0m;
            for (int i = 0; i < period; i++)
            {
                sumSlope += (history[i + period] - history[i]) / period;
            }
            initialTrend = sumSlope / period;
        }
        else
        {
            initialTrend = (history[n - 1] - history[0]) / (n - 1);
        }

        // 3. Initial Seasonal factors S_0 ... S_{p-1}
        var seasonalIndices = new List<decimal>(n + period);
        decimal sumSeasonals = 0m;
        for (int i = 0; i < period; i++)
        {
            decimal s = history[i] - initialLevel;
            seasonalIndices.Add(s);
            sumSeasonals += s;
        }

        // Center initial seasonal factors so they sum to 0
        decimal seasonalMean = sumSeasonals / period;
        for (int i = 0; i < period; i++)
        {
            seasonalIndices[i] -= seasonalMean;
        }

        decimal currentLevel = initialLevel;
        decimal currentTrend = initialTrend;
        decimal sse = 0m;

        // 4. Update Level, Trend, Seasonality sequentially
        for (int t = period; t < n; t++)
        {
            decimal y = history[t];
            decimal prevLevel = currentLevel;
            decimal prevTrend = currentTrend;
            decimal prevSeason = seasonalIndices[t - period];

            // 1-step forecast for error calculation
            decimal oneStepPred = prevLevel + prevTrend + prevSeason;
            decimal error = y - oneStepPred;
            sse += error * error;

            // Holt-Winters additive equations:
            // Level: L_t = alpha * (Y_t - S_{t-p}) + (1 - alpha) * (L_{t-1} + T_{t-1})
            currentLevel = (alpha * (y - prevSeason)) + ((1m - alpha) * (prevLevel + prevTrend));

            // Trend: T_t = beta * (L_t - L_{t-1}) + (1 - beta) * T_{t-1}
            currentTrend = (beta * (currentLevel - prevLevel)) + ((1m - beta) * prevTrend);

            // Seasonality: S_t = gamma * (Y_t - L_t) + (1 - gamma) * S_{t-p}
            decimal newSeason = (gamma * (y - currentLevel)) + ((1m - gamma) * prevSeason);
            seasonalIndices.Add(newSeason);
        }

        decimal mse = sse / Math.Max(1, n - period);

        // Extract final p seasonal factors
        var finalSeasonals = new List<decimal>(period);
        int totalSeasonals = seasonalIndices.Count;
        for (int i = totalSeasonals - period; i < totalSeasonals; i++)
        {
            finalSeasonals.Add(Math.Round(seasonalIndices[i], 4));
        }

        // 5. Point Forecast for m = 1 .. horizon
        var projectedSeries = new List<decimal>(horizon);
        for (int m = 1; m <= horizon; m++)
        {
            // Season index wraps periodically over the last p factors
            int seasonalIndexOffset = (m - 1) % period;
            decimal sFactor = finalSeasonals[seasonalIndexOffset];

            decimal forecastVal = currentLevel + (m * currentTrend) + sFactor;
            projectedSeries.Add(Math.Round(Math.Max(0m, forecastVal), 2));
        }

        return new ForecastResult(
            Sku: sku,
            ForecastHorizonDays: horizon,
            ProjectedDemandSeries: projectedSeries,
            Trend: Math.Round(currentTrend, 4),
            SeasonalityFactors: finalSeasonals,
            ComputedAtUtc: DateTime.UtcNow,
            CurrentLevel: Math.Round(currentLevel, 4),
            Alpha: alpha,
            Beta: beta,
            Gamma: gamma,
            Period: period,
            MeanSquaredError: Math.Round(mse, 4));
    }

    /// <inheritdoc />
    public decimal CalculateSafetyStock(
        decimal averageDailyDemand,
        decimal demandStdDev,
        decimal leadTimeDays,
        decimal leadTimeStdDev,
        ServiceLevelConfidence confidence = ServiceLevelConfidence.Sla99)
    {
        decimal z = confidence == ServiceLevelConfidence.Sla95 ? Z_95_PERCENT : Z_99_PERCENT;

        // Safety Stock formula: SS = Z * sqrt( (L_bar * sigma_d^2) + (d_bar^2 * sigma_L^2) )
        double dBar = (double)Math.Max(0m, averageDailyDemand);
        double sigmaD = (double)Math.Max(0m, demandStdDev);
        double lBar = (double)Math.Max(0m, leadTimeDays);
        double sigmaL = (double)Math.Max(0m, leadTimeStdDev);

        double term1 = lBar * (sigmaD * sigmaD);
        double term2 = (dBar * dBar) * (sigmaL * sigmaL);
        double combinedVariance = term1 + term2;

        if (combinedVariance <= 0.0)
        {
            return 0m;
        }

        double ss = (double)z * Math.Sqrt(combinedVariance);
        return Math.Round((decimal)ss, 2);
    }

    /// <inheritdoc />
    public decimal CalculateReorderPoint(
        decimal averageDailyDemand,
        decimal leadTimeDays,
        decimal safetyStock)
    {
        decimal leadTimeDemand = Math.Max(0m, averageDailyDemand) * Math.Max(0m, leadTimeDays);
        decimal rop = leadTimeDemand + Math.Max(0m, safetyStock);
        return Math.Round(rop, 2);
    }

    /// <inheritdoc />
    public decimal CalculateEoq(
        decimal annualDemand,
        decimal orderSetupCost,
        decimal unitHoldingCost)
    {
        if (annualDemand <= 0m || orderSetupCost <= 0m || unitHoldingCost <= 0m)
        {
            return 0m;
        }

        // Wilson EOQ formula: sqrt( (2 * D * S) / H )
        double d = (double)annualDemand;
        double s = (double)orderSetupCost;
        double h = (double)unitHoldingCost;

        double eoq = Math.Sqrt((2.0 * d * s) / h);
        return Math.Round((decimal)eoq, 2);
    }

    /// <inheritdoc />
    public decimal CalculateEoqFromDailyDemand(
        decimal averageDailyDemand,
        decimal orderSetupCost,
        decimal unitHoldingCost)
    {
        decimal annualDemand = Math.Max(0m, averageDailyDemand) * 365m;
        return CalculateEoq(annualDemand, orderSetupCost, unitHoldingCost);
    }

    /// <inheritdoc />
    public (decimal Mean, decimal StdDev) CalculateDemandStatistics(IReadOnlyList<decimal> historicalDemand)
    {
        ArgumentNullException.ThrowIfNull(historicalDemand);

        if (historicalDemand.Count == 0)
        {
            return (0m, 0m);
        }

        if (historicalDemand.Count == 1)
        {
            return (Math.Max(0m, historicalDemand[0]), 0m);
        }

        int count = historicalDemand.Count;
        decimal sum = 0m;
        for (int i = 0; i < count; i++)
        {
            sum += historicalDemand[i];
        }

        decimal mean = sum / count;

        double sumSquares = 0.0;
        double doubleMean = (double)mean;
        for (int i = 0; i < count; i++)
        {
            double diff = (double)historicalDemand[i] - doubleMean;
            sumSquares += diff * diff;
        }

        // Sample standard deviation (N - 1 denominator)
        double sampleVariance = sumSquares / (count - 1);
        decimal stdDev = (decimal)Math.Sqrt(sampleVariance);

        return (Math.Round(mean, 4), Math.Round(stdDev, 4));
    }

    /// <inheritdoc />
    public CalculateForecastResponse AnalyzeSkuDemand(CalculateForecastRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (avgDemand, demandStdDev) = CalculateDemandStatistics(request.HistoricalDemand);

        var forecast = ForecastDemand(
            sku: request.Sku,
            historicalDemand: request.HistoricalDemand,
            forecastHorizonDays: request.ForecastHorizonDays,
            period: request.Period,
            alpha: request.Alpha ?? 0.2m,
            beta: request.Beta ?? 0.1m,
            gamma: request.Gamma ?? 0.3m);

        decimal leadTimeDays = request.LeadTimeDays ?? 7m;
        decimal leadTimeStdDev = request.LeadTimeStdDev ?? 1.5m;
        decimal unitHoldingCost = request.UnitHoldingCost ?? 5m;
        decimal orderSetupCost = request.OrderSetupCost ?? 150m;

        decimal safetyStock = CalculateSafetyStock(
            averageDailyDemand: avgDemand,
            demandStdDev: demandStdDev,
            leadTimeDays: leadTimeDays,
            leadTimeStdDev: leadTimeStdDev,
            confidence: request.Confidence);

        decimal rop = CalculateReorderPoint(avgDemand, leadTimeDays, safetyStock);
        decimal eoq = CalculateEoqFromDailyDemand(avgDemand, orderSetupCost, unitHoldingCost);

        return new CalculateForecastResponse(
            Forecast: forecast,
            DynamicSafetyStock: safetyStock,
            DynamicReorderPoint: rop,
            EconomicOrderQuantity: eoq,
            AverageDailyDemand: avgDemand,
            DemandStdDev: demandStdDev);
    }
}
