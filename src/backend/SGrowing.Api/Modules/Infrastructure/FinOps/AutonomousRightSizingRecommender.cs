namespace SGrowing.Api.Modules.Infrastructure.FinOps;

/// <summary>
/// Autonomous rightsizing advisor evaluating historical P99 CPU and memory profiles
/// to detect overprovisioned workloads and recommend non-disruptive downsized resource allocations.
/// </summary>
public sealed class AutonomousRightSizingRecommender
{
    private const double SafetyHeadroomMultiplier = 1.25; // 25% buffer above P99 peak
    private const double HoursPerMonth = 730.0;

    /// <summary>
    /// Evaluates workload utilization metrics and generates a rightsizing recommendation.
    /// </summary>
    public RightSizingRecommendation AnalyzeWorkload(WorkloadUsageMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var isCpuOverprovisioned = metrics.P99CpuUtilizationPercent < 45.0;
        var isMemOverprovisioned = metrics.P99MemoryUtilizationPercent < 50.0;
        var isOverprovisioned = isCpuOverprovisioned || isMemOverprovisioned;

        var currentMonthlyCost = Math.Round(metrics.CurrentHourlyCostDzd * (decimal)HoursPerMonth, 2);

        if (!isOverprovisioned)
        {
            return new RightSizingRecommendation(
                WorkloadId: metrics.WorkloadId,
                ServiceName: metrics.ServiceName,
                CurrentCpuCores: metrics.CurrentAllocatedCpuCores,
                RecommendedCpuCores: metrics.CurrentAllocatedCpuCores,
                CurrentMemoryGb: metrics.CurrentAllocatedMemoryGb,
                RecommendedMemoryGb: metrics.CurrentAllocatedMemoryGb,
                CurrentMonthlyCostDzd: currentMonthlyCost,
                OptimizedMonthlyCostDzd: currentMonthlyCost,
                MonthlySavingsDzd: 0m,
                SavingsPercentage: 0.0,
                IsOverprovisioned: false
            );
        }

        // Calculate right-sized allocations with 25% safety headroom above P99 peak
        var targetCpu = (metrics.CurrentAllocatedCpuCores * (metrics.P99CpuUtilizationPercent / 100.0)) * SafetyHeadroomMultiplier;
        var recommendedCpu = Math.Max(0.5, Math.Round(targetCpu, 1));

        var targetMem = (metrics.CurrentAllocatedMemoryGb * (metrics.P99MemoryUtilizationPercent / 100.0)) * SafetyHeadroomMultiplier;
        var recommendedMem = Math.Max(1.0, Math.Round(targetMem, 1));

        // Ensure recommendation never exceeds current allocation
        recommendedCpu = Math.Min(recommendedCpu, metrics.CurrentAllocatedCpuCores);
        recommendedMem = Math.Min(recommendedMem, metrics.CurrentAllocatedMemoryGb);

        // Cost reduction estimation (60% weight CPU, 40% weight RAM)
        var cpuRatio = recommendedCpu / metrics.CurrentAllocatedCpuCores;
        var memRatio = recommendedMem / metrics.CurrentAllocatedMemoryGb;
        var compositeScaleRatio = (0.60 * cpuRatio) + (0.40 * memRatio);

        var optimizedMonthlyCost = Math.Round(currentMonthlyCost * (decimal)compositeScaleRatio, 2);
        var monthlySavings = Math.Max(0m, currentMonthlyCost - optimizedMonthlyCost);
        var savingsPercent = currentMonthlyCost > 0m ? (double)(monthlySavings / currentMonthlyCost) * 100.0 : 0.0;

        return new RightSizingRecommendation(
            WorkloadId: metrics.WorkloadId,
            ServiceName: metrics.ServiceName,
            CurrentCpuCores: metrics.CurrentAllocatedCpuCores,
            RecommendedCpuCores: recommendedCpu,
            CurrentMemoryGb: metrics.CurrentAllocatedMemoryGb,
            RecommendedMemoryGb: recommendedMem,
            CurrentMonthlyCostDzd: currentMonthlyCost,
            OptimizedMonthlyCostDzd: optimizedMonthlyCost,
            MonthlySavingsDzd: monthlySavings,
            SavingsPercentage: Math.Round(savingsPercent, 1),
            IsOverprovisioned: true
        );
    }
}
