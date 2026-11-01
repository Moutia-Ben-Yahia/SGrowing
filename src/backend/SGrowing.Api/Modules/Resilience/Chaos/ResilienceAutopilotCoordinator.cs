using System.Diagnostics;

namespace SGrowing.Api.Modules.Resilience.Chaos;

/// <summary>
/// Contract for the autonomous resilience autopilot coordinator.
/// </summary>
public interface IResilienceAutopilotCoordinator
{
    /// <summary>
    /// Launches an automated chaos experiment with canary blast-radius enforcement (<= 1.0%).
    /// </summary>
    ChaosExperimentReport StartExperiment(StartExperimentRequest request);

    /// <summary>
    /// Aborts an ongoing experiment with a specific SLA violation or manual reason.
    /// </summary>
    ChaosExperimentReport AbortExperiment(string experimentId, string reason);

    /// <summary>
    /// Completes an active experiment and computes the final resilience grade.
    /// </summary>
    ChaosExperimentReport CompleteExperiment(string experimentId);

    /// <summary>
    /// Retrieves the current report of an experiment.
    /// </summary>
    ChaosExperimentReport? GetExperimentReport(string experimentId);

    /// <summary>
    /// Obtains high-level autopilot health and error budget posture.
    /// </summary>
    AutopilotStatus GetAutopilotStatus();

    /// <summary>
    /// Records synthetic canary traffic during an experiment.
    /// </summary>
    void RecordExecution(string experimentId, bool fallbackSucceeded, double latencyMs, bool unhandledFailure = false);

    /// <summary>
    /// Executes automated synthetic simulation cycles against the experiment target.
    /// </summary>
    Task<ChaosExperimentReport> RunSimulationCyclesAsync(string experimentId, int cycles = 20, CancellationToken ct = default);
}

/// <summary>
/// Controls and monitors automated chaos experiments with strict canary blast-radius containment (canary traffic &lt;= 1.0%).
/// Autonomously aborts experiments on error rate or error budget breaches, assigning SLA resilience grades.
/// </summary>
public sealed class ResilienceAutopilotCoordinator(
    IChaosFaultInjectionMesh faultMesh,
    ICircuitBreakerResilienceAuditor resilienceAuditor) : IResilienceAutopilotCoordinator
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, ExperimentState> _experiments = [];

    private string? _activeExperimentId;
    private int _totalExperimentsRun;
    private double _systemErrorBudgetRemaining = 100.0;
    private ResilienceGrade? _lastAuditGrade;

    /// <inheritdoc />
    public ChaosExperimentReport StartExperiment(StartExperimentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ExperimentName))
        {
            throw new ArgumentException("Experiment name cannot be empty.", nameof(request.ExperimentName));
        }

        if (request.CanaryTrafficPercentage <= 0.0 || request.CanaryTrafficPercentage > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.CanaryTrafficPercentage),
                request.CanaryTrafficPercentage,
                "Canary traffic percentage cannot exceed 1.0% blast-radius limit.");
        }

        if (request.DurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.DurationSeconds),
                request.DurationSeconds,
                "Experiment duration must be greater than zero.");
        }

        lock (_lock)
        {
            if (_activeExperimentId is not null &&
                _experiments.TryGetValue(_activeExperimentId, out var existing) &&
                existing.Status == ExperimentStatus.Running)
            {
                throw new InvalidOperationException(
                    $"Experiment '{_activeExperimentId}' is currently running. Abort or complete it before starting a new one.");
            }

            var experimentId = $"exp-{Guid.NewGuid():N}"[..12];
            var ruleId = $"rule-{experimentId}";

            var rule = new ChaosFaultRule(
                RuleId: ruleId,
                Target: request.Target,
                FaultType: request.FaultType,
                Probability: Math.Clamp(request.FaultProbability, 0.0, 1.0),
                LatencyDelayMs: request.FaultType == ChaosFaultType.LatencyDelay ? 50 : 0,
                ErrorMessage: $"Autopilot injected fault for experiment {experimentId}",
                Enabled: true,
                CreatedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(request.DurationSeconds));

            faultMesh.AddRule(rule);

            var state = new ExperimentState
            {
                ExperimentId = experimentId,
                ExperimentName = request.ExperimentName,
                Target = request.Target,
                FaultType = request.FaultType,
                DurationSeconds = request.DurationSeconds,
                FaultProbability = request.FaultProbability,
                MaxAllowedErrorRate = request.MaxAllowedErrorRate,
                CanaryTrafficPercentage = request.CanaryTrafficPercentage,
                StartedAt = DateTimeOffset.UtcNow,
                Status = ExperimentStatus.Running
            };

            _experiments[experimentId] = state;
            _activeExperimentId = experimentId;
            _systemErrorBudgetRemaining = 100.0;

            return state.ToReport();
        }
    }

    /// <inheritdoc />
    public ChaosExperimentReport AbortExperiment(string experimentId, string reason)
    {
        ArgumentNullException.ThrowIfNull(experimentId);

        lock (_lock)
        {
            if (!_experiments.TryGetValue(experimentId, out var state))
            {
                throw new KeyNotFoundException($"Experiment '{experimentId}' not found.");
            }

            if (state.Status != ExperimentStatus.Running)
            {
                return state.ToReport();
            }

            AbortInternal(state, reason);
            return state.ToReport();
        }
    }

    /// <inheritdoc />
    public ChaosExperimentReport CompleteExperiment(string experimentId)
    {
        ArgumentNullException.ThrowIfNull(experimentId);

        lock (_lock)
        {
            if (!_experiments.TryGetValue(experimentId, out var state))
            {
                throw new KeyNotFoundException($"Experiment '{experimentId}' not found.");
            }

            if (state.Status != ExperimentStatus.Running)
            {
                return state.ToReport();
            }

            state.Status = ExperimentStatus.Completed;
            state.CompletedAt = DateTimeOffset.UtcNow;

            faultMesh.RemoveRule($"rule-{experimentId}");

            _totalExperimentsRun++;
            if (_activeExperimentId == experimentId)
            {
                _activeExperimentId = null;
            }

            var report = state.ToReport();
            _lastAuditGrade = report.ResilienceGrade;
            return report;
        }
    }

    /// <inheritdoc />
    public ChaosExperimentReport? GetExperimentReport(string experimentId)
    {
        if (string.IsNullOrWhiteSpace(experimentId))
        {
            return null;
        }

        lock (_lock)
        {
            return _experiments.TryGetValue(experimentId, out var state) ? state.ToReport() : null;
        }
    }

    /// <inheritdoc />
    public AutopilotStatus GetAutopilotStatus()
    {
        lock (_lock)
        {
            return new AutopilotStatus(
                ActiveExperimentId: _activeExperimentId,
                IsRunning: _activeExperimentId is not null,
                TotalExperimentsRun: _totalExperimentsRun,
                SystemErrorBudgetRemaining: _systemErrorBudgetRemaining,
                LastAuditGrade: _lastAuditGrade);
        }
    }

    /// <inheritdoc />
    public void RecordExecution(string experimentId, bool fallbackSucceeded, double latencyMs, bool unhandledFailure = false)
    {
        if (string.IsNullOrWhiteSpace(experimentId))
        {
            return;
        }

        lock (_lock)
        {
            if (!_experiments.TryGetValue(experimentId, out var state) || state.Status != ExperimentStatus.Running)
            {
                return;
            }

            state.TotalRequestsInjected++;
            state.Latencies.Add(latencyMs);

            if (unhandledFailure)
            {
                state.FailedRequestsCount++;
                _systemErrorBudgetRemaining = Math.Max(0.0, _systemErrorBudgetRemaining - 5.0);
            }
            else if (fallbackSucceeded)
            {
                state.FallbackSuccessCount++;
            }

            // Autonomous SLA abort checks
            double currentErrorRate = (double)state.FailedRequestsCount / state.TotalRequestsInjected;
            if (currentErrorRate > state.MaxAllowedErrorRate)
            {
                AbortInternal(
                    state,
                    $"Autonomous abort: error rate {currentErrorRate:P2} exceeded threshold {state.MaxAllowedErrorRate:P2}.");
            }
            else if (_systemErrorBudgetRemaining < 80.0)
            {
                AbortInternal(
                    state,
                    $"Autonomous abort: system error budget depleted to {_systemErrorBudgetRemaining:F1}% (below 80% SLA floor).");
            }
        }
    }

    /// <inheritdoc />
    public async Task<ChaosExperimentReport> RunSimulationCyclesAsync(
        string experimentId,
        int cycles = 20,
        CancellationToken ct = default)
    {
        if (cycles <= 0)
        {
            cycles = 20;
        }

        ExperimentState state;
        lock (_lock)
        {
            if (!_experiments.TryGetValue(experimentId, out var expState))
            {
                throw new KeyNotFoundException($"Experiment '{experimentId}' not found.");
            }
            state = expState;
        }

        for (int i = 0; i < cycles; i++)
        {
            ct.ThrowIfCancellationRequested();

            lock (_lock)
            {
                if (state.Status != ExperimentStatus.Running)
                {
                    break;
                }
            }

            var sw = Stopwatch.StartNew();
            try
            {
                await resilienceAuditor.ExecuteWithResilienceAsync(
                    state.Target,
                    primaryAction: () => Task.FromResult("primary_payload_result"),
                    fallbackAction: () => Task.FromResult("fallback_degraded_payload_result"),
                    ct: ct).ConfigureAwait(false);

                sw.Stop();
                RecordExecution(experimentId, fallbackSucceeded: true, latencyMs: sw.Elapsed.TotalMilliseconds, unhandledFailure: false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                sw.Stop();
                RecordExecution(experimentId, fallbackSucceeded: false, latencyMs: sw.Elapsed.TotalMilliseconds, unhandledFailure: true);
            }
        }

        lock (_lock)
        {
            return state.ToReport();
        }
    }

    private void AbortInternal(ExperimentState state, string reason)
    {
        state.Status = ExperimentStatus.Aborted;
        state.CompletedAt = DateTimeOffset.UtcNow;
        state.AbortReason = string.IsNullOrWhiteSpace(reason) ? "Manual abort requested." : reason;

        faultMesh.RemoveRule($"rule-{state.ExperimentId}");

        _totalExperimentsRun++;
        if (_activeExperimentId == state.ExperimentId)
        {
            _activeExperimentId = null;
        }

        _lastAuditGrade = ResilienceGrade.GradeF;
    }

    private sealed class ExperimentState
    {
        public required string ExperimentId { get; init; }
        public required string ExperimentName { get; init; }
        public required ChaosTarget Target { get; init; }
        public required ChaosFaultType FaultType { get; init; }
        public required int DurationSeconds { get; init; }
        public required double FaultProbability { get; init; }
        public required double MaxAllowedErrorRate { get; init; }
        public required double CanaryTrafficPercentage { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset? CompletedAt { get; set; }
        public ExperimentStatus Status { get; set; } = ExperimentStatus.Running;
        public string? AbortReason { get; set; }

        public int TotalRequestsInjected { get; set; }
        public int FallbackSuccessCount { get; set; }
        public int FailedRequestsCount { get; set; }
        public List<double> Latencies { get; } = [];

        public ChaosExperimentReport ToReport()
        {
            double fallbackSuccessRate = TotalRequestsInjected == 0
                ? 1.0
                : (double)FallbackSuccessCount / TotalRequestsInjected;

            double p99 = 0.0;
            if (Latencies.Count > 0)
            {
                var sorted = Latencies.OrderBy(x => x).ToList();
                int p99Index = (int)Math.Ceiling(sorted.Count * 0.99) - 1;
                p99Index = Math.Clamp(p99Index, 0, sorted.Count - 1);
                p99 = sorted[p99Index];
            }

            ResilienceGrade grade;
            if (Status == ExperimentStatus.Aborted)
            {
                grade = ResilienceGrade.GradeF;
            }
            else if (fallbackSuccessRate >= 1.0 && FailedRequestsCount == 0)
            {
                grade = ResilienceGrade.GradeAPlus;
            }
            else if (fallbackSuccessRate >= 0.98)
            {
                grade = ResilienceGrade.GradeA;
            }
            else if (fallbackSuccessRate >= 0.90)
            {
                grade = ResilienceGrade.GradeB;
            }
            else
            {
                grade = ResilienceGrade.GradeF;
            }

            return new ChaosExperimentReport(
                ExperimentId: ExperimentId,
                ExperimentName: ExperimentName,
                Target: Target,
                FaultType: FaultType,
                Status: Status,
                StartedAt: StartedAt,
                CompletedAt: CompletedAt,
                DurationSeconds: DurationSeconds,
                TotalRequestsInjected: TotalRequestsInjected,
                FallbackSuccessCount: FallbackSuccessCount,
                FailedRequestsCount: FailedRequestsCount,
                FallbackSuccessRate: Math.Round(fallbackSuccessRate, 4),
                ObservedP99LatencyMs: Math.Round(p99, 2),
                ResilienceGrade: grade,
                AbortReason: AbortReason);
        }
    }
}
