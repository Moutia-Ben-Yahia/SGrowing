using System.Collections.Concurrent;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Resilience.Chaos;
using Xunit;

namespace SGrowing.Tests.Resilience;

/// <summary>
/// Unit tests for Phase 35: Autonomous Chaos Engineering, Fault Injection Mesh &amp; Resilience Autopilot.
/// Covers fault rule lifecycle, TTL expiration, probability thresholds, synthetic exception simulation,
/// circuit breaker fallback degradation, zero-unhandled-exception guarantees, SLA resilience grading,
/// autonomous abort loops, canary blast-radius containment, multi-threaded concurrency, and DI descriptors.
/// </summary>
public class ChaosResilienceAutopilotTests
{
    private readonly ChaosFaultInjectionMesh _mesh = new();
    private readonly CircuitBreakerResilienceAuditor _auditor;
    private readonly ResilienceAutopilotCoordinator _coordinator;

    public ChaosResilienceAutopilotTests()
    {
        _auditor = new CircuitBreakerResilienceAuditor(_mesh);
        _coordinator = new ResilienceAutopilotCoordinator(_mesh, _auditor);
    }

    #region 1. Fault Rule Registration, TTL Expiration, and Removal

    [Fact]
    public void AddRule_ValidRule_RegistersSuccessfullyAndAppearsInActiveRules()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-db-drop",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            Probability: 1.0,
            Enabled: true);

        _mesh.AddRule(rule);

        var active = _mesh.GetActiveRules();
        active.Should().ContainSingle(r => r.RuleId == "rule-db-drop");
    }

    [Fact]
    public void AddRule_ClampsProbabilityAndNormalizesFields()
    {
        var rule = new ChaosFaultRule(
            RuleId: "",
            Target: ChaosTarget.Redis,
            FaultType: ChaosFaultType.LatencyDelay,
            Probability: 2.5,
            LatencyDelayMs: -50);

        _mesh.AddRule(rule);

        var active = _mesh.GetActiveRules();
        active.Should().HaveCount(1);
        var registered = active[0];
        registered.RuleId.Should().NotBeNullOrWhiteSpace();
        registered.Probability.Should().Be(1.0);
        registered.LatencyDelayMs.Should().Be(0);
        registered.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public void RemoveRule_ExistingRuleId_ReturnsTrueAndRemovesFromMesh()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-to-remove",
            Target: ChaosTarget.WhatsAppGateway,
            FaultType: ChaosFaultType.CarrierHttp503Error,
            Probability: 1.0);

        _mesh.AddRule(rule);
        var removed = _mesh.RemoveRule("rule-to-remove");
        var active = _mesh.GetActiveRules();

        removed.Should().BeTrue();
        active.Should().BeEmpty();
    }

    [Fact]
    public void RemoveRule_NonExistentOrEmptyRuleId_ReturnsFalse()
    {
        _mesh.RemoveRule("non-existent-rule").Should().BeFalse();
        _mesh.RemoveRule("").Should().BeFalse();
    }

    [Fact]
    public void ClearAllRules_RemovesAllRulesFromMesh()
    {
        _mesh.AddRule(new ChaosFaultRule("rule-1", ChaosTarget.Database, ChaosFaultType.DatabaseConnectionDrop, 1.0));
        _mesh.AddRule(new ChaosFaultRule("rule-2", ChaosTarget.Redis, ChaosFaultType.RedisCacheMissStorm, 1.0));

        _mesh.GetActiveRules().Should().HaveCount(2);

        _mesh.ClearAllRules();

        _mesh.GetActiveRules().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveRules_ExpiredRule_IsPurgedAndExcludedFromResults()
    {
        var expiredRule = new ChaosFaultRule(
            RuleId: "rule-expired",
            Target: ChaosTarget.PaymentGateway,
            FaultType: ChaosFaultType.TransientIoException,
            Probability: 1.0,
            ExpiresAt: DateTimeOffset.UtcNow.AddMilliseconds(-100));

        _mesh.AddRule(expiredRule);

        var active = _mesh.GetActiveRules();
        active.Should().BeEmpty();
    }

    [Fact]
    public void GetActiveRules_FutureExpiration_RemainsActive()
    {
        var futureRule = new ChaosFaultRule(
            RuleId: "rule-future",
            Target: ChaosTarget.PaymentGateway,
            FaultType: ChaosFaultType.TransientIoException,
            Probability: 1.0,
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(10));

        _mesh.AddRule(futureRule);

        var active = _mesh.GetActiveRules();
        active.Should().ContainSingle(r => r.RuleId == "rule-future");
    }

    #endregion

    #region 2. Probability Threshold Evaluation

    [Fact]
    public void ShouldInjectFault_ProbabilityOne_AlwaysTriggers()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-always",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            Probability: 1.0);

        _mesh.AddRule(rule);

        for (int i = 0; i < 20; i++)
        {
            var triggered = _mesh.ShouldInjectFault(ChaosTarget.Database, out var matched);
            triggered.Should().BeTrue();
            matched.Should().NotBeNull();
            matched!.RuleId.Should().Be("rule-always");
        }
    }

    [Fact]
    public void ShouldInjectFault_ProbabilityZero_NeverTriggers()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-never",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            Probability: 0.0);

        _mesh.AddRule(rule);

        for (int i = 0; i < 20; i++)
        {
            var triggered = _mesh.ShouldInjectFault(ChaosTarget.Database, out var matched);
            triggered.Should().BeFalse();
            matched.Should().BeNull();
        }
    }

    [Fact]
    public void ShouldInjectFault_DisabledRule_DoesNotTrigger()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-disabled",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            Probability: 1.0,
            Enabled: false);

        _mesh.AddRule(rule);

        var triggered = _mesh.ShouldInjectFault(ChaosTarget.Database, out var matched);
        triggered.Should().BeFalse();
        matched.Should().BeNull();
    }

    [Fact]
    public void ShouldInjectFault_TargetMismatch_DoesNotTrigger()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-redis-only",
            Target: ChaosTarget.Redis,
            FaultType: ChaosFaultType.RedisCacheMissStorm,
            Probability: 1.0);

        _mesh.AddRule(rule);

        var triggered = _mesh.ShouldInjectFault(ChaosTarget.WhatsAppGateway, out var matched);
        triggered.Should().BeFalse();
        matched.Should().BeNull();
    }

    #endregion

    #region 3. Artificial Latency Injection and CancellationToken

    [Fact]
    public async Task InjectFaultAsync_LatencyDelay_AwaitsDelayAndIncrementsLatencyCounter()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-latency",
            Target: ChaosTarget.PaymentGateway,
            FaultType: ChaosFaultType.LatencyDelay,
            Probability: 1.0,
            LatencyDelayMs: 20);

        _mesh.AddRule(rule);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _mesh.InjectFaultAsync(ChaosTarget.PaymentGateway);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(15);

        var metrics = _mesh.GetMetrics();
        metrics.TotalFaultsInjected.Should().Be(1);
        metrics.LatencyInjections.Should().Be(1);
        metrics.ExceptionInjections.Should().Be(0);
    }

    [Fact]
    public async Task InjectFaultAsync_LatencyDelay_WithZeroMs_DoesNotDelay()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-latency-zero",
            Target: ChaosTarget.PaymentGateway,
            FaultType: ChaosFaultType.LatencyDelay,
            Probability: 1.0,
            LatencyDelayMs: 0);

        _mesh.AddRule(rule);

        await _mesh.InjectFaultAsync(ChaosTarget.PaymentGateway);

        var metrics = _mesh.GetMetrics();
        metrics.TotalFaultsInjected.Should().Be(1);
        metrics.LatencyInjections.Should().Be(1);
    }

    [Fact]
    public async Task InjectFaultAsync_LatencyDelay_WithCancellationToken_ThrowsOperationCanceledException()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-latency-cancel",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.LatencyDelay,
            Probability: 1.0,
            LatencyDelayMs: 5000);

        _mesh.AddRule(rule);

        using var cts = new CancellationTokenSource(30);

        var act = () => _mesh.InjectFaultAsync(ChaosTarget.Database, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region 4. Simulated Exceptions & Fault Injections

    [Fact]
    public async Task InjectFaultAsync_DatabaseConnectionDrop_ThrowsInvalidOperationExceptionWithSimulatedMessage()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-db",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            Probability: 1.0,
            ErrorMessage: "Simulated chaos DB connection failure");

        _mesh.AddRule(rule);

        var act = () => _mesh.InjectFaultAsync(ChaosTarget.Database);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("*Simulated chaos DB connection failure*");

        var metrics = _mesh.GetMetrics();
        metrics.TotalFaultsInjected.Should().Be(1);
        metrics.ExceptionInjections.Should().Be(1);
    }

    [Fact]
    public async Task InjectFaultAsync_TransientIoException_ThrowsIOException()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-io",
            Target: ChaosTarget.PaymentGateway,
            FaultType: ChaosFaultType.TransientIoException,
            Probability: 1.0,
            ErrorMessage: "Simulated transient socket reset");

        _mesh.AddRule(rule);

        var act = () => _mesh.InjectFaultAsync(ChaosTarget.PaymentGateway);
        var ex = await act.Should().ThrowAsync<IOException>();
        ex.WithMessage("*Simulated transient socket reset*");
    }

    [Fact]
    public async Task InjectFaultAsync_CarrierHttp503Error_ThrowsHttpRequestExceptionWith503Status()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-503",
            Target: ChaosTarget.LogisticsCarrierApi,
            FaultType: ChaosFaultType.CarrierHttp503Error,
            Probability: 1.0,
            ErrorStatusCode: (int)HttpStatusCode.ServiceUnavailable,
            ErrorMessage: "Carrier service unavailable");

        _mesh.AddRule(rule);

        var act = () => _mesh.InjectFaultAsync(ChaosTarget.LogisticsCarrierApi);
        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task InjectFaultAsync_Http429TooManyRequests_ThrowsHttpRequestExceptionWith429Status()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-429",
            Target: ChaosTarget.VoiceTelephony,
            FaultType: ChaosFaultType.Http429TooManyRequests,
            Probability: 1.0,
            ErrorStatusCode: (int)HttpStatusCode.TooManyRequests,
            ErrorMessage: "SIP trunk rate limit exceeded");

        _mesh.AddRule(rule);

        var act = () => _mesh.InjectFaultAsync(ChaosTarget.VoiceTelephony);
        var ex = await act.Should().ThrowAsync<HttpRequestException>();
        ex.Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task InjectFaultAsync_RedisCacheMissStorm_ThrowsKeyNotFoundException()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-redis-storm",
            Target: ChaosTarget.Redis,
            FaultType: ChaosFaultType.RedisCacheMissStorm,
            Probability: 1.0);

        _mesh.AddRule(rule);

        var act = () => _mesh.InjectFaultAsync(ChaosTarget.Redis);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task InjectFaultAsync_PayloadCorruption_ThrowsFormatException()
    {
        var rule = new ChaosFaultRule(
            RuleId: "rule-corrupt",
            Target: ChaosTarget.WhatsAppGateway,
            FaultType: ChaosFaultType.PayloadCorruption,
            Probability: 1.0);

        _mesh.AddRule(rule);

        var act = () => _mesh.InjectFaultAsync(ChaosTarget.WhatsAppGateway);
        await act.Should().ThrowAsync<FormatException>();
    }

    #endregion

    #region 5. Circuit Breaker Fallback Execution & Degradation

    [Fact]
    public async Task ExecuteWithResilienceAsync_NoFault_ReturnsPrimaryResultAndDoesNotTriggerFallback()
    {
        bool fallbackInvoked = false;

        var result = await _auditor.ExecuteWithResilienceAsync(
            ChaosTarget.Database,
            primaryAction: () => Task.FromResult("primary_db_live"),
            fallbackAction: () =>
            {
                fallbackInvoked = true;
                return Task.FromResult("fallback_db_replica");
            });

        result.Should().Be("primary_db_live");
        fallbackInvoked.Should().BeFalse();

        var status = _auditor.GetAuditStatus(ChaosTarget.Database);
        status.TotalExecutions.Should().Be(1);
        status.InjectedFailures.Should().Be(0);
        status.FallbacksTriggered.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_OnFault_ExecutesFallbackTransparentlyAndIncrementsMetrics()
    {
        _mesh.AddRule(new ChaosFaultRule(
            RuleId: "rule-fault",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            Probability: 1.0));

        bool fallbackInvoked = false;

        var result = await _auditor.ExecuteWithResilienceAsync(
            ChaosTarget.Database,
            primaryAction: () => Task.FromResult("primary_db_live"),
            fallbackAction: () =>
            {
                fallbackInvoked = true;
                return Task.FromResult("fallback_db_replica");
            });

        result.Should().Be("fallback_db_replica");
        fallbackInvoked.Should().BeTrue();

        var status = _auditor.GetAuditStatus(ChaosTarget.Database);
        status.TotalExecutions.Should().Be(1);
        status.InjectedFailures.Should().Be(1);
        status.FallbacksTriggered.Should().Be(1);
        status.FallbackSuccessRate.Should().Be(1.0);
        status.Healthy.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_RedisFallback_GracefullyDegradesToLocalInMemoryCache()
    {
        _mesh.AddRule(new ChaosFaultRule(
            RuleId: "rule-redis",
            Target: ChaosTarget.Redis,
            FaultType: ChaosFaultType.RedisCacheMissStorm,
            Probability: 1.0));

        var result = await _auditor.ExecuteWithResilienceAsync(
            ChaosTarget.Redis,
            primaryAction: () => Task.FromResult("redis_cluster_session_token"),
            fallbackAction: () => Task.FromResult("in_memory_l1_cache_fallback_token"));

        result.Should().Be("in_memory_l1_cache_fallback_token");
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_WhatsAppFallback_GracefullyDegradesToSecondarySmsTransport()
    {
        _mesh.AddRule(new ChaosFaultRule(
            RuleId: "rule-wa",
            Target: ChaosTarget.WhatsAppGateway,
            FaultType: ChaosFaultType.CarrierHttp503Error,
            Probability: 1.0));

        var result = await _auditor.ExecuteWithResilienceAsync(
            ChaosTarget.WhatsAppGateway,
            primaryAction: () => Task.FromResult("wa_msg_outbound_status_sent"),
            fallbackAction: () => Task.FromResult("sms_fallback_outbound_status_sent"));

        result.Should().Be("sms_fallback_outbound_status_sent");
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_CarrierFallback_GracefullyDegradesToCachedRateSheet()
    {
        _mesh.AddRule(new ChaosFaultRule(
            RuleId: "rule-carrier",
            Target: ChaosTarget.LogisticsCarrierApi,
            FaultType: ChaosFaultType.CarrierHttp503Error,
            Probability: 1.0));

        var result = await _auditor.ExecuteWithResilienceAsync(
            ChaosTarget.LogisticsCarrierApi,
            primaryAction: () => Task.FromResult("live_api_rate:45.00"),
            fallbackAction: () => Task.FromResult("cached_offline_rate_sheet:45.00"));

        result.Should().Be("cached_offline_rate_sheet:45.00");
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_PaymentGatewayFallback_GracefullyDegradesToDeferredSettlementQueue()
    {
        _mesh.AddRule(new ChaosFaultRule(
            RuleId: "rule-payment",
            Target: ChaosTarget.PaymentGateway,
            FaultType: ChaosFaultType.TransientIoException,
            Probability: 1.0));

        var result = await _auditor.ExecuteWithResilienceAsync(
            ChaosTarget.PaymentGateway,
            primaryAction: () => Task.FromResult("realtime_card_charge_tx"),
            fallbackAction: () => Task.FromResult("deferred_settlement_queue_ack"));

        result.Should().Be("deferred_settlement_queue_ack");
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_DatabaseFallback_GracefullyDegradesToSnapshot()
    {
        _mesh.AddRule(new ChaosFaultRule(
            RuleId: "rule-db-drop",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            Probability: 1.0));

        var result = await _auditor.ExecuteWithResilienceAsync(
            ChaosTarget.Database,
            primaryAction: () => Task.FromResult("live_db_tenant_config"),
            fallbackAction: () => Task.FromResult("cached_replica_snapshot_config"));

        result.Should().Be("cached_replica_snapshot_config");
    }

    [Fact]
    public async Task ExecuteWithResilienceAsync_VoiceTelephonyFallback_GracefullyDegradesToPstnBridge()
    {
        _mesh.AddRule(new ChaosFaultRule(
            RuleId: "rule-sip",
            Target: ChaosTarget.VoiceTelephony,
            FaultType: ChaosFaultType.Http429TooManyRequests,
            Probability: 1.0));

        var result = await _auditor.ExecuteWithResilienceAsync(
            ChaosTarget.VoiceTelephony,
            primaryAction: () => Task.FromResult("sip_webrtc_trunk_call"),
            fallbackAction: () => Task.FromResult("pstn_telephony_ivr_bridge"));

        result.Should().Be("pstn_telephony_ivr_bridge");
    }

    #endregion

    #region 6. Zero Unhandled Exceptions & Audit Status

    [Fact]
    public async Task ExecuteWithResilienceAsync_ContinuousChaos_ZeroUnhandledExceptions()
    {
        _mesh.AddRule(new ChaosFaultRule(
            RuleId: "rule-continuous-chaos",
            Target: ChaosTarget.LogisticsCarrierApi,
            FaultType: ChaosFaultType.CarrierHttp503Error,
            Probability: 1.0));

        List<string> outputs = [];

        // Continuous 100 iterations under 100% active chaos
        for (int i = 0; i < 100; i++)
        {
            var output = await _auditor.ExecuteWithResilienceAsync(
                ChaosTarget.LogisticsCarrierApi,
                primaryAction: () => Task.FromResult("live_quote"),
                fallbackAction: () => Task.FromResult("cached_quote"));

            outputs.Add(output);
        }

        outputs.Should().HaveCount(100);
        outputs.Should().AllBe("cached_quote");

        var audit = _auditor.GetAuditStatus(ChaosTarget.LogisticsCarrierApi);
        audit.TotalExecutions.Should().Be(100);
        audit.InjectedFailures.Should().Be(100);
        audit.FallbacksTriggered.Should().Be(100);
        audit.FallbackSuccessRate.Should().Be(1.0);
        audit.Healthy.Should().BeTrue();
    }

    [Fact]
    public async Task AuditTargetResilience_ExecutesCycles_ReturnsHealthyStatusWith100PercentFallback()
    {
        var status = await _auditor.AuditTargetResilience(ChaosTarget.Redis, testCycles: 15);

        status.Target.Should().Be(ChaosTarget.Redis);
        status.TotalExecutions.Should().Be(15);
        status.InjectedFailures.Should().Be(15);
        status.FallbacksTriggered.Should().Be(15);
        status.FallbackSuccessRate.Should().Be(1.0);
        status.Healthy.Should().BeTrue();
    }

    [Fact]
    public void GetAuditStatus_UnexercisedTarget_ReturnsCleanDefaultStatus()
    {
        var status = _auditor.GetAuditStatus(ChaosTarget.VoiceTelephony);

        status.Target.Should().Be(ChaosTarget.VoiceTelephony);
        status.TotalExecutions.Should().Be(0);
        status.InjectedFailures.Should().Be(0);
        status.FallbacksTriggered.Should().Be(0);
        status.FallbackSuccessRate.Should().Be(1.0);
        status.Healthy.Should().BeTrue();
    }

    [Fact]
    public void GetAllAudits_ReturnsStatusForAllSupportedChaosTargets()
    {
        var audits = _auditor.GetAllAudits();

        var expectedTargets = Enum.GetValues<ChaosTarget>();
        audits.Should().HaveCount(expectedTargets.Length);
        audits.Select(a => a.Target).Should().BeEquivalentTo(expectedTargets);
    }

    #endregion

    #region 7. Autonomous Autopilot Start, Completion & Resilience Grading

    [Fact]
    public void StartExperiment_ValidRequest_RegistersTemporaryChaosRuleAndReturnsRunningStatus()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Database Resilience Drill",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            DurationSeconds: 120,
            FaultProbability: 0.8,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 0.5);

        var report = _coordinator.StartExperiment(request);

        report.Status.Should().Be(ExperimentStatus.Running);
        report.ExperimentName.Should().Be("Database Resilience Drill");
        report.Target.Should().Be(ChaosTarget.Database);
        report.DurationSeconds.Should().Be(120);

        var activeRules = _mesh.GetActiveRules();
        activeRules.Should().ContainSingle(r => r.RuleId == $"rule-{report.ExperimentId}");
    }

    [Fact]
    public void RecordExecution_100PercentFallbackSuccess_AssignsGradeAPlus()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Carrier Gateway Drill",
            Target: ChaosTarget.LogisticsCarrierApi,
            FaultType: ChaosFaultType.CarrierHttp503Error,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 1.0);

        var report = _coordinator.StartExperiment(request);

        for (int i = 0; i < 20; i++)
        {
            _coordinator.RecordExecution(report.ExperimentId, fallbackSucceeded: true, latencyMs: 15.0, unhandledFailure: false);
        }

        var completedReport = _coordinator.CompleteExperiment(report.ExperimentId);

        completedReport.Status.Should().Be(ExperimentStatus.Completed);
        completedReport.FallbackSuccessRate.Should().Be(1.0);
        completedReport.FailedRequestsCount.Should().Be(0);
        completedReport.ResilienceGrade.Should().Be(ResilienceGrade.GradeAPlus);
    }

    [Fact]
    public void RecordExecution_HighFallbackSuccessRateAbove98_AssignsGradeA()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "High Fallback Drill",
            Target: ChaosTarget.Redis,
            FaultType: ChaosFaultType.RedisCacheMissStorm,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 1.0);

        var report = _coordinator.StartExperiment(request);

        // 99 successes, 1 failure -> 99% success rate
        for (int i = 0; i < 99; i++)
        {
            _coordinator.RecordExecution(report.ExperimentId, fallbackSucceeded: true, latencyMs: 5.0, unhandledFailure: false);
        }
        _coordinator.RecordExecution(report.ExperimentId, fallbackSucceeded: false, latencyMs: 5.0, unhandledFailure: true);

        var completedReport = _coordinator.CompleteExperiment(report.ExperimentId);

        completedReport.FallbackSuccessRate.Should().BeApproximately(0.99, 0.01);
        completedReport.ResilienceGrade.Should().Be(ResilienceGrade.GradeA);
    }

    [Fact]
    public void RecordExecution_ModerateFallbackSuccessRateAbove90_AssignsGradeB()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Moderate Fallback Drill",
            Target: ChaosTarget.PaymentGateway,
            FaultType: ChaosFaultType.TransientIoException,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.15,
            CanaryTrafficPercentage: 1.0);

        var report = _coordinator.StartExperiment(request);

        // 23 successes, 2 unhandled failures -> 23/25 = 92% fallback success rate (Grade B)
        // Error rate is 2/25 = 8% (< 15% allowed) and budget drop is 2 * 5% = 10% -> 90% remaining (> 80% SLA floor)
        for (int i = 0; i < 23; i++)
        {
            _coordinator.RecordExecution(report.ExperimentId, fallbackSucceeded: true, latencyMs: 20.0, unhandledFailure: false);
        }
        for (int i = 0; i < 2; i++)
        {
            _coordinator.RecordExecution(report.ExperimentId, fallbackSucceeded: false, latencyMs: 20.0, unhandledFailure: true);
        }

        var completedReport = _coordinator.CompleteExperiment(report.ExperimentId);

        completedReport.FallbackSuccessRate.Should().BeApproximately(0.92, 0.01);
        completedReport.ResilienceGrade.Should().Be(ResilienceGrade.GradeB);
    }

    [Fact]
    public void CompleteExperiment_CleansUpChaosRuleAndFinalizesReport()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Cleanup Verification Drill",
            Target: ChaosTarget.WhatsAppGateway,
            FaultType: ChaosFaultType.CarrierHttp503Error,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 0.8);

        var started = _coordinator.StartExperiment(request);
        _mesh.GetActiveRules().Should().ContainSingle(r => r.RuleId == $"rule-{started.ExperimentId}");

        var completed = _coordinator.CompleteExperiment(started.ExperimentId);

        completed.Status.Should().Be(ExperimentStatus.Completed);
        completed.CompletedAt.Should().NotBeNull();
        _mesh.GetActiveRules().Should().BeEmpty();
    }

    #endregion

    #region 8. Automated Abort, Error Rate SLA & Error Budget

    [Fact]
    public void RecordExecution_ErrorRateExceedsMaxAllowed_TriggersAutonomousAbortWithGradeF()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Strict Error Rate SLA Drill",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05, // 5% max error rate
            CanaryTrafficPercentage: 0.5);

        var started = _coordinator.StartExperiment(request);

        // 10 requests, 1 unhandled failure -> 10% error rate > 5% allowed
        for (int i = 0; i < 9; i++)
        {
            _coordinator.RecordExecution(started.ExperimentId, fallbackSucceeded: true, latencyMs: 10.0);
        }
        _coordinator.RecordExecution(started.ExperimentId, fallbackSucceeded: false, latencyMs: 10.0, unhandledFailure: true);

        var report = _coordinator.GetExperimentReport(started.ExperimentId);

        report.Should().NotBeNull();
        report!.Status.Should().Be(ExperimentStatus.Aborted);
        report.AbortReason.Should().Contain("error rate");
        report.ResilienceGrade.Should().Be(ResilienceGrade.GradeF);

        // Active rule should be automatically removed
        _mesh.GetActiveRules().Should().BeEmpty();
    }

    [Fact]
    public void RecordExecution_DepletedErrorBudgetBelow80_TriggersAutonomousAbortWithGradeF()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Budget Depletion Drill",
            Target: ChaosTarget.VoiceTelephony,
            FaultType: ChaosFaultType.Http429TooManyRequests,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.50, // 50% max error rate
            CanaryTrafficPercentage: 0.5);

        var started = _coordinator.StartExperiment(request);

        // First inject 20 successful requests (error rate 0%)
        for (int i = 0; i < 20; i++)
        {
            _coordinator.RecordExecution(started.ExperimentId, fallbackSucceeded: true, latencyMs: 10.0, unhandledFailure: false);
        }

        // Budget starts at 100%. Each unhandled failure costs 5%.
        // 5 failures * 5% = 25% reduction -> 75% budget (< 80% SLA floor).
        // Total requests = 25, failures = 5 -> error rate is 20% (< 50% allowed), so budget depletion triggers abort!
        for (int i = 0; i < 5; i++)
        {
            _coordinator.RecordExecution(started.ExperimentId, fallbackSucceeded: false, latencyMs: 10.0, unhandledFailure: true);
        }

        var report = _coordinator.GetExperimentReport(started.ExperimentId);
        report.Should().NotBeNull();
        report!.Status.Should().Be(ExperimentStatus.Aborted);
        report.AbortReason.Should().Contain("error budget depleted");
        report.ResilienceGrade.Should().Be(ResilienceGrade.GradeF);
    }

    [Fact]
    public void AbortExperiment_ManualAbort_StopsExperimentAndSetsReason()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Manual Abort Drill",
            Target: ChaosTarget.PaymentGateway,
            FaultType: ChaosFaultType.TransientIoException,
            DurationSeconds: 120,
            FaultProbability: 0.5,
            MaxAllowedErrorRate: 0.10,
            CanaryTrafficPercentage: 0.2);

        var started = _coordinator.StartExperiment(request);

        var aborted = _coordinator.AbortExperiment(started.ExperimentId, "Engineer requested emergency halt");

        aborted.Status.Should().Be(ExperimentStatus.Aborted);
        aborted.AbortReason.Should().Be("Engineer requested emergency halt");
        aborted.ResilienceGrade.Should().Be(ResilienceGrade.GradeF);
        _mesh.GetActiveRules().Should().BeEmpty();
    }

    [Fact]
    public void GetAutopilotStatus_ReflectsActiveExperimentAndRemainingBudget()
    {
        var statusBefore = _coordinator.GetAutopilotStatus();
        statusBefore.IsRunning.Should().BeFalse();
        statusBefore.SystemErrorBudgetRemaining.Should().Be(100.0);

        var request = new StartExperimentRequest(
            ExperimentName: "Status Verification Drill",
            Target: ChaosTarget.Redis,
            FaultType: ChaosFaultType.RedisCacheMissStorm,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 0.5);

        var report = _coordinator.StartExperiment(request);

        var statusDuring = _coordinator.GetAutopilotStatus();
        statusDuring.IsRunning.Should().BeTrue();
        statusDuring.ActiveExperimentId.Should().Be(report.ExperimentId);

        _coordinator.CompleteExperiment(report.ExperimentId);

        var statusAfter = _coordinator.GetAutopilotStatus();
        statusAfter.IsRunning.Should().BeFalse();
        statusAfter.TotalExperimentsRun.Should().Be(1);
    }

    #endregion

    #region 9. Canary Traffic Blast-Radius Gating & Request Validation

    [Fact]
    public void StartExperiment_CanaryPercentageExceedsOnePercent_ThrowsArgumentOutOfRangeException()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Dangerous 5% Canary Blast Drill",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 5.0); // Exceeds 1.0% limit

        var act = () => _coordinator.StartExperiment(request);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*blast-radius*");
    }

    [Fact]
    public void StartExperiment_ZeroOrNegativeCanaryPercentage_ThrowsArgumentOutOfRangeException()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Zero Canary Drill",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 0.0);

        var act = () => _coordinator.StartExperiment(request);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void StartExperiment_ZeroOrNegativeDuration_ThrowsArgumentOutOfRangeException()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "Zero Duration Drill",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            DurationSeconds: 0,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 1.0);

        var act = () => _coordinator.StartExperiment(request);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void StartExperiment_EmptyExperimentName_ThrowsArgumentException()
    {
        var request = new StartExperimentRequest(
            ExperimentName: "   ",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 1.0);

        var act = () => _coordinator.StartExperiment(request);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StartExperiment_WhileAnotherRunning_ThrowsInvalidOperationException()
    {
        var request1 = new StartExperimentRequest(
            ExperimentName: "Active Drill",
            Target: ChaosTarget.Redis,
            FaultType: ChaosFaultType.RedisCacheMissStorm,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 0.5);

        _coordinator.StartExperiment(request1);

        var request2 = new StartExperimentRequest(
            ExperimentName: "Conflicting Drill",
            Target: ChaosTarget.Database,
            FaultType: ChaosFaultType.DatabaseConnectionDrop,
            DurationSeconds: 60,
            FaultProbability: 1.0,
            MaxAllowedErrorRate: 0.05,
            CanaryTrafficPercentage: 0.5);

        var act = () => _coordinator.StartExperiment(request2);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*currently running*");
    }

    #endregion

    #region 10. Multi-Threaded Concurrency & Thread-Safety

    [Fact]
    public async Task Concurrency_SimultaneousRuleMutationsAndEvaluations_ThreadSafe()
    {
        const int threadCount = 20;
        const int iterationsPerThread = 50;

        var tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() =>
        {
            for (int i = 0; i < iterationsPerThread; i++)
            {
                var ruleId = $"thread-{threadId}-rule-{i}";
                _mesh.AddRule(new ChaosFaultRule(
                    RuleId: ruleId,
                    Target: ChaosTarget.Redis,
                    FaultType: ChaosFaultType.RedisCacheMissStorm,
                    Probability: 0.5));

                _mesh.ShouldInjectFault(ChaosTarget.Redis, out _);

                _mesh.RemoveRule(ruleId);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        var active = _mesh.GetActiveRules();
        active.Should().BeEmpty();
    }

    [Fact]
    public async Task Concurrency_SimultaneousResilientExecutions_MaintainsMetricIntegrity()
    {
        _mesh.AddRule(new ChaosFaultRule(
            RuleId: "concurrent-chaos-rule",
            Target: ChaosTarget.WhatsAppGateway,
            FaultType: ChaosFaultType.CarrierHttp503Error,
            Probability: 1.0));

        const int totalTasks = 30;
        ConcurrentBag<string> capturedResults = [];

        var tasks = Enumerable.Range(0, totalTasks).Select(_ => Task.Run(async () =>
        {
            var result = await _auditor.ExecuteWithResilienceAsync(
                ChaosTarget.WhatsAppGateway,
                primaryAction: () => Task.FromResult("live_wa_success"),
                fallbackAction: () => Task.FromResult("fallback_sms_success"));

            capturedResults.Add(result);
        })).ToArray();

        await Task.WhenAll(tasks);

        capturedResults.Should().HaveCount(totalTasks);
        capturedResults.Should().AllBe("fallback_sms_success");

        var status = _auditor.GetAuditStatus(ChaosTarget.WhatsAppGateway);
        status.TotalExecutions.Should().Be(totalTasks);
        status.InjectedFailures.Should().Be(totalTasks);
        status.FallbacksTriggered.Should().Be(totalTasks);
        status.FallbackSuccessRate.Should().Be(1.0);
    }

    #endregion

    #region 11. Dependency Injection Descriptors Assertion

    [Fact]
    public void AddChaosResilienceServices_RegistersAllExpectedSingletons_WithoutBuildingProvider()
    {
        var services = new ServiceCollection();
        services.AddChaosResilienceServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IChaosFaultInjectionMesh) &&
            d.ImplementationType == typeof(ChaosFaultInjectionMesh) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(ICircuitBreakerResilienceAuditor) &&
            d.ImplementationType == typeof(CircuitBreakerResilienceAuditor) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IResilienceAutopilotCoordinator) &&
            d.ImplementationType == typeof(ResilienceAutopilotCoordinator) &&
            d.Lifetime == ServiceLifetime.Singleton);

        // Strict adherence to Rule 5: NEVER call services.BuildServiceProvider()
    }

    #endregion
}
