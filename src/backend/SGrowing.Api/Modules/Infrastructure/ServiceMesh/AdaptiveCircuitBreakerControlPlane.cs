using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.ServiceMesh;

/// <summary>
/// Adaptive service mesh control plane managing upstream instance registries,
/// outlier detection ejections, and circuit breaker state machines.
/// </summary>
public sealed class AdaptiveCircuitBreakerControlPlane
{
    private const int MaxConsecutiveFailuresBeforeEjection = 5;
    private static readonly TimeSpan BaseEjectionTime = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, UpstreamInstance> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Threading.Lock _lock = new();

    public void RegisterInstance(string instanceId, string serviceName, string host, int port)
    {
        _instances[instanceId] = new UpstreamInstance(
            InstanceId: instanceId,
            ServiceName: serviceName,
            HostAddress: host,
            Port: port
        );
    }

    public UpstreamInstance? SelectActiveInstance(string serviceName)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            var candidates = _instances.Values
                .Where(i => string.Equals(i.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 0) return null;

            // Check if any ejected instance has served its ejection duration
            for (var idx = 0; idx < candidates.Count; idx++)
            {
                var inst = candidates[idx];
                if (inst.State == CircuitState.Open && inst.EjectedUntilUtc.HasValue && inst.EjectedUntilUtc.Value <= now)
                {
                    // Transition to HalfOpen for probe
                    var halfOpen = inst with { State = CircuitState.HalfOpen };
                    _instances[inst.InstanceId] = halfOpen;
                    candidates[idx] = halfOpen;
                }
            }

            // Prefer Closed or HalfOpen instances with lowest EWMA latency
            var active = candidates
                .Where(i => i.State is CircuitState.Closed or CircuitState.HalfOpen)
                .OrderBy(i => i.LatencyEwmaMs)
                .FirstOrDefault();

            return active;
        }
    }

    public void RecordCallResult(string instanceId, bool success, double latencyMs)
    {
        lock (_lock)
        {
            if (!_instances.TryGetValue(instanceId, out var inst)) return;

            var now = DateTime.UtcNow;
            var totalCalls = inst.TotalCalls + 1;
            var failedCalls = success ? inst.FailedCalls : inst.FailedCalls + 1;
            var ewmaLatency = (0.2 * latencyMs) + (0.8 * inst.LatencyEwmaMs);

            if (success)
            {
                // Successful call in HalfOpen closes the circuit
                var newState = inst.State == CircuitState.HalfOpen ? CircuitState.Closed : inst.State;
                _instances[instanceId] = inst with
                {
                    TotalCalls = totalCalls,
                    ConsecutiveFailures = 0,
                    LatencyEwmaMs = Math.Round(ewmaLatency, 2),
                    State = newState,
                    EjectedUntilUtc = null
                };
            }
            else
            {
                var consecutive = inst.ConsecutiveFailures + 1;
                var ejections = inst.TotalEjections;
                var state = inst.State;
                DateTime? ejectedUntil = inst.EjectedUntilUtc;

                // Eject if consecutive errors breach threshold or if failed during HalfOpen trial
                if (consecutive >= MaxConsecutiveFailuresBeforeEjection || inst.State == CircuitState.HalfOpen)
                {
                    state = CircuitState.Open;
                    ejections++;
                    var durationMultiplier = Math.Min(16, Math.Pow(2, ejections - 1));
                    var ejectionDuration = TimeSpan.FromTicks((long)(BaseEjectionTime.Ticks * durationMultiplier));
                    ejectedUntil = now.Add(ejectionDuration);
                    consecutive = 0; // reset counter for next cycle
                }

                _instances[instanceId] = inst with
                {
                    TotalCalls = totalCalls,
                    FailedCalls = failedCalls,
                    ConsecutiveFailures = consecutive,
                    LatencyEwmaMs = Math.Round(ewmaLatency, 2),
                    State = state,
                    TotalEjections = ejections,
                    EjectedUntilUtc = ejectedUntil
                };
            }
        }
    }

    public IReadOnlyList<UpstreamInstance> GetAllInstances()
    {
        lock (_lock)
        {
            return [.. _instances.Values];
        }
    }
}
