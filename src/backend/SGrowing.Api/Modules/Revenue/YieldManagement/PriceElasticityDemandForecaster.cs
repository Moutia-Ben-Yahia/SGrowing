namespace SGrowing.Api.Modules.Revenue.YieldManagement;

/// <summary>
/// Econometric forecaster calculating price elasticity of demand and optimal revenue-maximizing prices.
/// </summary>
public sealed class PriceElasticityDemandForecaster
{
    /// <summary>
    /// Calculates mid-point arc price elasticity and optimal price given historical price/demand observations.
    /// </summary>
    public PriceElasticityReport CalculateElasticity(
        CapacityType capacityType,
        decimal price1Dzd,
        double demand1,
        decimal price2Dzd,
        double demand2,
        decimal marginalCostDzd,
        double crossPriceElasticityWithSubstitute = 0.45)
    {
        if (price1Dzd <= 0m || price2Dzd <= 0m)
        {
            throw new ArgumentException("Prices must be positive.");
        }

        if (demand1 <= 0.0 || demand2 <= 0.0)
        {
            throw new ArgumentException("Demand observations must be positive.");
        }

        var p1 = (double)price1Dzd;
        var p2 = (double)price2Dzd;
        var c = (double)marginalCostDzd;

        var dQ = demand2 - demand1;
        var avgQ = (demand1 + demand2) / 2.0;

        var dP = p2 - p1;
        var avgP = (p1 + p2) / 2.0;

        if (Math.Abs(dP) < 0.0001)
        {
            throw new InvalidOperationException("Price points cannot be identical.");
        }

        // Arc elasticity: % dQ / % dP
        var elasticity = (dQ / avgQ) / (dP / avgP);

        // Linear demand parameter estimation: Q(P) = a - b * P
        var b = -dQ / dP;
        if (b <= 0.0)
        {
            b = 0.01; // Avoid non-downward sloping demand instability
        }
        var a = demand1 + (b * p1);

        // Revenue maximizing price with marginal cost c: P* = (a + b * c) / (2 * b)
        var optimalP = (a + (b * c)) / (2.0 * b);
        var optimalPriceDzd = Math.Round((decimal)Math.Max(optimalP, c * 1.1), 2);

        var absE = Math.Abs(elasticity);
        var classification = absE switch
        {
            > 1.05 => "Elastic",
            < 0.95 => "Inelastic",
            _ => "Unitary"
        };

        return new PriceElasticityReport(
            CapacityType: capacityType,
            BasePriceDzd: price1Dzd,
            OptimalRevenuePriceDzd: optimalPriceDzd,
            PointElasticity: Math.Round(elasticity, 4),
            CrossPriceElasticityWithSubstitute: Math.Round(crossPriceElasticityWithSubstitute, 4),
            ElasticityClassification: classification
        );
    }
}
