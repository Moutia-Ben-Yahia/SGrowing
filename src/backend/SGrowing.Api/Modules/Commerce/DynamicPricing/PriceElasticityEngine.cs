namespace SGrowing.Api.Modules.Commerce.DynamicPricing;

#region Enums & DTOs

/// <summary>
/// Economic classification of price elasticity of demand.
/// </summary>
public enum ElasticityCategory
{
    /// <summary>|ε| &lt; 1.0: Demand changes by a smaller percentage than price.</summary>
    Inelastic,

    /// <summary>|ε| ≈ 1.0: Percentage change in quantity equals percentage change in price.</summary>
    Unitary,

    /// <summary>|ε| &gt; 1.0: Demand changes by a larger percentage than price.</summary>
    Elastic
}

/// <summary>
/// Parameters for calculating arc price elasticity of demand using the midpoint formula.
/// </summary>
public record ArcElasticityRequest(
    decimal PriceInitial,
    decimal QuantityInitial,
    decimal PriceFinal,
    decimal QuantityFinal);

/// <summary>
/// Result of an arc price elasticity analysis.
/// </summary>
public record ArcElasticityResult(
    decimal Elasticity,
    decimal AbsoluteElasticity,
    ElasticityCategory Category,
    decimal PriceDeltaPercent,
    decimal QuantityDeltaPercent,
    string Interpretation);

/// <summary>
/// Parameters for finding the optimal price point that maximizes contribution margin.
/// </summary>
public record OptimalPriceSearchRequest(
    decimal BasePrice,
    decimal BaseDemand,
    decimal UnitCost,
    decimal Elasticity,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    decimal StepSize = 1.0m,
    IReadOnlyList<decimal>? CandidatePrices = null);

/// <summary>
/// Detailed evaluation of a specific candidate price point.
/// </summary>
public record PriceMarginPoint(
    decimal Price,
    decimal EstimatedDemand,
    decimal UnitMargin,
    decimal TotalContributionMargin);

/// <summary>
/// Optimization output indicating the profit-maximizing price and expected margin lift.
/// </summary>
public record OptimalPriceResult(
    decimal OptimalPrice,
    decimal EstimatedDemand,
    decimal UnitMargin,
    decimal TotalContributionMargin,
    decimal BaselinePrice,
    decimal BaselineDemand,
    decimal BaselineMargin,
    decimal MarginLiftPercentage,
    IReadOnlyList<PriceMarginPoint> EvaluatedPoints);

/// <summary>
/// Result of adjusting price for inventory velocity (days of supply) and time-of-day traffic.
/// </summary>
public record VelocityAndTrafficAdjustment(
    decimal BasePrice,
    decimal AdjustedPrice,
    decimal StockVelocityMultiplier,
    decimal TimeOfDayMultiplier,
    decimal CombinedMultiplier,
    string VelocityReasoning,
    string TrafficReasoning);

#endregion

/// <summary>
/// Contract for quantitative price elasticity modeling, demand curve estimation,
/// margin maximization, inventory velocity adjustments, and time-of-day traffic weighting.
/// </summary>
public interface IPriceElasticityEngine
{
    ArcElasticityResult CalculateArcElasticity(ArcElasticityRequest request);
    ArcElasticityResult CalculateArcElasticity(decimal p1, decimal q1, decimal p2, decimal q2);
    ElasticityCategory ClassifyElasticity(decimal elasticity);
    decimal EstimateDemand(decimal targetPrice, decimal basePrice, decimal baseDemand, decimal elasticity);
    OptimalPriceResult ComputeOptimalPrice(OptimalPriceSearchRequest request);
    decimal CalculateStockVelocityMultiplier(decimal daysOfSupply);
    decimal CalculateTimeOfDayMultiplier(int hourOfDay, decimal? trafficIndex = null);
    VelocityAndTrafficAdjustment AdjustPriceForVelocityAndTraffic(
        decimal basePrice,
        decimal daysOfSupply,
        int? hourOfDay = null,
        decimal? trafficIndex = null);
}

/// <summary>
/// Quantitative price elasticity engine implementing arc elasticity (midpoint formula),
/// inverted demand curve margin maximization, inventory days-of-supply decay, and traffic surge weighting.
/// </summary>
public sealed class PriceElasticityEngine : IPriceElasticityEngine
{
    private const decimal UnitaryTolerance = 0.05m; // Tolerance for classifying unitary elasticity (|ε| ∈ [0.95, 1.05])

    /// <summary>
    /// Calculates arc price elasticity of demand:
    /// ε = ((Q2 - Q1) / (Q1 + Q2)) / ((P2 - P1) / (P1 + P2))
    /// </summary>
    public ArcElasticityResult CalculateArcElasticity(ArcElasticityRequest request) =>
        CalculateArcElasticity(request.PriceInitial, request.QuantityInitial, request.PriceFinal, request.QuantityFinal);

    /// <summary>
    /// Calculates arc price elasticity of demand using discrete price and quantity observations.
    /// </summary>
    public ArcElasticityResult CalculateArcElasticity(decimal p1, decimal q1, decimal p2, decimal q2)
    {
        // Guard against negative inputs
        if (p1 < 0 || p2 < 0 || q1 < 0 || q2 < 0)
        {
            throw new ArgumentOutOfRangeException("Prices and quantities must be non-negative.");
        }

        // Edge case: Price did not change
        if (p1 == p2)
        {
            return new ArcElasticityResult(
                Elasticity: 0.0000m,
                AbsoluteElasticity: 0.0000m,
                Category: ElasticityCategory.Inelastic,
                PriceDeltaPercent: 0m,
                QuantityDeltaPercent: q1 > 0 ? Math.Round(((q2 - q1) / q1) * 100m, 2) : 0m,
                Interpretation: "Price remained identical (ΔP = 0); price elasticity is undefined / zero.");
        }

        // Edge case: Quantities sum to zero or prices sum to zero
        if ((q1 + q2) == 0m || (p1 + p2) == 0m)
        {
            return new ArcElasticityResult(
                Elasticity: 0.0000m,
                AbsoluteElasticity: 0.0000m,
                Category: ElasticityCategory.Inelastic,
                PriceDeltaPercent: p1 > 0 ? Math.Round(((p2 - p1) / p1) * 100m, 2) : 0m,
                QuantityDeltaPercent: 0m,
                Interpretation: "Zero total quantity or price observed; elasticity defaults to zero.");
        }

        // Edge case: Quantity did not change (perfectly inelastic)
        if (q1 == q2)
        {
            return new ArcElasticityResult(
                Elasticity: 0.0000m,
                AbsoluteElasticity: 0.0000m,
                Category: ElasticityCategory.Inelastic,
                PriceDeltaPercent: Math.Round(((p2 - p1) / p1) * 100m, 2),
                QuantityDeltaPercent: 0m,
                Interpretation: "Quantity remained constant despite price change (Perfectly Inelastic demand).");
        }

        // Midpoint calculation:
        // Numerator = (Q2 - Q1) / (Q1 + Q2)
        // Denominator = (P2 - P1) / (P1 + P2)
        decimal deltaQ = q2 - q1;
        decimal sumQ = q1 + q2;
        decimal deltaP = p2 - p1;
        decimal sumP = p1 + p2;

        decimal numerator = deltaQ / sumQ;
        decimal denominator = deltaP / sumP;

        decimal elasticity = Math.Round(numerator / denominator, 4);
        decimal absElasticity = Math.Abs(elasticity);
        var category = ClassifyElasticity(elasticity);

        decimal priceDeltaPercent = Math.Round(((p2 - p1) / p1) * 100m, 2);
        decimal quantityDeltaPercent = Math.Round(((q2 - q1) / q1) * 100m, 2);

        string interpretation = category switch
        {
            ElasticityCategory.Elastic =>
                $"Elastic demand (|ε| = {absElasticity:F2} > 1.0): Demand is sensitive to price changes. A {Math.Abs(priceDeltaPercent):F1}% price increase results in a {Math.Abs(quantityDeltaPercent):F1}% volume decline.",
            ElasticityCategory.Inelastic =>
                $"Inelastic demand (|ε| = {absElasticity:F2} < 1.0): Demand is relatively insensitive to price shifts. Price increases expand gross revenue.",
            ElasticityCategory.Unitary =>
                $"Unitary elasticity (|ε| = {absElasticity:F2} ≈ 1.0): Proportional changes in price are matched by equal proportional changes in demand. Total revenue remains stable.",
            _ => "Unknown elasticity classification."
        };

        return new ArcElasticityResult(
            Elasticity: elasticity,
            AbsoluteElasticity: absElasticity,
            Category: category,
            PriceDeltaPercent: priceDeltaPercent,
            QuantityDeltaPercent: quantityDeltaPercent,
            Interpretation: interpretation);
    }

    /// <summary>
    /// Classifies elasticity value into Elastic, Inelastic, or Unitary.
    /// </summary>
    public ElasticityCategory ClassifyElasticity(decimal elasticity)
    {
        decimal abs = Math.Abs(elasticity);
        if (Math.Abs(abs - 1.0m) <= UnitaryTolerance)
        {
            return ElasticityCategory.Unitary;
        }

        return abs > 1.0m + UnitaryTolerance ? ElasticityCategory.Elastic : ElasticityCategory.Inelastic;
    }

    /// <summary>
    /// Inverts the arc elasticity midpoint formula to estimate demand Q at a hypothetical target price P:
    /// (Q - Q1) / (Q1 + Q) = ε * (P - P1) / (P1 + P)
    /// Solving for Q yields: Q = Q1 * (1 + X) / (1 - X), where X = ε * (P - P1) / (P1 + P).
    /// </summary>
    public decimal EstimateDemand(decimal targetPrice, decimal basePrice, decimal baseDemand, decimal elasticity)
    {
        if (targetPrice <= 0m || basePrice <= 0m || baseDemand <= 0m)
        {
            return 0m;
        }

        if (targetPrice == basePrice || elasticity == 0m)
        {
            return baseDemand;
        }

        decimal priceRatio = (targetPrice - basePrice) / (basePrice + targetPrice);
        decimal x = elasticity * priceRatio;

        // As price rises sharply in elastic demand, X can approach or exceed -1 or 1.
        // Guard against zero denominator or negative demand
        if ((1.0m - x) <= 0.0001m)
        {
            return 0m;
        }

        decimal estimated = baseDemand * ((1.0m + x) / (1.0m - x));
        return Math.Max(0m, Math.Round(estimated, 2));
    }

    /// <summary>
    /// Computes the optimal price maximizing total contribution margin:
    /// Margin(P) = (P - UnitCost) * EstimatedDemand(P)
    /// </summary>
    public OptimalPriceResult ComputeOptimalPrice(OptimalPriceSearchRequest request)
    {
        if (request.BasePrice <= 0m)
            throw new ArgumentException("Base price must be positive.", nameof(request.BasePrice));
        if (request.UnitCost < 0m)
            throw new ArgumentException("Unit cost cannot be negative.", nameof(request.UnitCost));

        decimal baselineMargin = (request.BasePrice - request.UnitCost) * request.BaseDemand;

        var evaluatedPoints = new List<PriceMarginPoint>();

        // If discrete candidates provided, evaluate them
        if (request.CandidatePrices is { Count: > 0 })
        {
            foreach (var price in request.CandidatePrices)
            {
                if (price < request.UnitCost) continue; // Skip loss-making candidates

                decimal demand = EstimateDemand(price, request.BasePrice, request.BaseDemand, request.Elasticity);
                decimal unitMargin = price - request.UnitCost;
                decimal totalMargin = unitMargin * demand;
                evaluatedPoints.Add(new PriceMarginPoint(price, demand, unitMargin, totalMargin));
            }
        }
        else
        {
            // Range search
            decimal minPrice = request.MinPrice ?? Math.Max(request.UnitCost * 1.05m, request.BasePrice * 0.50m);
            decimal maxPrice = request.MaxPrice ?? (request.BasePrice * 1.80m);
            decimal step = request.StepSize > 0m ? request.StepSize : 1.0m;

            if (minPrice > maxPrice)
            {
                (minPrice, maxPrice) = (maxPrice, minPrice);
            }

            // Cap evaluation steps to avoid excessive CPU loops
            int stepCount = (int)Math.Min(500, Math.Max(1, (maxPrice - minPrice) / step));
            decimal actualStep = (maxPrice - minPrice) / stepCount;

            for (int i = 0; i <= stepCount; i++)
            {
                decimal price = Math.Round(minPrice + (i * actualStep), 2);
                decimal demand = EstimateDemand(price, request.BasePrice, request.BaseDemand, request.Elasticity);
                decimal unitMargin = price - request.UnitCost;
                decimal totalMargin = unitMargin * demand;
                evaluatedPoints.Add(new PriceMarginPoint(price, demand, unitMargin, totalMargin));
            }
        }

        // If no valid points were evaluated, fallback to BasePrice
        if (evaluatedPoints.Count == 0)
        {
            decimal unitMargin = request.BasePrice - request.UnitCost;
            return new OptimalPriceResult(
                OptimalPrice: request.BasePrice,
                EstimatedDemand: request.BaseDemand,
                UnitMargin: unitMargin,
                TotalContributionMargin: baselineMargin,
                BaselinePrice: request.BasePrice,
                BaselineDemand: request.BaseDemand,
                BaselineMargin: baselineMargin,
                MarginLiftPercentage: 0m,
                EvaluatedPoints: evaluatedPoints);
        }

        // Select the candidate maximizing TotalContributionMargin
        var optimal = evaluatedPoints
            .OrderByDescending(p => p.TotalContributionMargin)
            .ThenByDescending(p => p.Price)
            .First();

        decimal marginLift = baselineMargin > 0m
            ? Math.Round(((optimal.TotalContributionMargin - baselineMargin) / baselineMargin) * 100m, 2)
            : 0m;

        return new OptimalPriceResult(
            OptimalPrice: optimal.Price,
            EstimatedDemand: optimal.EstimatedDemand,
            UnitMargin: optimal.UnitMargin,
            TotalContributionMargin: optimal.TotalContributionMargin,
            BaselinePrice: request.BasePrice,
            BaselineDemand: request.BaseDemand,
            BaselineMargin: baselineMargin,
            MarginLiftPercentage: marginLift,
            EvaluatedPoints: evaluatedPoints);
    }

    /// <summary>
    /// Calculates inventory velocity price multiplier based on days-of-supply:
    /// - Scarcity markup when inventory is critically low (&lt; 7 days): increases price by 5% to 25%.
    /// - Stagnant liquidation discount when inventory velocity is low (&gt; 60 days): discounts price by 2% to 15%.
    /// - Neutral (1.0x) for healthy stock (7 to 60 days).
    /// </summary>
    public decimal CalculateStockVelocityMultiplier(decimal daysOfSupply)
    {
        if (daysOfSupply < 0m) daysOfSupply = 0m;

        if (daysOfSupply < 7.0m)
        {
            // Scarcity markup: 0 days => 1.25 (+25%), 7 days => 1.05 (+5%)
            decimal scarcityFraction = Math.Clamp((7.0m - daysOfSupply) / 7.0m, 0m, 1m);
            return Math.Round(1.05m + (scarcityFraction * 0.20m), 4);
        }

        if (daysOfSupply > 60.0m)
        {
            // Excess inventory liquidation discount: 61 days => ~2% off, 180+ days => 15% off
            decimal excessFraction = Math.Clamp((daysOfSupply - 60.0m) / 120.0m, 0m, 1m);
            decimal discount = 0.02m + (excessFraction * 0.13m);
            return Math.Round(1.0m - discount, 4);
        }

        return 1.0000m;
    }

    /// <summary>
    /// Calculates time-of-day traffic weighting multiplier:
    /// - Peak prime shopping hours (18:00 - 22:00): +5% surge
    /// - Midday lunch surge (12:00 - 14:00): +2% surge
    /// - Deep night off-peak (01:00 - 05:00): -4% discount
    /// - Live traffic index override if provided.
    /// </summary>
    public decimal CalculateTimeOfDayMultiplier(int hourOfDay, decimal? trafficIndex = null)
    {
        if (trafficIndex.HasValue)
        {
            // E.g. trafficIndex = 1.30 => 1.0 + (0.30 * 0.10) = 1.03x
            decimal indexDelta = Math.Clamp(trafficIndex.Value - 1.0m, -0.5m, 1.0m);
            return Math.Round(1.0m + (indexDelta * 0.10m), 4);
        }

        int normalizedHour = ((hourOfDay % 24) + 24) % 24;

        return normalizedHour switch
        {
            >= 18 and <= 22 => 1.0500m, // Evening prime peak (+5%)
            >= 12 and <= 14 => 1.0200m, // Lunch peak (+2%)
            >= 1 and <= 5 => 0.9600m,   // Overnight off-peak (-4%)
            _ => 1.0000m                // Standard daytime/morning
        };
    }

    /// <summary>
    /// Applies combined stock velocity and time-of-day traffic multipliers to a base price.
    /// </summary>
    public VelocityAndTrafficAdjustment AdjustPriceForVelocityAndTraffic(
        decimal basePrice,
        decimal daysOfSupply,
        int? hourOfDay = null,
        decimal? trafficIndex = null)
    {
        decimal velocityMult = CalculateStockVelocityMultiplier(daysOfSupply);
        decimal trafficMult = hourOfDay.HasValue || trafficIndex.HasValue
            ? CalculateTimeOfDayMultiplier(hourOfDay ?? 12, trafficIndex)
            : 1.0000m;

        decimal combined = Math.Round(velocityMult * trafficMult, 4);
        decimal adjustedPrice = Math.Round(basePrice * combined, 2);

        string velocityReasoning = daysOfSupply switch
        {
            < 7.0m => $"Critically low inventory ({daysOfSupply:F1} days): applied scarcity markup factor of {velocityMult:F4} (+{((velocityMult - 1m) * 100):F1}%) to ration stock.",
            > 60.0m => $"Stagnant excess inventory ({daysOfSupply:F1} days): applied liquidation discount factor of {velocityMult:F4} (-{((1m - velocityMult) * 100):F1}%) to accelerate stock turnover.",
            _ => $"Healthy inventory velocity ({daysOfSupply:F1} days): neutral multiplier {velocityMult:F4}."
        };

        string trafficReasoning = trafficMult != 1.0000m
            ? $"Traffic weighting applied factor of {trafficMult:F4} based on hour {hourOfDay?.ToString() ?? "traffic index"}."
            : "Standard traffic volume: neutral multiplier (1.0000).";

        return new VelocityAndTrafficAdjustment(
            BasePrice: basePrice,
            AdjustedPrice: adjustedPrice,
            StockVelocityMultiplier: velocityMult,
            TimeOfDayMultiplier: trafficMult,
            CombinedMultiplier: combined,
            VelocityReasoning: velocityReasoning,
            TrafficReasoning: trafficReasoning);
    }
}
