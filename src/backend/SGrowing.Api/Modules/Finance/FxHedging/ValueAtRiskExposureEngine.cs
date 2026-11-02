namespace SGrowing.Api.Modules.Finance.FxHedging;

/// <summary>
/// Autonomous FX Value-at-Risk (VaR) & Conditional VaR (CVaR) risk engine
/// evaluating cross-border currency exposures and recommending forward contract hedges.
/// </summary>
public sealed class ValueAtRiskExposureEngine
{
    public ValueAtRiskReport EvaluatePortfolioRisk(EvaluateFxRiskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Positions.Count == 0)
        {
            return new ValueAtRiskReport(
                ReportId: $"VAR-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
                TenantId: request.TenantId,
                BaseCurrency: request.BaseCurrency,
                TotalNetPortfolioValueBase: 0.0m,
                ConfidenceLevel: request.ConfidenceLevel,
                HorizonDays: request.HorizonDays,
                ParametricVaRBase: 0.0m,
                ConditionalVaRExpectedShortfallBase: 0.0m,
                MaxTolerableLossBase: request.MaxLossToleranceBase,
                HedgeRecommended: false,
                Recommendations: [],
                EvaluatedAtUtc: DateTime.UtcNow
            );
        }

        // Z-score and Expected Shortfall multipliers
        var zScore = request.ConfidenceLevel >= 0.99 ? 2.326 : 1.645;
        var cvarMultiplier = request.ConfidenceLevel >= 0.99 ? 2.665 : 2.063;

        var timeHorizonFactor = Math.Sqrt((double)request.HorizonDays / 252.0); // 252 trading days/year

        var totalBaseValue = 0.0m;
        var totalVarianceSum = 0.0;
        var recommendations = new List<ForwardHedgeRecommendation>();

        foreach (var pos in request.Positions)
        {
            // Convert to Base Currency (DZD)
            var baseValue = pos.NetAmount * pos.CurrentSpotRateToDzd;
            totalBaseValue += (pos.Direction == ExposureDirection.NetReceivable ? baseValue : -baseValue);

            // Position variance component: (V * sigma)^2
            var posStdDev = (double)baseValue * pos.AnnualizedVolatilitySigma * timeHorizonFactor;
            totalVarianceSum += posStdDev * posStdDev;

            // Forward contract pricing: Interest Rate Parity approximation
            // Forward = Spot * (1 + r_base * t) / (1 + r_foreign * t)
            // For Algerian Dinar vs EUR/USD, forward premium is typically ~2-4% annualized
            var forwardPointsMultiplier = 1.0m + (0.035m * (request.HorizonDays / 365.0m));
            var forwardRate = pos.CurrentSpotRateToDzd * forwardPointsMultiplier;

            if (baseValue >= 100000.0m) // Material exposure threshold
            {
                var action = pos.Direction == ExposureDirection.NetPayable ? "Buy Forward" : "Sell Forward";
                recommendations.Add(new ForwardHedgeRecommendation(
                    Currency: pos.Currency,
                    ExposureToHedge: pos.Direction,
                    NotionalAmount: pos.NetAmount,
                    CurrentSpotRate: pos.CurrentSpotRateToDzd,
                    ForwardHedgeRate: Math.Round(forwardRate, 4),
                    ForwardTenorDays: request.HorizonDays,
                    RecommendedInstrument: "ForwardContract",
                    Rationale: $"{action} {pos.NetAmount:N0} {pos.Currency} to lock in {forwardRate:F2} {request.BaseCurrency} rate and immunize against volatility."
                ));
            }
        }

        var portfolioStdDev = Math.Sqrt(totalVarianceSum);
        var parametricVaR = (decimal)(zScore * portfolioStdDev);
        var conditionalVaR = (decimal)(cvarMultiplier * portfolioStdDev);

        var hedgeRecommended = parametricVaR >= request.MaxLossToleranceBase || recommendations.Count > 0;

        return new ValueAtRiskReport(
            ReportId: $"VAR-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            TenantId: request.TenantId,
            BaseCurrency: request.BaseCurrency,
            TotalNetPortfolioValueBase: Math.Round(Math.Abs(totalBaseValue), 2),
            ConfidenceLevel: request.ConfidenceLevel,
            HorizonDays: request.HorizonDays,
            ParametricVaRBase: Math.Round(parametricVaR, 2),
            ConditionalVaRExpectedShortfallBase: Math.Round(conditionalVaR, 2),
            MaxTolerableLossBase: request.MaxLossToleranceBase,
            HedgeRecommended: hedgeRecommended,
            Recommendations: recommendations,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }
}
