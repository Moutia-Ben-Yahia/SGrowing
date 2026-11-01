using System.Net;

namespace SGrowing.Api.Modules.Resilience.Chaos;

/// <summary>
/// Contract for the thread-safe in-memory chaos fault injection mesh.
/// </summary>
public interface IChaosFaultInjectionMesh
{
    /// <summary>
    /// Registers or updates a fault injection rule in the mesh.
    /// </summary>
    void AddRule(ChaosFaultRule rule);

    /// <summary>
    /// Removes a fault rule by its identifier.
    /// </summary>
    bool RemoveRule(string ruleId);

    /// <summary>
    /// Clears all registered fault rules.
    /// </summary>
    void ClearAllRules();

    /// <summary>
    /// Retrieves all currently active and unexpired rules.
    /// </summary>
    IReadOnlyList<ChaosFaultRule> GetActiveRules();

    /// <summary>
    /// Obtains atomic metrics of all injected faults across system targets.
    /// </summary>
    InjectedMetrics GetMetrics();

    /// <summary>
    /// Determines whether a synthetic fault should be injected for the target dependency.
    /// </summary>
    bool ShouldInjectFault(ChaosTarget target, out ChaosFaultRule? matchingRule);

    /// <summary>
    /// Injects an active fault asynchronously (latency delay or synthetic exception).
    /// </summary>
    Task InjectFaultAsync(ChaosTarget target, CancellationToken ct = default);
}

/// <summary>
/// Thread-safe in-memory fault injection mesh utilizing native System.Threading.Lock.
/// Evaluates active rules with automatic TTL expiration and records real-time injection metrics.
/// </summary>
public sealed class ChaosFaultInjectionMesh : IChaosFaultInjectionMesh
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, ChaosFaultRule> _rules = [];

    private long _totalFaultsInjected;
    private long _latencyInjections;
    private long _exceptionInjections;

    /// <inheritdoc />
    public void AddRule(ChaosFaultRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var ruleId = string.IsNullOrWhiteSpace(rule.RuleId)
            ? $"rule-{Guid.NewGuid():N}"[..12]
            : rule.RuleId;

        var sanitizedRule = rule with
        {
            RuleId = ruleId,
            Probability = Math.Clamp(rule.Probability, 0.0, 1.0),
            LatencyDelayMs = Math.Max(0, rule.LatencyDelayMs),
            CreatedAt = rule.CreatedAt == default ? DateTimeOffset.UtcNow : rule.CreatedAt
        };

        lock (_lock)
        {
            _rules[ruleId] = sanitizedRule;
        }
    }

    /// <inheritdoc />
    public bool RemoveRule(string ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
        {
            return false;
        }

        lock (_lock)
        {
            return _rules.Remove(ruleId);
        }
    }

    /// <inheritdoc />
    public void ClearAllRules()
    {
        lock (_lock)
        {
            _rules.Clear();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ChaosFaultRule> GetActiveRules()
    {
        var now = DateTimeOffset.UtcNow;
        List<ChaosFaultRule> active = [];

        lock (_lock)
        {
            List<string> expiredKeys = [];

            foreach (var (key, rule) in _rules)
            {
                if (rule.ExpiresAt.HasValue && rule.ExpiresAt.Value <= now)
                {
                    expiredKeys.Add(key);
                }
                else if (rule.Enabled)
                {
                    active.Add(rule);
                }
            }

            foreach (var expiredKey in expiredKeys)
            {
                _rules.Remove(expiredKey);
            }
        }

        return active;
    }

    /// <inheritdoc />
    public InjectedMetrics GetMetrics()
    {
        var activeCount = GetActiveRules().Count;
        return new InjectedMetrics(
            Interlocked.Read(ref _totalFaultsInjected),
            Interlocked.Read(ref _latencyInjections),
            Interlocked.Read(ref _exceptionInjections),
            activeCount);
    }

    /// <inheritdoc />
    public bool ShouldInjectFault(ChaosTarget target, out ChaosFaultRule? matchingRule)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            matchingRule = _rules.Values
                .Where(r => r.Target == target && r.Enabled && (!r.ExpiresAt.HasValue || r.ExpiresAt.Value > now))
                .OrderByDescending(r => r.Probability)
                .FirstOrDefault();

            if (matchingRule is null)
            {
                return false;
            }

            if (matchingRule.Probability <= 0.0)
            {
                matchingRule = null;
                return false;
            }

            if (matchingRule.Probability >= 1.0)
            {
                return true;
            }

            var randomRoll = Random.Shared.NextDouble();
            if (randomRoll < matchingRule.Probability)
            {
                return true;
            }

            matchingRule = null;
            return false;
        }
    }

    /// <inheritdoc />
    public async Task InjectFaultAsync(ChaosTarget target, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!ShouldInjectFault(target, out var rule) || rule is null)
        {
            return;
        }

        Interlocked.Increment(ref _totalFaultsInjected);

        switch (rule.FaultType)
        {
            case ChaosFaultType.LatencyDelay:
                Interlocked.Increment(ref _latencyInjections);
                if (rule.LatencyDelayMs > 0)
                {
                    await Task.Delay(rule.LatencyDelayMs, ct).ConfigureAwait(false);
                }
                break;

            case ChaosFaultType.DatabaseConnectionDrop:
                Interlocked.Increment(ref _exceptionInjections);
                throw new InvalidOperationException(rule.ErrorMessage ?? "Simulated chaos DB connection failure");

            case ChaosFaultType.TransientIoException:
                Interlocked.Increment(ref _exceptionInjections);
                throw new IOException(rule.ErrorMessage ?? "Simulated transient I/O network failure");

            case ChaosFaultType.CarrierHttp503Error:
                Interlocked.Increment(ref _exceptionInjections);
                var statusCode503 = rule.ErrorStatusCode.HasValue
                    ? (HttpStatusCode)rule.ErrorStatusCode.Value
                    : HttpStatusCode.ServiceUnavailable;
                throw new HttpRequestException(
                    rule.ErrorMessage ?? "Simulated carrier 503 Service Unavailable",
                    null,
                    statusCode503);

            case ChaosFaultType.Http429TooManyRequests:
                Interlocked.Increment(ref _exceptionInjections);
                var statusCode429 = rule.ErrorStatusCode.HasValue
                    ? (HttpStatusCode)rule.ErrorStatusCode.Value
                    : HttpStatusCode.TooManyRequests;
                throw new HttpRequestException(
                    rule.ErrorMessage ?? "Simulated 429 Too Many Requests",
                    null,
                    statusCode429);

            case ChaosFaultType.RedisCacheMissStorm:
                Interlocked.Increment(ref _exceptionInjections);
                throw new KeyNotFoundException(rule.ErrorMessage ?? "Simulated Redis cache miss storm - key evicted or unavailable");

            case ChaosFaultType.PayloadCorruption:
                Interlocked.Increment(ref _exceptionInjections);
                throw new FormatException(rule.ErrorMessage ?? "Simulated payload corruption - invalid framing / checksum mismatch");

            default:
                Interlocked.Increment(ref _exceptionInjections);
                throw new InvalidOperationException($"Simulated unhandled chaos fault type: {rule.FaultType}");
        }
    }
}
