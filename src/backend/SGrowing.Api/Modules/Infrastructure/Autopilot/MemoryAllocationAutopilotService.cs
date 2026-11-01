namespace SGrowing.Api.Modules.Infrastructure.Autopilot;

/// <summary>
/// Service interface for .NET Garbage Collection health monitoring and dynamic cache budget autopilot.
/// </summary>
public interface IMemoryAllocationAutopilotService
{
    /// <summary>
    /// Captures a live snapshot of .NET GC and heap telemetry.
    /// </summary>
    GcMemoryHealthReport CaptureMemorySnapshot();

    /// <summary>
    /// Evaluates current memory health, detects Gen 2 pressure/fragmentation, and autonomously adjusts cache budgets.
    /// </summary>
    GcMemoryHealthReport EvaluateMemoryHealth();

    /// <summary>
    /// Evaluates memory health with explicitly provided telemetry metrics (for deterministic testing and external telemetry ingest).
    /// </summary>
    GcMemoryHealthReport EvaluateMemoryHealth(int gen0, int gen1, int gen2, long totalAllocatedBytes, long heapSizeBytes, double memoryFragmentationPct);

    /// <summary>
    /// Applies a cache budget reduction to a target cache and logs the tuning action.
    /// </summary>
    MemoryTuningAction ApplyCacheBudgetTuning(string targetCache, int targetReductionPct, string? reason = null);

    /// <summary>
    /// Retrieves the audit trail of memory tuning actions.
    /// </summary>
    IReadOnlyList<MemoryTuningAction> GetTuningHistory();

    /// <summary>
    /// Gets the current active budget in MB for a target cache.
    /// </summary>
    double GetCurrentCacheBudgetMb(string targetCache);

    /// <summary>
    /// Sets or registers the base budget in MB for a target cache.
    /// </summary>
    void SetBaseCacheBudgetMb(string targetCache, double budgetMb);

    /// <summary>
    /// Resets tuning history and cache budgets back to defaults.
    /// </summary>
    void ResetAutopilotState();
}

/// <summary>
/// Production-grade implementation of <see cref="IMemoryAllocationAutopilotService"/>.
/// Real-time .NET Garbage Collection health monitor and autonomous memory tuner using <see cref="System.Threading.Lock"/>.
/// </summary>
public sealed class MemoryAllocationAutopilotService : IMemoryAllocationAutopilotService
{
    private readonly System.Threading.Lock _lock = new();
    private readonly List<MemoryTuningAction> _tuningHistory = [];
    private readonly Dictionary<string, (double BaseMb, double CurrentMb)> _cacheBudgets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["L2-Redis"] = (1024.0, 1024.0),
        ["HybridCache-Memory"] = (512.0, 512.0),
        ["QueryPlanCache"] = (256.0, 256.0)
    };

    private double _lastAppliedThrottlePct = -1.0;

    /// <inheritdoc />
    public GcMemoryHealthReport CaptureMemorySnapshot()
    {
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var allocatedBytes = GC.GetTotalAllocatedBytes();
        var memInfo = GC.GetGCMemoryInfo();
        var heapSizeBytes = memInfo.HeapSizeBytes > 0 ? memInfo.HeapSizeBytes : GC.GetTotalMemory(false);
        var fragPct = heapSizeBytes > 0 ? Math.Round(((double)memInfo.FragmentedBytes / heapSizeBytes) * 100.0, 2) : 0.0;

        return ComputeHealthReport(gen0, gen1, gen2, allocatedBytes, heapSizeBytes, fragPct, applyAutonomousTuning: false);
    }

    /// <inheritdoc />
    public GcMemoryHealthReport EvaluateMemoryHealth()
    {
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var allocatedBytes = GC.GetTotalAllocatedBytes();
        var memInfo = GC.GetGCMemoryInfo();
        var heapSizeBytes = memInfo.HeapSizeBytes > 0 ? memInfo.HeapSizeBytes : GC.GetTotalMemory(false);
        var fragPct = heapSizeBytes > 0 ? Math.Round(((double)memInfo.FragmentedBytes / heapSizeBytes) * 100.0, 2) : 0.0;

        return ComputeHealthReport(gen0, gen1, gen2, allocatedBytes, heapSizeBytes, fragPct, applyAutonomousTuning: true);
    }

    /// <inheritdoc />
    public GcMemoryHealthReport EvaluateMemoryHealth(int gen0, int gen1, int gen2, long totalAllocatedBytes, long heapSizeBytes, double memoryFragmentationPct)
    {
        return ComputeHealthReport(gen0, gen1, gen2, totalAllocatedBytes, heapSizeBytes, memoryFragmentationPct, applyAutonomousTuning: true);
    }

    /// <inheritdoc />
    public MemoryTuningAction ApplyCacheBudgetTuning(string targetCache, int targetReductionPct, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCache);

        targetReductionPct = Math.Clamp(targetReductionPct, 0, 100);

        lock (_lock)
        {
            if (!_cacheBudgets.TryGetValue(targetCache, out var budgets))
            {
                budgets = (BaseMb: 512.0, CurrentMb: 512.0);
            }

            var previousBudget = budgets.CurrentMb;
            var newBudget = Math.Round(budgets.BaseMb * (1.0 - (targetReductionPct / 100.0)), 2);
            _cacheBudgets[targetCache] = (budgets.BaseMb, newBudget);

            var defaultReason = targetReductionPct switch
            {
                >= 50 => "Critical GC Gen2 pressure or high fragmentation detected. Autonomously throttling cache budget by 50%.",
                >= 25 => "Elevated GC Gen2 pressure detected. Autonomously throttling cache budget by 25%.",
                0 => "GC health restored to Optimal. Cache budget fully restored to 100%.",
                _ => $"Target cache budget adjusted by {targetReductionPct}% reduction."
            };

            var action = new MemoryTuningAction(
                ActionId: $"tune-{Guid.NewGuid():N}",
                TargetCache: targetCache,
                PreviousBudgetMb: previousBudget,
                NewBudgetMb: newBudget,
                ReductionPct: targetReductionPct,
                Reason: reason ?? defaultReason,
                TimestampUtc: DateTimeOffset.UtcNow);

            _tuningHistory.Add(action);
            return action;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<MemoryTuningAction> GetTuningHistory()
    {
        lock (_lock)
        {
            return [.. _tuningHistory];
        }
    }

    /// <inheritdoc />
    public double GetCurrentCacheBudgetMb(string targetCache)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCache);

        lock (_lock)
        {
            if (_cacheBudgets.TryGetValue(targetCache, out var budgets))
            {
                return budgets.CurrentMb;
            }

            return 512.0;
        }
    }

    /// <inheritdoc />
    public void SetBaseCacheBudgetMb(string targetCache, double budgetMb)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCache);
        if (budgetMb <= 0) throw new ArgumentOutOfRangeException(nameof(budgetMb), "Budget must be positive.");

        lock (_lock)
        {
            _cacheBudgets[targetCache] = (BaseMb: budgetMb, CurrentMb: budgetMb);
        }
    }

    /// <inheritdoc />
    public void ResetAutopilotState()
    {
        lock (_lock)
        {
            _tuningHistory.Clear();
            _cacheBudgets["L2-Redis"] = (1024.0, 1024.0);
            _cacheBudgets["HybridCache-Memory"] = (512.0, 512.0);
            _cacheBudgets["QueryPlanCache"] = (256.0, 256.0);
            _lastAppliedThrottlePct = -1.0;
        }
    }

    private GcMemoryHealthReport ComputeHealthReport(
        int gen0,
        int gen1,
        int gen2,
        long totalAllocatedBytes,
        long heapSizeBytes,
        double memoryFragmentationPct,
        bool applyAutonomousTuning)
    {
        // Gen2 Pressure = Gen2Collections / (Gen0Collections + 1.0)
        var gen2PressureRatio = (double)gen2 / (gen0 + 1.0);
        var gen2PressurePct = Math.Round(gen2PressureRatio * 100.0, 2);

        // Urgency rules:
        // Gen 2 Pressure > 15% or Fragmentation > 25%: 'CriticalThrottling' -> 50% cache reduction.
        // Gen 2 Pressure > 8%: 'ElevatedPressure' -> 25% cache reduction.
        // Otherwise: 'Optimal' -> full 100% cache budget restored (0% reduction).
        GcHealthStatus status;
        double throttlePct;

        if (gen2PressurePct > 15.0 || memoryFragmentationPct > 25.0)
        {
            status = GcHealthStatus.CriticalThrottling;
            throttlePct = 50.0;
        }
        else if (gen2PressurePct > 8.0)
        {
            status = GcHealthStatus.ElevatedPressure;
            throttlePct = 25.0;
        }
        else
        {
            status = GcHealthStatus.Optimal;
            throttlePct = 0.0;
        }

        if (applyAutonomousTuning)
        {
            lock (_lock)
            {
                if (Math.Abs(_lastAppliedThrottlePct - throttlePct) > 0.01)
                {
                    _lastAppliedThrottlePct = throttlePct;

                    // Tune all registered caches
                    var targets = _cacheBudgets.Keys.ToList();
                    foreach (var target in targets)
                    {
                        var budgets = _cacheBudgets[target];
                        var previousBudget = budgets.CurrentMb;
                        var newBudget = Math.Round(budgets.BaseMb * (1.0 - (throttlePct / 100.0)), 2);
                        _cacheBudgets[target] = (budgets.BaseMb, newBudget);

                        var reason = status switch
                        {
                            GcHealthStatus.CriticalThrottling =>
                                $"Autopilot engaged CriticalThrottling: Gen2 Pressure ({gen2PressurePct:F1}%) or Fragmentation ({memoryFragmentationPct:F1}%) exceeded safety limit. 50% cache budget reduction applied.",
                            GcHealthStatus.ElevatedPressure =>
                                $"Autopilot engaged ElevatedPressure: Gen2 Pressure ({gen2PressurePct:F1}%) elevated. 25% cache budget reduction applied.",
                            _ =>
                                $"Autopilot restored Optimal status: Gen2 Pressure ({gen2PressurePct:F1}%) normalized. 100% cache budget restored."
                        };

                        _tuningHistory.Add(new MemoryTuningAction(
                            ActionId: $"tune-{Guid.NewGuid():N}",
                            TargetCache: target,
                            PreviousBudgetMb: previousBudget,
                            NewBudgetMb: newBudget,
                            ReductionPct: throttlePct,
                            Reason: reason,
                            TimestampUtc: DateTimeOffset.UtcNow));
                    }
                }
            }
        }

        return new GcMemoryHealthReport(
            Gen0Collections: gen0,
            Gen1Collections: gen1,
            Gen2Collections: gen2,
            TotalAllocatedBytes: totalAllocatedBytes,
            HeapSizeBytes: heapSizeBytes,
            MemoryFragmentationPct: memoryFragmentationPct,
            Gen2PressurePct: gen2PressurePct,
            CacheBudgetThrottlePct: throttlePct,
            HealthStatus: status);
    }
}
