using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.ServiceMesh;

/// <summary>
/// Autonomous canary traffic splitter managing weighted multi-version routing (e.g. 90/10)
/// with real-time statistical evaluation and automated rollback guards.
/// </summary>
public sealed class CanaryTrafficSplitterService
{
    private readonly ConcurrentDictionary<string, CanaryPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, VersionMetricsTracker> _trackers = new(StringComparer.OrdinalIgnoreCase);

    public void SetPolicy(CanaryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policies[policy.ServiceName] = policy;
        _trackers[policy.ServiceName] = new VersionMetricsTracker();
    }

    public string SelectVersion(string serviceName)
    {
        if (!_policies.TryGetValue(serviceName, out var policy))
        {
            return "v1.0.0";
        }

        // Deterministic pseudo-random split
        var rand = Random.Shared.NextDouble();
        return rand < policy.CanaryTrafficRatio ? policy.CanaryVersion : policy.BaselineVersion;
    }

    public void RecordMetric(string serviceName, string version, bool success, double latencyMs)
    {
        var tracker = _trackers.GetOrAdd(serviceName, _ => new VersionMetricsTracker());
        tracker.Record(version, success, latencyMs);
    }

    public CanaryEvaluationReport EvaluateCanary(string serviceName)
    {
        if (!_policies.TryGetValue(serviceName, out var policy))
        {
            throw new KeyNotFoundException($"No canary policy configured for service '{serviceName}'.");
        }

        var tracker = _trackers.GetOrAdd(serviceName, _ => new VersionMetricsTracker());

        var (baseCalls, baseErrors, baseLatencies) = tracker.GetMetrics(policy.BaselineVersion);
        var (canaryCalls, canaryErrors, canaryLatencies) = tracker.GetMetrics(policy.CanaryVersion);

        var baseErrorRate = baseCalls > 0 ? ((double)baseErrors / baseCalls) * 100.0 : 0.0;
        var canaryErrorRate = canaryCalls > 0 ? ((double)canaryErrors / canaryCalls) * 100.0 : 0.0;

        var baseP95 = CalculatePercentile(baseLatencies, 0.95);
        var canaryP95 = CalculatePercentile(canaryLatencies, 0.95);

        var errorDelta = canaryErrorRate - baseErrorRate;
        var latencyDelta = baseP95 > 0 ? ((canaryP95 - baseP95) / baseP95) * 100.0 : 0.0;

        var decision = CanaryDecision.KeepProbing;
        var reason = "Canary performance within acceptable statistical error and latency bounds.";

        // Minimum 10 calls required before enacting automated rollback or promotion
        if (canaryCalls >= 10)
        {
            if (errorDelta > policy.MaxAllowedErrorDeltaPercent)
            {
                decision = CanaryDecision.RollbackCanary;
                reason = $"CRITICAL REGRESSION: Canary error rate ({canaryErrorRate:F2}%) exceeds baseline ({baseErrorRate:F2}%) by {errorDelta:F2}% > threshold ({policy.MaxAllowedErrorDeltaPercent}%). Rollback triggered.";
            }
            else if (latencyDelta > policy.MaxAllowedLatencyDeltaPercent)
            {
                decision = CanaryDecision.RollbackCanary;
                reason = $"PERFORMANCE DEGRADATION: Canary p95 latency ({canaryP95:F1}ms) is {latencyDelta:F1}% slower than baseline ({baseP95:F1}ms) > threshold ({policy.MaxAllowedLatencyDeltaPercent}%). Rollback triggered.";
            }
            else if (canaryCalls >= 100 && errorDelta <= 0.0 && latencyDelta <= 5.0)
            {
                decision = CanaryDecision.PromoteCanary;
                reason = $"PROMOTION READY: Canary processed {canaryCalls} calls with superior or equivalent SLA conformance.";
            }
        }

        return new CanaryEvaluationReport(
            ServiceName: serviceName,
            BaselineVersion: policy.BaselineVersion,
            CanaryVersion: policy.CanaryVersion,
            BaselineTotalCalls: baseCalls,
            BaselineErrorRatePercent: Math.Round(baseErrorRate, 2),
            BaselineP95LatencyMs: Math.Round(baseP95, 2),
            CanaryTotalCalls: canaryCalls,
            CanaryErrorRatePercent: Math.Round(canaryErrorRate, 2),
            CanaryP95LatencyMs: Math.Round(canaryP95, 2),
            ErrorDeltaPercent: Math.Round(errorDelta, 2),
            LatencyDeltaPercent: Math.Round(latencyDelta, 2),
            Decision: decision,
            Reason: reason,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }

    private static double CalculatePercentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0) return 0.0;
        var sorted = values.OrderBy(v => v).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private sealed class VersionMetricsTracker
    {
        private readonly System.Threading.Lock _lock = new();
        private readonly Dictionary<string, (long Total, long Errors, List<double> Latencies)> _data = new(StringComparer.OrdinalIgnoreCase);

        public void Record(string version, bool success, double latencyMs)
        {
            lock (_lock)
            {
                if (!_data.TryGetValue(version, out var tuple))
                {
                    tuple = (0, 0, []);
                }

                tuple.Total++;
                if (!success) tuple.Errors++;
                tuple.Latencies.Add(latencyMs);

                // Cap list to rolling 1000 samples
                if (tuple.Latencies.Count > 1000)
                {
                    tuple.Latencies.RemoveAt(0);
                }

                _data[version] = tuple;
            }
        }

        public (long Total, long Errors, IReadOnlyList<double> Latencies) GetMetrics(string version)
        {
            lock (_lock)
            {
                if (_data.TryGetValue(version, out var tuple))
                {
                    return (tuple.Total, tuple.Errors, tuple.Latencies.ToList());
                }
                return (0, 0, []);
            }
        }
    }
}
