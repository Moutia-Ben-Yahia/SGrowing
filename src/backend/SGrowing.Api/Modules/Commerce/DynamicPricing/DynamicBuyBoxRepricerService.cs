namespace SGrowing.Api.Modules.Commerce.DynamicPricing;

#region Enums & DTOs

/// <summary>
/// Repricing strategy type.
/// </summary>
public enum RepricingStrategy
{
    /// <summary>Undercut lowest in-stock competitor by a small margin delta while respecting floor.</summary>
    AggressiveUndercut,

    /// <summary>Align price with the median observed market price.</summary>
    MatchMarketMedian,

    /// <summary>Set price to the elasticity-optimal contribution margin maximizing price point.</summary>
    MarginMaximizer
}

/// <summary>
/// Input parameters for evaluating a dynamic repricing decision.
/// </summary>
public record RepricingRequest(
    Guid TenantId,
    string Sku,
    decimal CurrentPrice,
    RepricingStrategy Strategy,
    MarginCostProfile CostProfile,
    decimal UndercutDelta = 0.05m,
    decimal? DaysOfSupply = null,
    int? HourOfDay = null,
    decimal? TrafficIndex = null,
    decimal? BaselineDemand = null,
    decimal? EstimatedElasticity = null);

/// <summary>
/// Structured repricing decision output produced by the BuyBox repricer engine.
/// </summary>
public record RepricingRecommendation(
    string Sku,
    decimal OriginalPrice,
    decimal RecommendedPrice,
    decimal FloorPrice,
    RepricingStrategy StrategyApplied,
    decimal? MarketLowestPrice,
    decimal? MarketLowestInStockPrice,
    decimal? MarketMedianPrice,
    decimal? PriceSpreadPercentage,
    bool StockOutOpportunity,
    decimal ExpectedUnitMargin,
    decimal ExpectedMarginPercentage,
    decimal MarginChangePercentage,
    decimal? ExpectedDemand,
    decimal? ExpectedRevenue,
    string StrategyJustification,
    MarginValidationResult GuardrailValidation,
    DateTime TimestampUtc);

#endregion

/// <summary>
/// Central repricing engine that determines competitive pricing decisions across AggressiveUndercut,
/// MatchMarketMedian, and MarginMaximizer strategies while enforcing strict margin floor guardrails.
/// </summary>
public interface IDynamicBuyBoxRepricerService
{
    Task<RepricingRecommendation> RepriceAsync(
        RepricingRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of the Dynamic BuyBox Repricer Service.
/// </summary>
public sealed class DynamicBuyBoxRepricerService(
    ICompetitorScraperMeshService scraperMesh,
    IPriceElasticityEngine elasticityEngine,
    IMarginGuardrailValidator guardrailValidator) : IDynamicBuyBoxRepricerService
{
    public async Task<RepricingRecommendation> RepriceAsync(
        RepricingRequest request,
        CancellationToken ct = default)
    {
        if (request.CurrentPrice <= 0m)
            throw new ArgumentException("Current price must be greater than zero.", nameof(request.CurrentPrice));

        // 1. Fetch real-time market intelligence
        var market = await scraperMesh.GetMarketMetricsAsync(request.TenantId, request.Sku, ct);

        // 2. Compute non-negotiable margin floor
        decimal floorPrice = guardrailValidator.CalculateFloorPrice(request.CostProfile);

        decimal tentativePrice;
        string justification;
        bool stockOutOpportunity = false;
        decimal? expectedDemand = null;

        // 3. Evaluate selected repricing strategy
        switch (request.Strategy)
        {
            case RepricingStrategy.AggressiveUndercut:
            {
                if (market.LowestInStockPrice.HasValue)
                {
                    decimal competitorTarget = market.LowestInStockPrice.Value;
                    tentativePrice = Math.Round(competitorTarget - request.UndercutDelta, 2);
                    justification = $"Aggressive undercut: set to {tentativePrice:C2} ({request.UndercutDelta:C2} below lowest in-stock competitor at {competitorTarget:C2}) to win BuyBox position.";
                }
                else if (market.AreAllCompetitorsOutOfStock)
                {
                    // Competitor stock-out detected! Capitalize by increasing price to capture excess margin
                    stockOutOpportunity = true;
                    decimal premiumMultiplier = 1.08m; // +8% stock-out premium
                    tentativePrice = Math.Round(Math.Max(request.CurrentPrice, market.MedianPrice) * premiumMultiplier, 2);
                    justification = $"Stock-out opportunity detected: All competitors are out of stock. Raised price to {tentativePrice:C2} (+8% premium) to maximize profit without volume loss.";
                }
                else
                {
                    // No competitor observations recorded
                    tentativePrice = request.CurrentPrice;
                    justification = "No competitor price signals available; maintaining current price.";
                }
                break;
            }

            case RepricingStrategy.MatchMarketMedian:
            {
                if (market.MedianPrice > 0m)
                {
                    // Prefer median of in-stock competitors if available, otherwise general median
                    tentativePrice = market.MedianInStockPrice ?? market.MedianPrice;
                    justification = $"Match Market Median: aligned price with market median ({tentativePrice:C2}) across {market.TotalCompetitorsObserved} competitors.";
                }
                else
                {
                    tentativePrice = request.CurrentPrice;
                    justification = "No market median available; maintaining current price.";
                }
                break;
            }

            case RepricingStrategy.MarginMaximizer:
            {
                decimal elasticity = request.EstimatedElasticity ?? -1.35m;
                decimal baselineDemand = request.BaselineDemand ?? 100m;

                // Price search bounded between floor price and market ceiling (or 1.6x base price)
                decimal searchCeiling = Math.Max(request.CurrentPrice * 1.50m, market.HighestPrice > 0 ? market.HighestPrice * 1.10m : request.CurrentPrice * 1.50m);

                var searchReq = new OptimalPriceSearchRequest(
                    BasePrice: request.CurrentPrice,
                    BaseDemand: baselineDemand,
                    UnitCost: request.CostProfile.Cogs,
                    Elasticity: elasticity,
                    MinPrice: floorPrice,
                    MaxPrice: searchCeiling,
                    StepSize: 0.50m);

                var optimalResult = elasticityEngine.ComputeOptimalPrice(searchReq);
                tentativePrice = optimalResult.OptimalPrice;
                expectedDemand = optimalResult.EstimatedDemand;

                justification = $"Margin Maximizer: identified optimal price {tentativePrice:C2} (ε = {elasticity:F2}), expected demand {expectedDemand:F0} units, yielding {optimalResult.MarginLiftPercentage:+0.0;-0.0;0.0}% contribution margin lift.";
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(request.Strategy), request.Strategy, "Unsupported repricing strategy.");
        }

        // 4. Apply inventory velocity and traffic surge multipliers if requested
        if (request.DaysOfSupply.HasValue || request.HourOfDay.HasValue || request.TrafficIndex.HasValue)
        {
            var adj = elasticityEngine.AdjustPriceForVelocityAndTraffic(
                tentativePrice,
                request.DaysOfSupply ?? 30m,
                request.HourOfDay,
                request.TrafficIndex);

            if (adj.CombinedMultiplier != 1.0000m)
            {
                tentativePrice = adj.AdjustedPrice;
                justification += $" [Velocity/Traffic Adjustment: factor {adj.CombinedMultiplier:F4} ({adj.VelocityReasoning} {adj.TrafficReasoning})].";
            }
        }

        // 5. Margin Guardrail Validation & Enforcement
        var validation = guardrailValidator.Validate(tentativePrice, request.CostProfile);
        decimal finalRecommendedPrice;

        if (!validation.IsValid)
        {
            // Clamp to floor price
            finalRecommendedPrice = validation.RecommendedClampedPrice;
            justification += $" [Floor Guardrail Enforced: Proposed price {tentativePrice:C2} breached margin floor {validation.FloorPrice:C2}; clamped to floor to preserve minimum required margin].";
        }
        else
        {
            finalRecommendedPrice = tentativePrice;
        }

        // 6. Calculate economic lift and metrics
        decimal originalBreakEven = request.CostProfile.Cogs + Math.Max(0m, request.CostProfile.EstimatedShippingCost);
        decimal originalNetMargin = request.CurrentPrice - originalBreakEven;
        decimal recommendedNetMargin = finalRecommendedPrice - validation.BreakEvenCost;
        decimal marginPct = finalRecommendedPrice > 0m
            ? Math.Round((recommendedNetMargin / finalRecommendedPrice) * 100m, 2)
            : 0m;

        decimal marginChangePct = originalNetMargin != 0m
            ? Math.Round(((recommendedNetMargin - originalNetMargin) / Math.Abs(originalNetMargin)) * 100m, 2)
            : 0m;

        decimal? expectedRevenue = expectedDemand.HasValue
            ? Math.Round(finalRecommendedPrice * expectedDemand.Value, 2)
            : null;

        return new RepricingRecommendation(
            Sku: request.Sku,
            OriginalPrice: request.CurrentPrice,
            RecommendedPrice: finalRecommendedPrice,
            FloorPrice: floorPrice,
            StrategyApplied: request.Strategy,
            MarketLowestPrice: market.LowestPrice > 0 ? market.LowestPrice : null,
            MarketLowestInStockPrice: market.LowestInStockPrice,
            MarketMedianPrice: market.MedianPrice > 0 ? market.MedianPrice : null,
            PriceSpreadPercentage: market.PriceSpreadPercentage > 0 ? market.PriceSpreadPercentage : null,
            StockOutOpportunity: stockOutOpportunity || market.StockOutOpportunityDetected,
            ExpectedUnitMargin: recommendedNetMargin,
            ExpectedMarginPercentage: marginPct,
            MarginChangePercentage: marginChangePct,
            ExpectedDemand: expectedDemand,
            ExpectedRevenue: expectedRevenue,
            StrategyJustification: justification,
            GuardrailValidation: validation,
            TimestampUtc: DateTime.UtcNow);
    }
}
