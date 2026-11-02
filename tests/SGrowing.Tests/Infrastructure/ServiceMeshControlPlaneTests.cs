using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.ServiceMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public sealed class ServiceMeshControlPlaneTests
{
    private readonly AdaptiveCircuitBreakerControlPlane _controlPlane = new();
    private readonly CanaryTrafficSplitterService _canaryService = new();
    private readonly DistributedRateLimitTokenBucket _rateLimiter = new();

    #region Circuit Breaker & Outlier Ejection Tests

    [Fact]
    public void SelectActiveInstance_MultipleInstances_SelectsHealthyLowestLatencyInstance()
    {
        _controlPlane.RegisterInstance("INST-01", "OrderService", "10.0.1.10", 8080);
        _controlPlane.RegisterInstance("INST-02", "OrderService", "10.0.1.11", 8080);

        // INST-01 records fast latency (5ms), INST-02 records slower latency (50ms)
        _controlPlane.RecordCallResult("INST-01", success: true, latencyMs: 5.0);
        _controlPlane.RecordCallResult("INST-02", success: true, latencyMs: 50.0);

        var selected = _controlPlane.SelectActiveInstance("OrderService");

        selected.Should().NotBeNull();
        selected!.InstanceId.Should().Be("INST-01", "Control plane should route to lowest EWMA latency instance.");
    }

    [Fact]
    public void RecordCallResult_FiveConsecutiveFailures_EjectsInstanceFromActivePool()
    {
        _controlPlane.RegisterInstance("INST-FAILING", "PaymentService", "10.0.2.1", 8080);
        _controlPlane.RegisterInstance("INST-BACKUP", "PaymentService", "10.0.2.2", 8080);

        // 4 failures -> still in closed state
        for (var i = 0; i < 4; i++)
        {
            _controlPlane.RecordCallResult("INST-FAILING", success: false, latencyMs: 100.0);
        }

        var check1 = _controlPlane.SelectActiveInstance("PaymentService");
        check1.Should().NotBeNull();

        // 5th failure -> triggers outlier ejection
        _controlPlane.RecordCallResult("INST-FAILING", success: false, latencyMs: 100.0);

        var all = _controlPlane.GetAllInstances();
        var ejected = all.First(i => i.InstanceId == "INST-FAILING");
        ejected.State.Should().Be(CircuitState.Open);
        ejected.EjectedUntilUtc.Should().BeAfter(DateTime.UtcNow);
        ejected.TotalEjections.Should().Be(1);

        // Active selection should now strictly route to backup
        var selected = _controlPlane.SelectActiveInstance("PaymentService");
        selected!.InstanceId.Should().Be("INST-BACKUP");
    }

    #endregion

    #region Canary Evaluation & Automated Rollback Tests

    [Fact]
    public void EvaluateCanary_HighCanaryErrorRate_TriggersAutomatedRollback()
    {
        var policy = new CanaryPolicy(
            ServiceName: "CheckoutService",
            BaselineVersion: "v1.2.0",
            CanaryVersion: "v2.0.0-rc1",
            CanaryTrafficRatio: 0.10,
            MaxAllowedErrorDeltaPercent: 2.0,
            MaxAllowedLatencyDeltaPercent: 20.0
        );

        _canaryService.SetPolicy(policy);

        // Baseline: 50 calls, 0 errors, 20ms latency
        for (var i = 0; i < 50; i++)
        {
            _canaryService.RecordMetric("CheckoutService", "v1.2.0", success: true, latencyMs: 20.0);
        }

        // Canary: 15 calls, 3 errors (20% error rate > 2% threshold)
        for (var i = 0; i < 12; i++)
        {
            _canaryService.RecordMetric("CheckoutService", "v2.0.0-rc1", success: true, latencyMs: 25.0);
        }
        for (var i = 0; i < 3; i++)
        {
            _canaryService.RecordMetric("CheckoutService", "v2.0.0-rc1", success: false, latencyMs: 25.0);
        }

        var report = _canaryService.EvaluateCanary("CheckoutService");

        report.Should().NotBeNull();
        report.Decision.Should().Be(CanaryDecision.RollbackCanary);
        report.Reason.Should().Contain("CRITICAL REGRESSION");
        report.CanaryErrorRatePercent.Should().Be(20.0);
    }

    [Fact]
    public void EvaluateCanary_HealthyPerformance_KeepsProbingOrPromotes()
    {
        var policy = new CanaryPolicy(
            ServiceName: "InventoryService",
            BaselineVersion: "v1.0.0",
            CanaryVersion: "v1.1.0",
            CanaryTrafficRatio: 0.20
        );

        _canaryService.SetPolicy(policy);

        for (var i = 0; i < 30; i++)
        {
            _canaryService.RecordMetric("InventoryService", "v1.0.0", success: true, latencyMs: 15.0);
            _canaryService.RecordMetric("InventoryService", "v1.1.0", success: true, latencyMs: 14.0);
        }

        var report = _canaryService.EvaluateCanary("InventoryService");
        report.Decision.Should().Be(CanaryDecision.KeepProbing);
    }

    #endregion

    #region Distributed Token Bucket Tests

    [Fact]
    public void TryAcquire_WithinCapacity_AllowsAndDecrementsTokens()
    {
        var res1 = _rateLimiter.TryAcquire(
            tenantId: "TENANT-001",
            serviceKey: "SearchApi",
            tokenCost: 10.0,
            capacity: 50.0,
            refillTokensPerSecond: 10.0
        );

        res1.Allowed.Should().BeTrue();
        res1.RemainingTokens.Should().Be(40.0);

        // Exhaust remaining tokens
        var res2 = _rateLimiter.TryAcquire("TENANT-001", "SearchApi", 40.0, 50.0, 10.0);
        res2.Allowed.Should().BeTrue();
        res2.RemainingTokens.Should().BeApproximately(0.0, 0.05);

        // Next call must be rejected
        var res3 = _rateLimiter.TryAcquire("TENANT-001", "SearchApi", 5.0, 50.0, 10.0);
        res3.Allowed.Should().BeFalse();
        res3.RetryAfter.Should().BeGreaterThan(TimeSpan.Zero);
    }

    #endregion
}
