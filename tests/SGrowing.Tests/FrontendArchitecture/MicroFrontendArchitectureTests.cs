using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.FrontendArchitecture.MicroFrontends;
using Xunit;

namespace SGrowing.Tests.FrontendArchitecture;

public class MicroFrontendArchitectureTests
{
    private readonly MicroFrontendHostEngine _hostEngine;
    private readonly RemoteMicroAppCircuitBreaker _circuitBreaker;
    private readonly FederatedCrossAppEventBus _eventBus;
    private readonly EdgeSsrHydrationService _ssrService;

    public MicroFrontendArchitectureTests()
    {
        _hostEngine = new MicroFrontendHostEngine();
        _circuitBreaker = new RemoteMicroAppCircuitBreaker(_hostEngine, defaultFailureThreshold: 3, defaultResetTimeoutSeconds: 1);
        _eventBus = new FederatedCrossAppEventBus("test-secret-key-32-bytes-secure!!", maxClockSkewSeconds: 300);
        _ssrService = new EdgeSsrHydrationService();
    }

    #region 1. MicroFrontendHostEngine & Module Federation Manifest

    [Fact]
    public void MicroFrontendHostEngine_InitialState_SeedsStandardRemotes()
    {
        var remotes = _hostEngine.GetAllRemotes();

        remotes.Should().HaveCount(4);
        remotes.Should().Contain(r => r.ModuleId == "@sgrowing/cockpit-commerce");
        remotes.Should().Contain(r => r.ModuleId == "@sgrowing/cockpit-fintech");
        remotes.Should().Contain(r => r.ModuleId == "@sgrowing/cockpit-agents");
        remotes.Should().Contain(r => r.ModuleId == "@sgrowing/cockpit-voice");
    }

    [Fact]
    public void GetFederationManifest_ReturnsHostVersionRemotesAndSharedSingletons()
    {
        var manifest = _hostEngine.GetFederationManifest("22.1.0");

        manifest.HostVersion.Should().Be("22.1.0");
        manifest.Remotes.Should().HaveCount(4);
        manifest.SharedDependencies.Should().ContainKey("@angular/core")
            .WhoseValue.Should().Be("^22.0.0");
        manifest.SharedDependencies.Should().ContainKey("rxjs")
            .WhoseValue.Should().Be("^7.8.1");
        manifest.SharedDependencies.Should().ContainKey("@microsoft/signalr")
            .WhoseValue.Should().Be("^10.0.0");
    }

    [Fact]
    public void RegisterRemote_DynamicallyAddsNewRemoteModule()
    {
        var dynamicRemote = new RemoteModuleMetadata(
            ModuleId: "@sgrowing/cockpit-analytics",
            RemoteEntryUrl: "https://cdn.sgrowing.io/mfe/analytics/remoteEntry.js",
            ExposedComponent: "AnalyticsModule",
            Version: "22.3.0",
            HealthStatus: RemoteHealthStatus.Healthy,
            LatencyMs: 9.8,
            LastPingUtc: DateTimeOffset.UtcNow);

        var registered = _hostEngine.RegisterRemote(dynamicRemote);

        registered.Should().BeTrue();
        _hostEngine.GetRemote("@sgrowing/cockpit-analytics").Should().NotBeNull();
        _hostEngine.GetRemote("@sgrowing/cockpit-analytics")!.Version.Should().Be("22.3.0");
    }

    [Fact]
    public void UnregisterRemote_RemovesRegisteredRemoteModule()
    {
        var unregistered = _hostEngine.UnregisterRemote("@sgrowing/cockpit-voice");

        unregistered.Should().BeTrue();
        _hostEngine.GetRemote("@sgrowing/cockpit-voice").Should().BeNull();
    }

    [Fact]
    public void UpdateRemoteHealth_UpdatesStatusLatencyAndTimestamp()
    {
        var updated = _hostEngine.UpdateRemoteHealth("@sgrowing/cockpit-commerce", RemoteHealthStatus.Degraded, 142.5);

        updated.Should().BeTrue();
        var remote = _hostEngine.GetRemote("@sgrowing/cockpit-commerce");
        remote.Should().NotBeNull();
        remote!.HealthStatus.Should().Be(RemoteHealthStatus.Degraded);
        remote.LatencyMs.Should().Be(142.5);
    }

    [Fact]
    public void UpdateRemoteHealth_NonExistentRemote_ReturnsFalse()
    {
        var updated = _hostEngine.UpdateRemoteHealth("@sgrowing/unknown-module", RemoteHealthStatus.Healthy, 20.0);
        updated.Should().BeFalse();
    }

    #endregion

    #region 2. SemVer Version Resolution

    [Theory]
    [InlineData("^22.0.0", "22.0.0", true)]
    [InlineData("^22.0.0", "22.4.1", true)]
    [InlineData("^22.0.0", "23.0.0", false)]
    [InlineData("^22.0.0", "21.9.9", false)]
    [InlineData("^0.2.0", "0.2.3", true)]
    [InlineData("^0.2.0", "0.3.0", false)]
    [InlineData("~22.1.0", "22.1.5", true)]
    [InlineData("~22.1.0", "22.2.0", false)]
    [InlineData(">=22.0.0", "22.5.0", true)]
    [InlineData(">=22.0.0", "21.0.0", false)]
    [InlineData("*", "1.0.0", true)]
    [InlineData("latest", "22.0.0", true)]
    [InlineData("22.4.0", "22.4.0", true)]
    [InlineData("22.4.0", "22.4.1", false)]
    public void ResolveSemVerMatch_EvaluatesVersionCompatibilityCorrectly(
        string requestedConstraint,
        string availableVersion,
        bool expectedResult)
    {
        var result = _hostEngine.ResolveSemVerMatch(requestedConstraint, availableVersion);
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void ResolveCompatibleRemote_WhenConstraintSatisfied_ReturnsRemoteMetadata()
    {
        var resolved = _hostEngine.ResolveCompatibleRemote("@sgrowing/cockpit-commerce", "^22.0.0");

        resolved.Should().NotBeNull();
        resolved!.ModuleId.Should().Be("@sgrowing/cockpit-commerce");
        resolved.Version.Should().Be("22.4.0");
    }

    [Fact]
    public void ResolveCompatibleRemote_WhenConstraintViolated_ReturnsNull()
    {
        var resolved = _hostEngine.ResolveCompatibleRemote("@sgrowing/cockpit-commerce", "^23.0.0");
        resolved.Should().BeNull();
    }

    [Fact]
    public void ResolveCompatibleRemote_NonExistentModule_ReturnsNull()
    {
        var resolved = _hostEngine.ResolveCompatibleRemote("@sgrowing/non-existent", "^22.0.0");
        resolved.Should().BeNull();
    }

    #endregion

    #region 3. Remote Micro-App Circuit Breaker State Transitions

    [Fact]
    public void CircuitBreaker_InitialState_IsClosedWithZeroFailures()
    {
        var state = _circuitBreaker.GetState("@sgrowing/cockpit-commerce");
        var metrics = _circuitBreaker.GetMetrics("@sgrowing/cockpit-commerce");

        state.Should().Be(CircuitState.Closed);
        metrics.ConsecutiveFailures.Should().Be(0);
        metrics.FailureThreshold.Should().Be(3);
        _circuitBreaker.AllowExecution("@sgrowing/cockpit-commerce").Should().BeTrue();
    }

    [Fact]
    public void CircuitBreaker_FailuresBelowThreshold_RemainsClosed()
    {
        var metrics1 = _circuitBreaker.RecordFailure("@sgrowing/cockpit-commerce", "Timeout 1");
        var metrics2 = _circuitBreaker.RecordFailure("@sgrowing/cockpit-commerce", "Timeout 2");

        metrics1.ConsecutiveFailures.Should().Be(1);
        metrics1.State.Should().Be(CircuitState.Closed);

        metrics2.ConsecutiveFailures.Should().Be(2);
        metrics2.State.Should().Be(CircuitState.Closed);
        _circuitBreaker.GetState("@sgrowing/cockpit-commerce").Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void CircuitBreaker_ReachingFailureThreshold_TransitionsToOpen()
    {
        _circuitBreaker.RecordFailure("@sgrowing/cockpit-commerce");
        _circuitBreaker.RecordFailure("@sgrowing/cockpit-commerce");
        var metrics = _circuitBreaker.RecordFailure("@sgrowing/cockpit-commerce");

        metrics.State.Should().Be(CircuitState.Open);
        metrics.ConsecutiveFailures.Should().Be(3);
        metrics.TotalTripCount.Should().Be(1);

        _circuitBreaker.GetState("@sgrowing/cockpit-commerce").Should().Be(CircuitState.Open);
        _circuitBreaker.AllowExecution("@sgrowing/cockpit-commerce").Should().BeFalse();

        // Host engine health should reflect CircuitOpen
        var remote = _hostEngine.GetRemote("@sgrowing/cockpit-commerce");
        remote!.HealthStatus.Should().Be(RemoteHealthStatus.CircuitOpen);
    }

    [Fact]
    public void CircuitBreaker_RecordSuccessInClosed_ResetsConsecutiveFailures()
    {
        _circuitBreaker.RecordFailure("@sgrowing/cockpit-commerce");
        _circuitBreaker.RecordFailure("@sgrowing/cockpit-commerce");
        var metrics = _circuitBreaker.RecordSuccess("@sgrowing/cockpit-commerce");

        metrics.ConsecutiveFailures.Should().Be(0);
        metrics.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void CircuitBreaker_ForceTrip_ImmediatelyOpensCircuit()
    {
        var metrics = _circuitBreaker.ForceTrip("@sgrowing/cockpit-fintech", "Simulated Bundle Crash");

        metrics.State.Should().Be(CircuitState.Open);
        metrics.TotalTripCount.Should().Be(1);
        _circuitBreaker.AllowExecution("@sgrowing/cockpit-fintech").Should().BeFalse();
    }

    [Fact]
    public void CircuitBreaker_ResetCircuit_ReturnsStateToClosed()
    {
        _circuitBreaker.ForceTrip("@sgrowing/cockpit-agents", "Manual Fault Injection");
        var metrics = _circuitBreaker.ResetCircuit("@sgrowing/cockpit-agents");

        metrics.State.Should().Be(CircuitState.Closed);
        metrics.ConsecutiveFailures.Should().Be(0);
        _circuitBreaker.AllowExecution("@sgrowing/cockpit-agents").Should().BeTrue();
    }

    [Fact]
    public async Task CircuitBreaker_AfterResetTimeout_TransitionsOpenToHalfOpen()
    {
        // CircuitBreaker initialized with 1 second reset timeout
        _circuitBreaker.ForceTrip("@sgrowing/cockpit-voice", "Network Disruption");

        _circuitBreaker.GetState("@sgrowing/cockpit-voice").Should().Be(CircuitState.Open);

        // Wait for timeout to elapse
        await Task.Delay(1100);

        var state = _circuitBreaker.GetState("@sgrowing/cockpit-voice");
        state.Should().Be(CircuitState.HalfOpen);
        _circuitBreaker.AllowExecution("@sgrowing/cockpit-voice").Should().BeTrue();
    }

    [Fact]
    public async Task CircuitBreaker_InHalfOpen_SuccessfulProbe_TransitionsToClosed()
    {
        _circuitBreaker.ForceTrip("@sgrowing/cockpit-voice", "Network Disruption");
        await Task.Delay(1100); // Transition to HalfOpen

        _circuitBreaker.GetState("@sgrowing/cockpit-voice").Should().Be(CircuitState.HalfOpen);

        var metrics = _circuitBreaker.RecordSuccess("@sgrowing/cockpit-voice");

        metrics.State.Should().Be(CircuitState.Closed);
        metrics.ConsecutiveFailures.Should().Be(0);
        _circuitBreaker.GetState("@sgrowing/cockpit-voice").Should().Be(CircuitState.Closed);
    }

    [Fact]
    public async Task CircuitBreaker_InHalfOpen_FailedProbe_TransitionsBackToOpen()
    {
        _circuitBreaker.ForceTrip("@sgrowing/cockpit-voice", "Network Disruption");
        await Task.Delay(1100); // Transition to HalfOpen

        _circuitBreaker.GetState("@sgrowing/cockpit-voice").Should().Be(CircuitState.HalfOpen);

        var metrics = _circuitBreaker.RecordFailure("@sgrowing/cockpit-voice", "Probe Failed");

        metrics.State.Should().Be(CircuitState.Open);
        metrics.TotalTripCount.Should().Be(2);
        _circuitBreaker.GetState("@sgrowing/cockpit-voice").Should().Be(CircuitState.Open);
        _circuitBreaker.AllowExecution("@sgrowing/cockpit-voice").Should().BeFalse();
    }

    [Fact]
    public void CircuitBreaker_GetAllMetrics_ReturnsMetricsForTrackedModules()
    {
        _circuitBreaker.RecordFailure("@sgrowing/cockpit-commerce");
        _circuitBreaker.RecordSuccess("@sgrowing/cockpit-fintech");

        var allMetrics = _circuitBreaker.GetAllMetrics();

        allMetrics.Count.Should().BeGreaterThanOrEqualTo(2);
        allMetrics.Should().Contain(m => m.ModuleId == "@sgrowing/cockpit-commerce");
        allMetrics.Should().Contain(m => m.ModuleId == "@sgrowing/cockpit-fintech");
    }

    #endregion

    #region 4. Fallback View Representation

    [Fact]
    public void GetFallbackView_WhenCircuitOpen_EmitsActiveFallbackView()
    {
        _circuitBreaker.ForceTrip("@sgrowing/cockpit-commerce", "Remote Script 404");

        var fallback = _circuitBreaker.GetFallbackView("@sgrowing/cockpit-commerce");

        fallback.ModuleId.Should().Be("@sgrowing/cockpit-commerce");
        fallback.IsFallbackActive.Should().BeTrue();
        fallback.FallbackHtml.Should().Contain("Resilient Degraded Mode");
        fallback.FallbackHtml.Should().Contain("data-circuit-state=\"Open\"");
        fallback.FallbackHtml.Should().Contain("Retry Component Loading");
        fallback.FallbackComponentUrl.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetFallbackView_WhenCircuitClosed_EmitsInactiveFallbackView()
    {
        var fallback = _circuitBreaker.GetFallbackView("@sgrowing/cockpit-commerce");

        fallback.IsFallbackActive.Should().BeFalse();
        fallback.RetryAfterSeconds.Should().Be(0);
        fallback.FallbackHtml.Should().Contain("data-circuit-state=\"Closed\"");
    }

    #endregion

    #region 5. Federated Cross-App Event Bus & Cryptographic Security

    [Fact]
    public void GenerateSignature_CreatesValidHMACSHA256Hex()
    {
        var now = DateTimeOffset.UtcNow;
        var signature = _eventBus.GenerateSignature(
            sourceAppId: "@sgrowing/cockpit-commerce",
            targetAppId: "@sgrowing/cockpit-fintech",
            eventType: "ORDER_CHECKOUT_COMPLETED",
            payloadJson: "{\"orderId\":\"ORD-101\",\"amount\":499.00}",
            nonce: Guid.NewGuid().ToString("N"),
            timestampUtc: now);

        signature.Should().NotBeNullOrWhiteSpace();
        signature.Length.Should().Be(64); // 256 bits = 32 bytes = 64 hex characters
    }

    [Fact]
    public async Task PublishAsync_ValidSignatureAndNonce_DeliversToSubscribedHandler()
    {
        var received = false;
        CrossAppEvent? deliveredEvent = null;

        using var sub = _eventBus.Subscribe("ORDER_CHECKOUT_COMPLETED", evt =>
        {
            received = true;
            deliveredEvent = evt;
            return Task.CompletedTask;
        });

        var now = DateTimeOffset.UtcNow;
        var nonce = Guid.NewGuid().ToString("N");
        var payload = "{\"orderId\":\"ORD-999\",\"total\":120.50}";

        var sig = _eventBus.GenerateSignature("@sgrowing/cockpit-commerce", "*", "ORDER_CHECKOUT_COMPLETED", payload, nonce, now);
        var appEvent = new CrossAppEvent(
            EventId: Guid.NewGuid(),
            SourceAppId: "@sgrowing/cockpit-commerce",
            TargetAppId: "*",
            EventType: "ORDER_CHECKOUT_COMPLETED",
            PayloadJson: payload,
            Nonce: nonce,
            TimestampUtc: now,
            Signature: sig);

        var result = await _eventBus.PublishAsync(appEvent);

        result.IsSuccess.Should().BeTrue();
        result.DeliveredCount.Should().Be(1);
        received.Should().BeTrue();
        deliveredEvent.Should().NotBeNull();
        deliveredEvent!.EventId.Should().Be(appEvent.EventId);
    }

    [Fact]
    public async Task PublishAsync_WildcardSubscriber_ReceivesAnyEventType()
    {
        var wildcardDelivered = false;

        using var wildcardSub = _eventBus.Subscribe("*", _ =>
        {
            wildcardDelivered = true;
            return Task.CompletedTask;
        });

        var now = DateTimeOffset.UtcNow;
        var nonce = Guid.NewGuid().ToString("N");
        var sig = _eventBus.GenerateSignature("@sgrowing/cockpit-agents", "*", "AGENT_PROMPT_DISPATCHED", "{}", nonce, now);

        var appEvent = new CrossAppEvent(
            EventId: Guid.NewGuid(),
            SourceAppId: "@sgrowing/cockpit-agents",
            TargetAppId: "*",
            EventType: "AGENT_PROMPT_DISPATCHED",
            PayloadJson: "{}",
            Nonce: nonce,
            TimestampUtc: now,
            Signature: sig);

        var result = await _eventBus.PublishAsync(appEvent);

        result.IsSuccess.Should().BeTrue();
        wildcardDelivered.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_TamperedSignature_IsRejectedAndIncrementsMetric()
    {
        var now = DateTimeOffset.UtcNow;
        var nonce = Guid.NewGuid().ToString("N");

        var appEvent = new CrossAppEvent(
            EventId: Guid.NewGuid(),
            SourceAppId: "@sgrowing/cockpit-commerce",
            TargetAppId: "@sgrowing/cockpit-fintech",
            EventType: "FUNDS_TRANSFER_REQUESTED",
            PayloadJson: "{\"amount\":1000000}",
            Nonce: nonce,
            TimestampUtc: now,
            Signature: "0000000000000000000000000000000000000000000000000000000000000000"); // Forged/Tampered

        var result = await _eventBus.PublishAsync(appEvent);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TAMPERED_SIGNATURE");

        var metrics = _eventBus.GetMetrics();
        metrics.TotalTamperedSignaturesBlocked.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PublishAsync_ReplayAttackReusingNonce_IsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var nonce = "fixed-replay-nonce-" + Guid.NewGuid();
        var payload = "{\"action\":\"login\"}";

        var sig = _eventBus.GenerateSignature("@sgrowing/auth", "*", "USER_LOGGED_IN", payload, nonce, now);
        var appEvent = new CrossAppEvent(
            EventId: Guid.NewGuid(),
            SourceAppId: "@sgrowing/auth",
            TargetAppId: "*",
            EventType: "USER_LOGGED_IN",
            PayloadJson: payload,
            Nonce: nonce,
            TimestampUtc: now,
            Signature: sig);

        // First attempt succeeds
        var result1 = await _eventBus.PublishAsync(appEvent);
        result1.IsSuccess.Should().BeTrue();

        // Second attempt with exact same nonce is rejected as replay attack
        var appEventDuplicate = appEvent with { EventId = Guid.NewGuid() };
        var result2 = await _eventBus.PublishAsync(appEventDuplicate);

        result2.IsSuccess.Should().BeFalse();
        result2.ErrorCode.Should().Be("REPLAY_ATTACK_DETECTED");

        var metrics = _eventBus.GetMetrics();
        metrics.TotalReplayAttacksBlocked.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PublishAsync_ExpiredTimestampOutsideClockSkew_IsRejected()
    {
        var expiredTime = DateTimeOffset.UtcNow.AddMinutes(-10); // 10 minutes old (> 300s)
        var nonce = Guid.NewGuid().ToString("N");
        var payload = "{\"status\":\"ping\"}";

        var sig = _eventBus.GenerateSignature("@sgrowing/cockpit-voice", "*", "HEARTBEAT", payload, nonce, expiredTime);
        var appEvent = new CrossAppEvent(
            EventId: Guid.NewGuid(),
            SourceAppId: "@sgrowing/cockpit-voice",
            TargetAppId: "*",
            EventType: "HEARTBEAT",
            PayloadJson: payload,
            Nonce: nonce,
            TimestampUtc: expiredTime,
            Signature: sig);

        var result = await _eventBus.PublishAsync(appEvent);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("TIMESTAMP_EXPIRED");

        var metrics = _eventBus.GetMetrics();
        metrics.TotalExpiredTimestampsBlocked.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Unsubscribe_TokenDisposed_StopsReceivingEvents()
    {
        var deliveryCount = 0;
        var subscription = _eventBus.Subscribe("METRIC_PING", _ =>
        {
            deliveryCount++;
            return Task.CompletedTask;
        });

        var now = DateTimeOffset.UtcNow;
        var nonce1 = Guid.NewGuid().ToString("N");
        var sig1 = _eventBus.GenerateSignature("source", "*", "METRIC_PING", "{}", nonce1, now);
        await _eventBus.PublishAsync(new CrossAppEvent(Guid.NewGuid(), "source", "*", "METRIC_PING", "{}", nonce1, now, sig1));

        deliveryCount.Should().Be(1);

        // Unsubscribe
        subscription.Dispose();

        var nonce2 = Guid.NewGuid().ToString("N");
        var sig2 = _eventBus.GenerateSignature("source", "*", "METRIC_PING", "{}", nonce2, now);
        await _eventBus.PublishAsync(new CrossAppEvent(Guid.NewGuid(), "source", "*", "METRIC_PING", "{}", nonce2, now, sig2));

        // Delivery count should not have increased
        deliveryCount.Should().Be(1);
    }

    [Fact]
    public async Task EventHistory_RecordsPublishedEventsAndAllowsFiltering()
    {
        _eventBus.ClearHistory();

        var now = DateTimeOffset.UtcNow;
        var nonce = Guid.NewGuid().ToString("N");
        var sig = _eventBus.GenerateSignature("src", "dst", "CUSTOM_EVENT", "{}", nonce, now);

        await _eventBus.PublishAsync(new CrossAppEvent(Guid.NewGuid(), "src", "dst", "CUSTOM_EVENT", "{}", nonce, now, sig));

        var history = _eventBus.GetEventHistory("CUSTOM_EVENT");
        history.Should().HaveCount(1);
        history[0].EventType.Should().Be("CUSTOM_EVENT");
    }

    #endregion

    #region 6. Edge SSR Manifest & Progressive Hydration Strategies

    [Fact]
    public void GenerateManifest_CockpitCommerceRoute_ContainsCriticalCssAndPreRenderedHtml()
    {
        var manifest = _ssrService.GenerateManifest("/cockpit/commerce");

        manifest.RoutePath.Should().Be("/cockpit/commerce");
        manifest.PreRenderedHtml.Should().Contain("data-route=\"/cockpit/commerce\"");
        manifest.PreRenderedHtml.Should().Contain("@sgrowing/cockpit-commerce/HeroBanner");
        manifest.PreRenderedHtml.Should().Contain("@sgrowing/cockpit-commerce/ProductGrid");
        manifest.CriticalCss.Should().Contain("Zero CLS Guarantee");
        manifest.IntersectingComponents.Should().Contain("@sgrowing/cockpit-commerce/HeroBanner");
    }

    [Fact]
    public void GenerateManifest_AssignsProgressiveHydrationStrategiesCorrectly()
    {
        var manifest = _ssrService.GenerateManifest("/cockpit/commerce");

        manifest.ComponentHydrationMap.Should().NotBeNull();
        var map = manifest.ComponentHydrationMap!;

        map["@sgrowing/cockpit-commerce/HeroBanner"].Should().Be(HydrationStrategy.Immediate);
        map["@sgrowing/cockpit-commerce/ProductGrid"].Should().Be(HydrationStrategy.OnVisible);
        map["@sgrowing/cockpit-commerce/CartSummary"].Should().Be(HydrationStrategy.OnIdle);
    }

    [Fact]
    public void ResolveIntersectingComponents_DesktopVsMobile_ReturnsResponsiveIntersections()
    {
        var desktopComponents = _ssrService.ResolveIntersectingComponents("/cockpit/fintech", viewportWidth: 1280);
        var mobileComponents = _ssrService.ResolveIntersectingComponents("/cockpit/fintech", viewportWidth: 480);

        desktopComponents.Should().Contain("@sgrowing/cockpit-fintech/RevenueKpiSummary");
        desktopComponents.Should().Contain("@sgrowing/cockpit-fintech/TransactionLedger");

        // Mobile restricts initial intersection to above-the-fold
        mobileComponents.Should().Contain("@sgrowing/cockpit-fintech/RevenueKpiSummary");
        mobileComponents.Should().NotContain("@sgrowing/cockpit-fintech/TransactionLedger");
    }

    [Fact]
    public void DetermineHydrationStrategy_MapsComponentTypesAppropriately()
    {
        _ssrService.DetermineHydrationStrategy("HeroBanner", isAboveTheFold: true)
            .Should().Be(HydrationStrategy.Immediate);

        _ssrService.DetermineHydrationStrategy("ProductGrid", isAboveTheFold: false)
            .Should().Be(HydrationStrategy.OnVisible);

        _ssrService.DetermineHydrationStrategy("TransactionLedger", isAboveTheFold: false)
            .Should().Be(HydrationStrategy.OnVisible);

        _ssrService.DetermineHydrationStrategy("CartSummary", isAboveTheFold: false)
            .Should().Be(HydrationStrategy.OnIdle);
    }

    [Fact]
    public void GetPreCachedManifests_PreRendersAllPrimaryCockpitRoutes()
    {
        var preCached = _ssrService.GetPreCachedManifests();

        preCached.Should().HaveCount(4);
        preCached.Should().Contain(m => m.RoutePath == "/cockpit/commerce");
        preCached.Should().Contain(m => m.RoutePath == "/cockpit/fintech");
        preCached.Should().Contain(m => m.RoutePath == "/cockpit/agents");
        preCached.Should().Contain(m => m.RoutePath == "/cockpit/voice");
    }

    #endregion

    #region 7. Dependency Injection Registration

    [Fact]
    public void AddMicroFrontendServices_RegistersSingletonsDirectlyWithoutBuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddMicroFrontendServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IMicroFrontendHostEngine) &&
            d.ImplementationType == typeof(MicroFrontendHostEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IRemoteMicroAppCircuitBreaker) &&
            d.ImplementationType == typeof(RemoteMicroAppCircuitBreaker) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IFederatedCrossAppEventBus) &&
            d.ImplementationType == typeof(FederatedCrossAppEventBus) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IEdgeSsrHydrationService) &&
            d.ImplementationType == typeof(EdgeSsrHydrationService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
