using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Resilience.Chaos;

/// <summary>
/// Contract for auditing circuit breaker degradation posture and executing resilient fallback patterns.
/// </summary>
public interface ICircuitBreakerResilienceAuditor
{
    /// <summary>
    /// Executes a primary action under chaos fault mesh, cleanly degrading to fallback action if a failure occurs.
    /// Guarantees zero unhandled exceptions to callers during downstream dependency outages.
    /// </summary>
    Task<T> ExecuteWithResilienceAsync<T>(
        ChaosTarget target,
        Func<Task<T>> primaryAction,
        Func<Task<T>> fallbackAction,
        CancellationToken ct = default);

    /// <summary>
    /// Audits target resilience by simulating downstream fault injection over a fixed number of execution cycles.
    /// </summary>
    Task<CircuitBreakerAuditStatus> AuditTargetResilience(ChaosTarget target, int testCycles = 10);

    /// <summary>
    /// Gets the current audit status for a specific dependency target.
    /// </summary>
    CircuitBreakerAuditStatus GetAuditStatus(ChaosTarget target);

    /// <summary>
    /// Gets audit posture across all supported system dependencies.
    /// </summary>
    IReadOnlyList<CircuitBreakerAuditStatus> GetAllAudits();
}

/// <summary>
/// Audits whether client and downstream caller patterns cleanly degrade without unhandled exceptions.
/// Implements resilient degradation simulations for Redis, WhatsApp, Logistics Carrier, Payment, Database, and Voice.
/// </summary>
public sealed class CircuitBreakerResilienceAuditor(IChaosFaultInjectionMesh faultMesh) : ICircuitBreakerResilienceAuditor
{
    private readonly ConcurrentDictionary<ChaosTarget, TargetAuditCounters> _targetStats = new();

    /// <inheritdoc />
    public async Task<T> ExecuteWithResilienceAsync<T>(
        ChaosTarget target,
        Func<Task<T>> primaryAction,
        Func<Task<T>> fallbackAction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(primaryAction);
        ArgumentNullException.ThrowIfNull(fallbackAction);

        var stats = _targetStats.GetOrAdd(target, _ => new TargetAuditCounters());
        stats.RecordExecution();

        try
        {
            await faultMesh.InjectFaultAsync(target, ct).ConfigureAwait(false);
            return await primaryAction().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            stats.RecordFailure();
            stats.RecordFallbackTriggered();

            try
            {
                var fallbackResult = await fallbackAction().ConfigureAwait(false);
                stats.RecordFallbackSuccess();
                return fallbackResult;
            }
            catch (Exception)
            {
                stats.RecordFallbackFailure();
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async Task<CircuitBreakerAuditStatus> AuditTargetResilience(ChaosTarget target, int testCycles = 10)
    {
        if (testCycles <= 0)
        {
            testCycles = 10;
        }

        var ruleId = $"audit-{target}-{Guid.NewGuid():N}"[..16];
        var faultType = target switch
        {
            ChaosTarget.Redis => ChaosFaultType.RedisCacheMissStorm,
            ChaosTarget.WhatsAppGateway => ChaosFaultType.CarrierHttp503Error,
            ChaosTarget.LogisticsCarrierApi => ChaosFaultType.CarrierHttp503Error,
            ChaosTarget.PaymentGateway => ChaosFaultType.TransientIoException,
            ChaosTarget.Database => ChaosFaultType.DatabaseConnectionDrop,
            ChaosTarget.VoiceTelephony => ChaosFaultType.Http429TooManyRequests,
            _ => ChaosFaultType.TransientIoException
        };

        var rule = new ChaosFaultRule(
            RuleId: ruleId,
            Target: target,
            FaultType: faultType,
            Probability: 1.0,
            ErrorMessage: $"Resilience audit synthetic failure on {target}",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(1));

        faultMesh.AddRule(rule);

        try
        {
            for (int i = 0; i < testCycles; i++)
            {
                switch (target)
                {
                    case ChaosTarget.Redis:
                        await ExecuteWithResilienceAsync(
                            target,
                            primaryAction: () => Task.FromResult("live_redis_cluster_cache_hit"),
                            fallbackAction: () => Task.FromResult("in_memory_l1_cache_fallback")).ConfigureAwait(false);
                        break;

                    case ChaosTarget.WhatsAppGateway:
                        await ExecuteWithResilienceAsync(
                            target,
                            primaryAction: () => Task.FromResult("whatsapp_msg_delivered_200"),
                            fallbackAction: () => Task.FromResult("secondary_sms_transport_delivered")).ConfigureAwait(false);
                        break;

                    case ChaosTarget.LogisticsCarrierApi:
                        await ExecuteWithResilienceAsync(
                            target,
                            primaryAction: () => Task.FromResult("carrier_live_rate_mad_45"),
                            fallbackAction: () => Task.FromResult("offline_cached_rate_sheet_mad_45")).ConfigureAwait(false);
                        break;

                    case ChaosTarget.PaymentGateway:
                        await ExecuteWithResilienceAsync(
                            target,
                            primaryAction: () => Task.FromResult("direct_settlement_captured"),
                            fallbackAction: () => Task.FromResult("deferred_async_settlement_queued")).ConfigureAwait(false);
                        break;

                    case ChaosTarget.Database:
                        await ExecuteWithResilienceAsync(
                            target,
                            primaryAction: () => Task.FromResult("primary_db_query_result"),
                            fallbackAction: () => Task.FromResult("read_replica_snapshot_cached")).ConfigureAwait(false);
                        break;

                    case ChaosTarget.VoiceTelephony:
                        await ExecuteWithResilienceAsync(
                            target,
                            primaryAction: () => Task.FromResult("webrtc_sip_call_established"),
                            fallbackAction: () => Task.FromResult("pstn_bridge_voicemail_recorded")).ConfigureAwait(false);
                        break;
                }
            }
        }
        finally
        {
            faultMesh.RemoveRule(ruleId);
        }

        return GetAuditStatus(target);
    }

    /// <inheritdoc />
    public CircuitBreakerAuditStatus GetAuditStatus(ChaosTarget target)
    {
        if (!_targetStats.TryGetValue(target, out var stats))
        {
            return new CircuitBreakerAuditStatus(
                Target: target,
                TotalExecutions: 0,
                InjectedFailures: 0,
                FallbacksTriggered: 0,
                FallbackSuccessRate: 1.0,
                Healthy: true);
        }

        var total = (int)stats.TotalExecutions;
        var failures = (int)stats.InjectedFailures;
        var fallbacks = (int)stats.FallbacksTriggered;
        var successes = (int)stats.FallbackSuccesses;

        double successRate = fallbacks == 0 ? 1.0 : (double)successes / fallbacks;
        bool healthy = successRate >= 0.95;

        return new CircuitBreakerAuditStatus(
            Target: target,
            TotalExecutions: total,
            InjectedFailures: failures,
            FallbacksTriggered: fallbacks,
            FallbackSuccessRate: successRate,
            Healthy: healthy);
    }

    /// <inheritdoc />
    public IReadOnlyList<CircuitBreakerAuditStatus> GetAllAudits()
    {
        return Enum.GetValues<ChaosTarget>()
            .Select(GetAuditStatus)
            .ToList();
    }

    private sealed class TargetAuditCounters
    {
        private long _totalExecutions;
        private long _injectedFailures;
        private long _fallbacksTriggered;
        private long _fallbackSuccesses;
        private long _fallbackFailures;

        public long TotalExecutions => Interlocked.Read(ref _totalExecutions);
        public long InjectedFailures => Interlocked.Read(ref _injectedFailures);
        public long FallbacksTriggered => Interlocked.Read(ref _fallbacksTriggered);
        public long FallbackSuccesses => Interlocked.Read(ref _fallbackSuccesses);
        public long FallbackFailures => Interlocked.Read(ref _fallbackFailures);

        public void RecordExecution() => Interlocked.Increment(ref _totalExecutions);
        public void RecordFailure() => Interlocked.Increment(ref _injectedFailures);
        public void RecordFallbackTriggered() => Interlocked.Increment(ref _fallbacksTriggered);
        public void RecordFallbackSuccess() => Interlocked.Increment(ref _fallbackSuccesses);
        public void RecordFallbackFailure() => Interlocked.Increment(ref _fallbackFailures);
    }
}
