using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SGrowing.Api.Modules.AI.Gateway;

namespace SGrowing.Api.Modules.AI.Firewall;

public enum CircuitState
{
    Closed = 0,    // Healthy, accepting calls
    Open = 1,      // Tripped, failing fast / skipping
    HalfOpen = 2   // Trial probe after cooldown
}

public record ModelTierConfig(
    int Priority,
    string TierName,
    string Provider,
    string Model,
    string Description,
    int MaxTokens = 4096,
    double Temperature = 0.7
);

public record ModelProviderResponse(
    bool Success,
    int StatusCode,
    string Content,
    string? ErrorMessage = null,
    int LatencyMs = 0
);

public record CascadingRouteRequest(
    string Prompt,
    string? SystemPrompt = null,
    double Temperature = 0.7,
    int MaxTokens = 2048,
    string? SessionId = null
);

public record CascadingRouteExecutionResult(
    bool Success,
    string Content,
    string PrimaryModel,
    string ActualModel,
    string ActualProvider,
    int ActualTierPriority,
    bool IsFallback,
    string? FallbackReason,
    int TotalLatencyMs,
    IReadOnlyList<string> FallbackAuditTrail
);

public record CircuitBreakerStatus(
    string Provider,
    string Model,
    CircuitState State,
    int ConsecutiveFailures,
    string? LastFailureReason,
    DateTime? LastTrippedAtUtc,
    double CooldownRemainingSeconds,
    long TotalRequests,
    long TotalSuccesses,
    long TotalFailures
);

public interface ICascadingModelRouterService
{
    Task<CascadingRouteExecutionResult> ExecuteAsync(
        CascadingRouteRequest request,
        Func<ModelTierConfig, CancellationToken, Task<ModelProviderResponse>>? customInvoker = null,
        CancellationToken ct = default);

    IReadOnlyList<CircuitBreakerStatus> GetCircuitBreakerStatuses();
    void TripCircuitBreaker(string provider, string reason);
    void ResetCircuitBreaker(string provider);
    void ResetAll();
    void SetCooldownPeriod(TimeSpan cooldown);
    void SetLatencyTimeout(TimeSpan timeout);
}

public class CascadingModelRouterService : ICascadingModelRouterService
{
    private readonly ILogger<CascadingModelRouterService>? _logger;

    // Routing Priority Tiers
    public static readonly IReadOnlyList<ModelTierConfig> DefaultTiers =
    [
        new(Priority: 0, TierName: "Primary", Provider: "OpenAI", Model: "gpt-4o", Description: "OpenAI GPT-4o / Realtime API"),
        new(Priority: 1, TierName: "Fallback1", Provider: "Anthropic", Model: "claude-3-5-sonnet", Description: "Anthropic Claude 3.5 Sonnet"),
        new(Priority: 2, TierName: "Fallback2", Provider: "Local", Model: "llama-3.3-70b", Description: "Local Llama 3.3 / Mistral Large"),
        new(Priority: 3, TierName: "Fallback3", Provider: "Google", Model: "gemini-2.5-flash", Description: "Google Gemini 2.5 Flash")
    ];

    private TimeSpan _cooldownPeriod = TimeSpan.FromSeconds(60);
    private TimeSpan _latencyTimeout = TimeSpan.FromSeconds(5.0);

    // Internal Circuit Breaker State per Provider
    private class ProviderCircuitState
    {
        public object Lock = new();
        public CircuitState State = CircuitState.Closed;
        public int ConsecutiveFailures;
        public string? LastFailureReason;
        public DateTime? LastTrippedAtUtc;
        public long TotalRequests;
        public long TotalSuccesses;
        public long TotalFailures;
    }

    private readonly ConcurrentDictionary<string, ProviderCircuitState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ILlmProviderClient> _providerClients = new(StringComparer.OrdinalIgnoreCase);

    public CascadingModelRouterService(
        IEnumerable<ILlmProviderClient>? providerClients = null,
        ILogger<CascadingModelRouterService>? logger = null)
    {
        _logger = logger;
        if (providerClients != null)
        {
            foreach (var client in providerClients)
            {
                _providerClients[client.ProviderName] = client;
            }
        }

        foreach (var tier in DefaultTiers)
        {
            _states.TryAdd(tier.Provider, new ProviderCircuitState());
        }
    }

    public CascadingModelRouterService(ILogger<CascadingModelRouterService>? logger)
        : this(null, logger)
    {
    }

    public async Task<CascadingRouteExecutionResult> ExecuteAsync(
        CascadingRouteRequest request,
        Func<ModelTierConfig, CancellationToken, Task<ModelProviderResponse>>? customInvoker = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var swTotal = Stopwatch.StartNew();
        var auditTrail = new List<string>();
        string? firstTierFailureReason = null;

        for (int i = 0; i < DefaultTiers.Count; i++)
        {
            var tier = DefaultTiers[i];
            var state = _states.GetOrAdd(tier.Provider, _ => new ProviderCircuitState());

            // Check if circuit breaker allows execution
            if (!CheckCircuitBreakerAvailability(tier.Provider, state, out var skipReason))
            {
                auditTrail.Add($"[Tier {tier.Priority} - {tier.Provider} ({tier.Model})]: SKIPPED -> {skipReason}");
                if (i == 0) firstTierFailureReason = skipReason;
                continue;
            }

            // Attempt execution on this tier
            Interlocked.Increment(ref state.TotalRequests);
            var tierSw = Stopwatch.StartNew();

            try
            {
                ModelProviderResponse response;
                if (customInvoker != null)
                {
                    response = await customInvoker(tier, ct);
                }
                else if (_providerClients.TryGetValue(tier.Provider, out var client))
                {
                    var llmRequest = new LlmCompletionRequest(
                        Prompt: request.Prompt,
                        SystemPrompt: request.SystemPrompt,
                        Model: tier.Model,
                        Temperature: request.Temperature,
                        MaxTokens: request.MaxTokens
                    );

                    var llmResult = await client.CompleteAsync(llmRequest, ct);

                    response = new ModelProviderResponse(
                        Success: llmResult.Success,
                        StatusCode: llmResult.StatusCode,
                        Content: llmResult.Content,
                        ErrorMessage: llmResult.ErrorMessage,
                        LatencyMs: llmResult.LatencyMs
                    );
                }
                else
                {
                    // Built-in resilient simulator
                    response = await SimulateProviderCallAsync(tier, request, ct);
                }

                tierSw.Stop();
                var latencyMs = response.LatencyMs > 0 ? response.LatencyMs : (int)tierSw.ElapsedMilliseconds;

                // Rule 1: HTTP 429 Rate Limit triggers circuit breaker
                if (response.StatusCode == 429)
                {
                    TripCircuitBreakerInternal(tier.Provider, state, "HTTP 429 (Rate Limit exceeded)");
                    auditTrail.Add($"[Tier {tier.Priority} - {tier.Provider} ({tier.Model})]: FAILED HTTP 429 in {latencyMs}ms -> Circuit breaker tripped.");
                    if (i == 0) firstTierFailureReason = "HTTP 429 Rate Limit";
                    continue;
                }

                // Rule 2: Latency exceeding threshold (5.0s / 5000ms) triggers circuit breaker
                if (tierSw.Elapsed > _latencyTimeout || latencyMs > _latencyTimeout.TotalMilliseconds)
                {
                    TripCircuitBreakerInternal(tier.Provider, state, $"Latency {latencyMs}ms exceeded timeout threshold {_latencyTimeout.TotalMilliseconds}ms");
                    auditTrail.Add($"[Tier {tier.Priority} - {tier.Provider} ({tier.Model})]: FAILED Latency timeout ({latencyMs}ms > {_latencyTimeout.TotalMilliseconds}ms) -> Circuit breaker tripped.");
                    if (i == 0) firstTierFailureReason = $"Latency timeout ({latencyMs}ms)";
                    continue;
                }

                // Rule 3: Error response
                if (!response.Success)
                {
                    TripCircuitBreakerInternal(tier.Provider, state, response.ErrorMessage ?? $"HTTP {response.StatusCode} failure");
                    auditTrail.Add($"[Tier {tier.Priority} - {tier.Provider} ({tier.Model})]: FAILED with error '{response.ErrorMessage}' in {latencyMs}ms.");
                    if (i == 0) firstTierFailureReason = response.ErrorMessage;
                    continue;
                }

                // SUCCESS on this tier!
                RecordSuccess(tier.Provider, state);
                swTotal.Stop();

                bool isFallback = i > 0;
                auditTrail.Add($"[Tier {tier.Priority} - {tier.Provider} ({tier.Model})]: SUCCESS in {latencyMs}ms (Total: {swTotal.ElapsedMilliseconds}ms)");

                return new CascadingRouteExecutionResult(
                    Success: true,
                    Content: response.Content,
                    PrimaryModel: DefaultTiers[0].Model,
                    ActualModel: tier.Model,
                    ActualProvider: tier.Provider,
                    ActualTierPriority: tier.Priority,
                    IsFallback: isFallback,
                    FallbackReason: isFallback ? firstTierFailureReason : null,
                    TotalLatencyMs: (int)swTotal.ElapsedMilliseconds,
                    FallbackAuditTrail: auditTrail
                );
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                tierSw.Stop();
                var latencyMs = (int)tierSw.ElapsedMilliseconds;
                TripCircuitBreakerInternal(tier.Provider, state, $"Exception: {ex.Message}");
                auditTrail.Add($"[Tier {tier.Priority} - {tier.Provider} ({tier.Model})]: EXCEPTION '{ex.Message}' in {latencyMs}ms -> Circuit breaker tripped.");
                if (i == 0) firstTierFailureReason = ex.Message;
            }
        }

        swTotal.Stop();
        return new CascadingRouteExecutionResult(
            Success: false,
            Content: string.Empty,
            PrimaryModel: DefaultTiers[0].Model,
            ActualModel: string.Empty,
            ActualProvider: string.Empty,
            ActualTierPriority: -1,
            IsFallback: true,
            FallbackReason: firstTierFailureReason ?? "All cascading model tiers exhausted or tripped",
            TotalLatencyMs: (int)swTotal.ElapsedMilliseconds,
            FallbackAuditTrail: auditTrail
        );
    }

    private bool CheckCircuitBreakerAvailability(string provider, ProviderCircuitState state, out string reason)
    {
        lock (state.Lock)
        {
            if (state.State == CircuitState.Closed)
            {
                reason = string.Empty;
                return true;
            }

            if (state.State == CircuitState.Open)
            {
                var elapsed = DateTime.UtcNow - (state.LastTrippedAtUtc ?? DateTime.UtcNow);
                if (elapsed >= _cooldownPeriod)
                {
                    // Transition to HalfOpen for probe
                    state.State = CircuitState.HalfOpen;
                    _logger?.LogInformation(
                        "Circuit breaker for '{Provider}' cooled down ({Elapsed:F1}s >= {Cooldown:F1}s). Transitioning to HalfOpen probe.",
                        provider, elapsed.TotalSeconds, _cooldownPeriod.TotalSeconds);
                    reason = string.Empty;
                    return true;
                }

                var remaining = Math.Max(0, (_cooldownPeriod - elapsed).TotalSeconds);
                reason = $"CircuitBreaker OPEN. Reason: '{state.LastFailureReason}'. Cooldown remaining: {remaining:F1}s";
                return false;
            }

            if (state.State == CircuitState.HalfOpen)
            {
                // Already probing
                reason = string.Empty;
                return true;
            }

            reason = "Unknown circuit state";
            return false;
        }
    }

    private void RecordSuccess(string provider, ProviderCircuitState state)
    {
        lock (state.Lock)
        {
            Interlocked.Increment(ref state.TotalSuccesses);
            state.ConsecutiveFailures = 0;
            if (state.State == CircuitState.HalfOpen)
            {
                state.State = CircuitState.Closed;
                state.LastFailureReason = null;
                _logger?.LogInformation("Circuit breaker for '{Provider}' successfully recovered. State is now CLOSED.", provider);
            }
        }
    }

    private void TripCircuitBreakerInternal(string provider, ProviderCircuitState state, string reason)
    {
        lock (state.Lock)
        {
            Interlocked.Increment(ref state.TotalFailures);
            state.ConsecutiveFailures++;
            state.State = CircuitState.Open;
            state.LastFailureReason = reason;
            state.LastTrippedAtUtc = DateTime.UtcNow;

            _logger?.LogWarning(
                "Circuit breaker TRIPPED for '{Provider}'. State: OPEN. Reason: {Reason}. Failures: {Failures}",
                provider, reason, state.ConsecutiveFailures);
        }
    }

    public void TripCircuitBreaker(string provider, string reason)
    {
        var state = _states.GetOrAdd(provider, _ => new ProviderCircuitState());
        TripCircuitBreakerInternal(provider, state, reason);
    }

    public void ResetCircuitBreaker(string provider)
    {
        if (_states.TryGetValue(provider, out var state))
        {
            lock (state.Lock)
            {
                state.State = CircuitState.Closed;
                state.ConsecutiveFailures = 0;
                state.LastFailureReason = null;
                state.LastTrippedAtUtc = null;
            }
        }
    }

    public void ResetAll()
    {
        foreach (var (provider, _) in _states)
        {
            ResetCircuitBreaker(provider);
        }
    }

    public void SetCooldownPeriod(TimeSpan cooldown) => _cooldownPeriod = cooldown;
    public void SetLatencyTimeout(TimeSpan timeout) => _latencyTimeout = timeout;

    public IReadOnlyList<CircuitBreakerStatus> GetCircuitBreakerStatuses()
    {
        var list = new List<CircuitBreakerStatus>();
        foreach (var tier in DefaultTiers)
        {
            var state = _states.GetOrAdd(tier.Provider, _ => new ProviderCircuitState());
            lock (state.Lock)
            {
                double remaining = 0;
                if (state.State == CircuitState.Open && state.LastTrippedAtUtc.HasValue)
                {
                    var elapsed = DateTime.UtcNow - state.LastTrippedAtUtc.Value;
                    remaining = Math.Max(0, (_cooldownPeriod - elapsed).TotalSeconds);
                }

                list.Add(new CircuitBreakerStatus(
                    Provider: tier.Provider,
                    Model: tier.Model,
                    State: state.State,
                    ConsecutiveFailures: state.ConsecutiveFailures,
                    LastFailureReason: state.LastFailureReason,
                    LastTrippedAtUtc: state.LastTrippedAtUtc,
                    CooldownRemainingSeconds: remaining,
                    TotalRequests: Volatile.Read(ref state.TotalRequests),
                    TotalSuccesses: Volatile.Read(ref state.TotalSuccesses),
                    TotalFailures: Volatile.Read(ref state.TotalFailures)
                ));
            }
        }
        return list;
    }

    private static Task<ModelProviderResponse> SimulateProviderCallAsync(
        ModelTierConfig tier,
        CascadingRouteRequest request,
        CancellationToken ct)
    {
        return Task.FromResult(new ModelProviderResponse(
            Success: true,
            StatusCode: 200,
            Content: $"[{tier.Provider} - {tier.Model}] Processed prompt: \"{request.Prompt}\"",
            LatencyMs: 150
        ));
    }
}
