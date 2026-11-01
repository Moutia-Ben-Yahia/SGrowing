namespace SGrowing.Api.Modules.SupplyChain.DisruptionSimulator;

/// <summary>
/// Inventory buffer optimizer calculating optimal safety stock allocations and reorder points
/// under combined stochastic demand variance and lead-time volatility.
/// </summary>
public sealed class StrategicStockBufferOptimizer
{
    /// <summary>
    /// Computes the exact safety stock buffer required to guarantee the specified service level SLA.
    /// </summary>
    public SafetyStockOptimizationResult CalculateSafetyStock(CalculateSafetyStockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.DailyDemandMean <= 0.0 || request.LeadTimeDaysMean <= 0.0)
        {
            throw new ArgumentException("Demand and lead time means must be strictly positive.");
        }

        var z = GetZScoreForServiceLevel(request.TargetServiceLevelPercent);

        var dMean = request.DailyDemandMean;
        var dVar = Math.Pow(request.DailyDemandStdDev, 2);
        var lMean = request.LeadTimeDaysMean;
        var lVar = Math.Pow(request.LeadTimeDaysStdDev, 2);

        // Combined standard deviation of demand during lead time:
        // sigma_DL = sqrt(L_mean * sigma_D^2 + D_mean^2 * sigma_L^2)
        var combinedStdDev = Math.Sqrt((lMean * dVar) + (Math.Pow(dMean, 2) * lVar));

        var safetyStock = (int)Math.Ceiling(z * combinedStdDev);
        var expectedDemandDuringLeadTime = dMean * lMean;
        var reorderPoint = (int)Math.Ceiling(expectedDemandDuringLeadTime + safetyStock);

        var annualHoldingCost = (decimal)safetyStock * request.UnitHoldingCostAnnualDzd;

        return new SafetyStockOptimizationResult(
            ProductSku: request.ProductSku,
            DailyDemandMean: request.DailyDemandMean,
            LeadTimeDaysMean: request.LeadTimeDaysMean,
            RecommendedSafetyStockUnits: safetyStock,
            RecommendedReorderPointUnits: reorderPoint,
            AnnualHoldingCostDzd: annualHoldingCost,
            TargetServiceLevelPercent: request.TargetServiceLevelPercent
        );
    }

    private static double GetZScoreForServiceLevel(double serviceLevelPercent)
    {
        return serviceLevelPercent switch
        {
            >= 99.9 => 3.0902,
            >= 99.0 => 2.3263,
            >= 98.0 => 2.0537,
            >= 97.5 => 1.95996,
            >= 95.0 => 1.6449,
            >= 90.0 => 1.2816,
            >= 85.0 => 1.0364,
            >= 80.0 => 0.8416,
            _ => 1.2816
        };
    }
}
