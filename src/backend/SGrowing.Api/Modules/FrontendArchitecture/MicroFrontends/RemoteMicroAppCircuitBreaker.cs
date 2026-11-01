using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SGrowing.Api.Modules.FrontendArchitecture.MicroFrontends;

/// <summary>
/// Service contract for the remote micro-app circuit breaker.
/// </summary>
public interface IRemoteMicroAppCircuitBreaker
{
    CircuitState GetState(string moduleId);
    bool AllowExecution(string moduleId);
    CircuitBreakerMetrics RecordFailure(string moduleId, string? reason = null);
    CircuitBreakerMetrics RecordSuccess(string moduleId);
    CircuitBreakerMetrics ForceTrip(string moduleId, string? reason = null);
    CircuitBreakerMetrics ResetCircuit(string moduleId);
    CircuitBreakerMetrics GetMetrics(string moduleId);
    IReadOnlyList<CircuitBreakerMetrics> GetAllMetrics();
    FallbackViewRepresentation GetFallbackView(string moduleId, string? customReason = null);
}

/// <summary>
/// Thread-safe circuit breaker monitoring federated micro-app remotes, handling tripping,
/// half-open probes, and fallback view emission to prevent shell crashes.
/// </summary>
public sealed class RemoteMicroAppCircuitBreaker : IRemoteMicroAppCircuitBreaker
{
    private readonly Lock _lock = new();
    private readonly IMicroFrontendHostEngine? _hostEngine;
    private readonly int _defaultFailureThreshold;
    private readonly int _defaultResetTimeoutSeconds;

    private readonly Dictionary<string, CircuitStateEntry> _circuits = new(StringComparer.OrdinalIgnoreCase);

    public const int StandardFailureThreshold = 3;
    public const int StandardResetTimeoutSeconds = 30;

    public RemoteMicroAppCircuitBreaker(
        IMicroFrontendHostEngine? hostEngine = null,
        int defaultFailureThreshold = StandardFailureThreshold,
        int defaultResetTimeoutSeconds = StandardResetTimeoutSeconds)
    {
        _hostEngine = hostEngine;
        _defaultFailureThreshold = Math.Max(1, defaultFailureThreshold);
        _defaultResetTimeoutSeconds = Math.Max(1, defaultResetTimeoutSeconds);
    }

    public CircuitState GetState(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_lock)
        {
            var entry = GetOrCreateEntry(moduleId);
            EvaluateStateTransition(entry);
            return entry.State;
        }
    }

    public bool AllowExecution(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_lock)
        {
            var entry = GetOrCreateEntry(moduleId);
            EvaluateStateTransition(entry);

            // Allow execution if Closed or trial probe in HalfOpen
            return entry.State is CircuitState.Closed or CircuitState.HalfOpen;
        }
    }

    public CircuitBreakerMetrics RecordFailure(string moduleId, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_lock)
        {
            var entry = GetOrCreateEntry(moduleId);
            EvaluateStateTransition(entry);

            var now = DateTimeOffset.UtcNow;
            entry.LastFailureUtc = now;

            if (entry.State == CircuitState.HalfOpen)
            {
                // Renewed failure during trial probe -> Trip immediately back to Open
                entry.State = CircuitState.Open;
                entry.ConsecutiveFailures = entry.FailureThreshold;
                entry.LastStateChangeUtc = now;
                entry.TotalTripCount++;
                _hostEngine?.UpdateRemoteHealth(moduleId, RemoteHealthStatus.CircuitOpen, 0);
            }
            else if (entry.State == CircuitState.Closed)
            {
                entry.ConsecutiveFailures++;
                if (entry.ConsecutiveFailures >= entry.FailureThreshold)
                {
                    entry.State = CircuitState.Open;
                    entry.LastStateChangeUtc = now;
                    entry.TotalTripCount++;
                    _hostEngine?.UpdateRemoteHealth(moduleId, RemoteHealthStatus.CircuitOpen, 0);
                }
                else
                {
                    _hostEngine?.UpdateRemoteHealth(moduleId, RemoteHealthStatus.Degraded, 0);
                }
            }

            return entry.ToMetrics();
        }
    }

    public CircuitBreakerMetrics RecordSuccess(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_lock)
        {
            var entry = GetOrCreateEntry(moduleId);
            EvaluateStateTransition(entry);

            var now = DateTimeOffset.UtcNow;
            entry.TotalSuccessProbes++;

            if (entry.State == CircuitState.HalfOpen)
            {
                // Successful trial probe -> close circuit
                entry.State = CircuitState.Closed;
                entry.ConsecutiveFailures = 0;
                entry.LastStateChangeUtc = now;
                _hostEngine?.UpdateRemoteHealth(moduleId, RemoteHealthStatus.Healthy, 10.0);
            }
            else if (entry.State == CircuitState.Closed)
            {
                entry.ConsecutiveFailures = 0;
                _hostEngine?.UpdateRemoteHealth(moduleId, RemoteHealthStatus.Healthy, 10.0);
            }

            return entry.ToMetrics();
        }
    }

    public CircuitBreakerMetrics ForceTrip(string moduleId, string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_lock)
        {
            var entry = GetOrCreateEntry(moduleId);
            var now = DateTimeOffset.UtcNow;

            entry.State = CircuitState.Open;
            entry.ConsecutiveFailures = entry.FailureThreshold;
            entry.LastFailureUtc = now;
            entry.LastStateChangeUtc = now;
            entry.TotalTripCount++;

            _hostEngine?.UpdateRemoteHealth(moduleId, RemoteHealthStatus.CircuitOpen, 0);

            return entry.ToMetrics();
        }
    }

    public CircuitBreakerMetrics ResetCircuit(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_lock)
        {
            var entry = GetOrCreateEntry(moduleId);
            var now = DateTimeOffset.UtcNow;

            entry.State = CircuitState.Closed;
            entry.ConsecutiveFailures = 0;
            entry.LastStateChangeUtc = now;

            _hostEngine?.UpdateRemoteHealth(moduleId, RemoteHealthStatus.Healthy, 10.0);

            return entry.ToMetrics();
        }
    }

    public CircuitBreakerMetrics GetMetrics(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_lock)
        {
            var entry = GetOrCreateEntry(moduleId);
            EvaluateStateTransition(entry);
            return entry.ToMetrics();
        }
    }

    public IReadOnlyList<CircuitBreakerMetrics> GetAllMetrics()
    {
        lock (_lock)
        {
            foreach (var entry in _circuits.Values)
            {
                EvaluateStateTransition(entry);
            }

            return _circuits.Values.Select(e => e.ToMetrics()).ToList();
        }
    }

    public FallbackViewRepresentation GetFallbackView(string moduleId, string? customReason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_lock)
        {
            var entry = GetOrCreateEntry(moduleId);
            EvaluateStateTransition(entry);

            var now = DateTimeOffset.UtcNow;
            var isCircuitOpen = entry.State is CircuitState.Open or CircuitState.HalfOpen;
            var reason = customReason ?? (isCircuitOpen
                ? $"Remote micro-app '{moduleId}' is experiencing transient failure. Circuit state: {entry.State}."
                : $"Remote micro-app '{moduleId}' is operating normally.");

            var retryAfter = 0;
            if (entry.State == CircuitState.Open)
            {
                var elapsed = (now - entry.LastStateChangeUtc).TotalSeconds;
                retryAfter = (int)Math.Max(0, Math.Ceiling(entry.ResetTimeoutSeconds - elapsed));
            }

            var remote = _hostEngine?.GetRemote(moduleId);
            var fallbackUrl = remote?.FallbackComponentUrl ?? $"/static/mfe/fallbacks/{SanitizeId(moduleId)}-fallback.js";

            var html = $"""
                <div class="sg-mfe-fallback" role="alert" aria-live="polite" data-module="{moduleId}" data-circuit-state="{entry.State}">
                    <div class="sg-mfe-fallback-header">
                        <span class="sg-mfe-fallback-icon" aria-hidden="true">&#9888;</span>
                        <h4 class="sg-mfe-fallback-title">Resilient Degraded Mode: {moduleId}</h4>
                    </div>
                    <p class="sg-mfe-fallback-reason">{reason}</p>
                    <div class="sg-mfe-fallback-actions">
                        <button type="button" class="sg-btn-mfe-retry" onclick="window.__sgMfeHost?.retryRemote('{moduleId}')" data-retry-after="{retryAfter}">
                            Retry Component Loading ({retryAfter}s)
                        </button>
                    </div>
                </div>
                """;

            return new FallbackViewRepresentation(
                ModuleId: moduleId,
                FallbackHtml: html,
                FallbackComponentUrl: fallbackUrl,
                IsFallbackActive: isCircuitOpen,
                Reason: reason,
                EmittedAtUtc: now,
                RetryAfterSeconds: retryAfter);
        }
    }

    private void EvaluateStateTransition(CircuitStateEntry entry)
    {
        if (entry.State == CircuitState.Open)
        {
            var elapsed = DateTimeOffset.UtcNow - entry.LastStateChangeUtc;
            if (elapsed >= TimeSpan.FromSeconds(entry.ResetTimeoutSeconds))
            {
                // Timeout elapsed -> transition to HalfOpen for probe
                entry.State = CircuitState.HalfOpen;
                entry.LastStateChangeUtc = DateTimeOffset.UtcNow;
                _hostEngine?.UpdateRemoteHealth(entry.ModuleId, RemoteHealthStatus.Degraded, 0);
            }
        }
    }

    private CircuitStateEntry GetOrCreateEntry(string moduleId)
    {
        if (!_circuits.TryGetValue(moduleId, out var entry))
        {
            entry = new CircuitStateEntry(moduleId, _defaultFailureThreshold, _defaultResetTimeoutSeconds);
            _circuits[moduleId] = entry;
        }

        return entry;
    }

    private static string SanitizeId(string moduleId) => moduleId.Replace("@", "").Replace("/", "-");

    private sealed class CircuitStateEntry
    {
        public string ModuleId { get; }
        public CircuitState State { get; set; } = CircuitState.Closed;
        public int ConsecutiveFailures { get; set; }
        public int FailureThreshold { get; }
        public int ResetTimeoutSeconds { get; }
        public DateTimeOffset? LastFailureUtc { get; set; }
        public DateTimeOffset LastStateChangeUtc { get; set; } = DateTimeOffset.UtcNow;
        public int TotalTripCount { get; set; }
        public int TotalSuccessProbes { get; set; }

        public CircuitStateEntry(string moduleId, int failureThreshold, int resetTimeoutSeconds)
        {
            ModuleId = moduleId;
            FailureThreshold = failureThreshold;
            ResetTimeoutSeconds = resetTimeoutSeconds;
        }

        public CircuitBreakerMetrics ToMetrics() => new(
            ModuleId: ModuleId,
            State: State,
            ConsecutiveFailures: ConsecutiveFailures,
            FailureThreshold: FailureThreshold,
            ResetTimeoutSeconds: ResetTimeoutSeconds,
            LastFailureUtc: LastFailureUtc,
            LastStateChangeUtc: LastStateChangeUtc,
            TotalTripCount: TotalTripCount,
            TotalSuccessProbes: TotalSuccessProbes);
    }
}
