namespace SGrowing.Api.Modules.SupplyChain.DisruptionSimulator;

/// <summary>
/// Stochastic Monte Carlo simulation engine executing multi-thousand run stress tests
/// to quantify supply chain resilience, Value at Risk (VaR), and stockout probabilities under black swan events.
/// </summary>
public sealed class MonteCarloDisruptionSimulator
{
    /// <summary>
    /// Executes a Monte Carlo disruption stress test over the supplied supply chain network.
    /// </summary>
    public MonteCarloSimulationResult RunSimulation(RunSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scenario = request.Scenario;
        var iterations = Math.Clamp(scenario.IterationCount, 100, 10000);
        var horizonDays = Math.Clamp(scenario.SimulationHorizonDays, 7, 365);

        var fulfillmentRatios = new double[iterations];
        var lostRevenues = new decimal[iterations];
        var stockoutOccurredCount = 0;

        // Base network lead time
        var baseLeadTimeDays = request.Nodes.Sum(n => n.LeadTimeDaysMean) + request.Links.Sum(l => l.TransitDaysMean);
        if (baseLeadTimeDays <= 0.0) baseLeadTimeDays = 14.0;

        var bottleneckNodes = request.Nodes
            .Where(n => n.AnnualFailureProbability > 0.10 || request.Links.Any(l => l.FromNodeId == n.NodeId && l.IsSingleSourceDependency))
            .Select(n => n.Name)
            .ToList();

        if (bottleneckNodes.Count == 0 && request.Nodes.Count > 0)
        {
            bottleneckNodes.Add(request.Nodes[0].Name);
        }

        // Run Monte Carlo trials
        for (var i = 0; i < iterations; i++)
        {
            var initialInventory = (int)(scenario.DailyDemandMean * baseLeadTimeDays * 1.5);
            var onHandInventory = initialInventory;
            var totalDemand = 0.0;
            var totalFulfilled = 0.0;
            var stockoutInTrial = false;

            // Sample whether an injected disruption hits during this horizon
            var disruptionActive = false;
            var disruptionDelayDays = 0;

            if (scenario.InjectedDisruptions.Count > 0)
            {
                // Disruption hit probability ~ 35% in stress scenario
                if (Random.Shared.NextDouble() < 0.35)
                {
                    disruptionActive = true;
                    disruptionDelayDays = Random.Shared.Next(10, 30);
                }
            }

            var daysUntilReplenishment = (int)Math.Max(1.0, SampleGaussian(baseLeadTimeDays, baseLeadTimeDays * 0.15));
            if (disruptionActive)
            {
                daysUntilReplenishment += disruptionDelayDays;
            }

            for (var day = 0; day < horizonDays; day++)
            {
                var dailyDemand = Math.Max(0.0, SampleGaussian(scenario.DailyDemandMean, scenario.DailyDemandStdDev));
                totalDemand += dailyDemand;

                // Arrival of replenishment shipment
                if (day == daysUntilReplenishment)
                {
                    onHandInventory += (int)(scenario.DailyDemandMean * baseLeadTimeDays);
                    // Schedule next replenishment
                    daysUntilReplenishment = day + (int)Math.Max(1.0, SampleGaussian(baseLeadTimeDays, baseLeadTimeDays * 0.15));
                }

                if (onHandInventory >= dailyDemand)
                {
                    onHandInventory -= (int)dailyDemand;
                    totalFulfilled += dailyDemand;
                }
                else
                {
                    totalFulfilled += onHandInventory;
                    onHandInventory = 0;
                    stockoutInTrial = true;
                }
            }

            if (stockoutInTrial)
            {
                stockoutOccurredCount++;
            }

            var unfulfilled = Math.Max(0.0, totalDemand - totalFulfilled);
            var ratio = totalDemand > 0.0 ? totalFulfilled / totalDemand : 1.0;

            fulfillmentRatios[i] = Math.Clamp(ratio, 0.0, 1.0);
            lostRevenues[i] = (decimal)unfulfilled * scenario.UnitSellingPriceDzd;
        }

        // Statistical summarization
        Array.Sort(fulfillmentRatios);
        Array.Sort(lostRevenues);

        var meanFulfillment = fulfillmentRatios.Average();
        // 5th percentile of fulfillment (P95 worst case)
        var p95Idx = (int)(iterations * 0.05);
        var p99Idx = (int)(iterations * 0.01);

        var p95Fulfillment = fulfillmentRatios[Math.Clamp(p95Idx, 0, iterations - 1)];
        var p99Fulfillment = fulfillmentRatios[Math.Clamp(p99Idx, 0, iterations - 1)];

        // 95% Value at Risk: 95th percentile of lost revenue
        var var95Idx = (int)(iterations * 0.95);
        var var95 = lostRevenues[Math.Clamp(var95Idx, 0, iterations - 1)];

        var stockoutProb = (double)stockoutOccurredCount / iterations;

        return new MonteCarloSimulationResult(
            ScenarioId: scenario.ScenarioId,
            TargetSku: scenario.TargetSku,
            IterationsCompleted: iterations,
            MeanFulfillmentRatio: Math.Round(meanFulfillment, 4),
            P95FulfillmentRatio: Math.Round(p95Fulfillment, 4),
            P99FulfillmentRatio: Math.Round(p99Fulfillment, 4),
            ValueAtRisk95Dzd: var95,
            StockoutProbability: Math.Round(stockoutProb, 4),
            CriticalBottlenecks: bottleneckNodes,
            SimulatedAtUtc: DateTime.UtcNow
        );
    }

    private static double SampleGaussian(double mean, double stdDev)
    {
        var u1 = 1.0 - Random.Shared.NextDouble();
        var u2 = 1.0 - Random.Shared.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + (stdDev * randStdNormal);
    }
}
