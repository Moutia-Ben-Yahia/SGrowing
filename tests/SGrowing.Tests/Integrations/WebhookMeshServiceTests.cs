using FluentAssertions;
using SGrowing.Api.Modules.Integrations;
using Xunit;

namespace SGrowing.Tests.Integrations;

public class WebhookMeshServiceTests
{
    private readonly WebhookMeshService _service;

    public WebhookMeshServiceTests()
    {
        _service = new WebhookMeshService();
    }

    [Fact]
    public void ComputeHmacSignature_GeneratesDeterministic64CharHexSignature()
    {
        // Arrange
        var secret = "whsec_live_test_secret_key_889900";
        var payload = "{\"event\":\"order.created\",\"orderId\":\"ORD-1002\"}";
        var timestamp = 1793112800L;

        // Act
        var sig1 = _service.ComputeHmacSignature(payload, secret, timestamp);
        var sig2 = _service.ComputeHmacSignature(payload, secret, timestamp);

        // Assert
        sig1.Should().NotBeNullOrWhiteSpace();
        sig1.Length.Should().Be(64); // 32 bytes in hex = 64 characters
        sig1.Should().MatchRegex("^[a-f0-9]{64}$");
        sig1.Should().Be(sig2, "Same inputs must produce identical HMAC signatures");

        // Different timestamp or payload must change signature
        var sigDiffTime = _service.ComputeHmacSignature(payload, secret, timestamp + 1);
        sigDiffTime.Should().NotBe(sig1);

        var sigDiffPayload = _service.ComputeHmacSignature(payload + " ", secret, timestamp);
        sigDiffPayload.Should().NotBe(sig1);
    }

    [Fact]
    public void VerifyHmacSignature_ValidatesCorrectSignatureAndRejectsTampered()
    {
        // Arrange
        var secret = "whsec_verification_secret_12345";
        var payload = "{\"status\":\"approved\",\"amount\":450.00}";
        var timestamp = 1793112999L;
        var validSig = _service.ComputeHmacSignature(payload, secret, timestamp);

        // Act & Assert
        // 1. Valid signature
        _service.VerifyHmacSignature(payload, validSig, secret, timestamp).Should().BeTrue();

        // 2. Signature with optional 'sha256=' prefix
        _service.VerifyHmacSignature(payload, $"sha256={validSig}", secret, timestamp).Should().BeTrue();

        // 3. Tampered payload
        var tamperedPayload = "{\"status\":\"approved\",\"amount\":999.00}";
        _service.VerifyHmacSignature(tamperedPayload, validSig, secret, timestamp).Should().BeFalse();

        // 4. Wrong secret
        _service.VerifyHmacSignature(payload, validSig, "whsec_wrong_key", timestamp).Should().BeFalse();

        // 5. Wrong timestamp
        _service.VerifyHmacSignature(payload, validSig, secret, timestamp + 60).Should().BeFalse();

        // 6. Empty / null signature
        _service.VerifyHmacSignature(payload, "", secret, timestamp).Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 0.5, 2.5)]   // 2^1 = 2s, +0.5s = 2.5s
    [InlineData(2, -1.0, 3.0)]  // 2^2 = 4s, -1.0s = 3.0s
    [InlineData(3, 0.0, 8.0)]   // 2^3 = 8s, +0.0s = 8.0s
    [InlineData(4, 2.0, 18.0)]  // 2^4 = 16s, +2.0s = 18.0s
    public void CalculateBackoff_AppliesExactExponentialBaseAndJitter(int attempt, double explicitJitter, double expectedSeconds)
    {
        // Act
        var backoff = _service.CalculateBackoff(attempt, explicitJitter);

        // Assert
        backoff.TotalSeconds.Should().BeApproximately(expectedSeconds, 0.001);
    }

    [Fact]
    public void CalculateBackoff_ClampsToMinimumFloorWhenNegativeJitterExceedsBase()
    {
        // Attempt 1 = 2s base, jitter = -10s -> total would be negative without floor
        var backoff = _service.CalculateBackoff(1, -10.0);
        backoff.TotalSeconds.Should().Be(0.1);
    }

    [Fact]
    public void CalculateFullJitterBackoff_ProducesPositiveDurationWithinJitterBounds()
    {
        var rng = new Random(42);

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            var baseSeconds = Math.Pow(2, attempt);
            var backoff = _service.CalculateFullJitterBackoff(attempt, rng);

            // Jitter span is +/- 25% of base
            var minExpected = baseSeconds * 0.75;
            var maxExpected = baseSeconds * 1.25;

            backoff.TotalSeconds.Should().BeInRange(minExpected - 0.01, maxExpected + 0.01);
            backoff.TotalSeconds.Should().BeGreaterThan(0.1);
        }
    }

    [Fact]
    public void CircuitBreaker_StartsInClosedState_AllowsExecution()
    {
        // Arrange
        var testUrl = $"https://api.test-host-{Guid.NewGuid():N}.com/webhook";

        // Act
        var cb = _service.GetOrCreateCircuitBreaker(testUrl);

        // Assert
        cb.State.Should().Be(CircuitBreakerState.Closed);
        cb.CanExecute().Should().BeTrue();
        cb.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void CircuitBreaker_TripsToOpen_AfterReachingConsecutiveFailureThreshold()
    {
        // Arrange
        var testUrl = $"https://api.failing-host-{Guid.NewGuid():N}.com/webhook";
        var cb = _service.GetOrCreateCircuitBreaker(testUrl);
        cb.FailureThreshold = 5;

        // Act - 4 failures should keep it Closed
        for (int i = 1; i <= 4; i++)
        {
            cb.RecordFailure($"HTTP 500 error attempt {i}");
            cb.State.Should().Be(CircuitBreakerState.Closed);
            cb.CanExecute().Should().BeTrue();
        }

        // 5th failure trips it to Open
        cb.RecordFailure("HTTP 500 final failure");

        // Assert
        cb.State.Should().Be(CircuitBreakerState.Open);
        cb.CanExecute().Should().BeFalse();
        cb.LastTripReason.Should().Contain("exceeded threshold");
        cb.TotalTrippedCount.Should().Be(1);
    }

    [Fact]
    public void CircuitBreaker_WhileOpen_RejectsExecutionDuringCooldown()
    {
        // Arrange
        var testUrl = $"https://api.cooldown-host-{Guid.NewGuid():N}.com/webhook";
        var cb = _service.GetOrCreateCircuitBreaker(testUrl);
        cb.Trip("Manual test trip");

        // Assert
        cb.State.Should().Be(CircuitBreakerState.Open);
        cb.CanExecute().Should().BeFalse();
    }

    [Fact]
    public async Task CircuitBreaker_TransitionsToHalfOpen_AfterCooldownExpires()
    {
        // Arrange
        var testUrl = $"https://api.probe-host-{Guid.NewGuid():N}.com/webhook";
        var cb = _service.GetOrCreateCircuitBreaker(testUrl);
        cb.Trip("Temporary outage");
        cb.SetCooldownForTest(TimeSpan.FromMilliseconds(50));

        // Wait for cooldown to expire
        await Task.Delay(75);

        // Act
        var canExecute = cb.CanExecute();

        // Assert
        canExecute.Should().BeTrue("Half-Open state must allow a probe request");
        cb.State.Should().Be(CircuitBreakerState.HalfOpen);
    }

    [Fact]
    public void CircuitBreaker_HalfOpen_RecoversToClosedOnSuccessfulProbe()
    {
        // Arrange
        var testUrl = $"https://api.recover-host-{Guid.NewGuid():N}.com/webhook";
        var cb = _service.GetOrCreateCircuitBreaker(testUrl);
        cb.Trip("Temporary outage");
        cb.SetCooldownForTest(TimeSpan.FromMilliseconds(1));

        Thread.Sleep(5);
        cb.CanExecute(); // Transitions to HalfOpen
        cb.State.Should().Be(CircuitBreakerState.HalfOpen);

        // Act
        cb.RecordSuccess();

        // Assert
        cb.State.Should().Be(CircuitBreakerState.Closed);
        cb.ConsecutiveFailures.Should().Be(0);
        cb.ConsecutiveSuccesses.Should().Be(1);
    }

    [Fact]
    public void CircuitBreaker_HalfOpen_TripsBackToOpenOnFailedProbe()
    {
        // Arrange
        var testUrl = $"https://api.re-trip-host-{Guid.NewGuid():N}.com/webhook";
        var cb = _service.GetOrCreateCircuitBreaker(testUrl);
        cb.Trip("Outage");
        cb.SetCooldownForTest(TimeSpan.FromMilliseconds(1));

        Thread.Sleep(5);
        cb.CanExecute(); // Transitions to HalfOpen
        cb.State.Should().Be(CircuitBreakerState.HalfOpen);

        // Act
        cb.RecordFailure("Probe connection timed out");

        // Assert
        cb.State.Should().Be(CircuitBreakerState.Open);
        cb.LastTripReason.Should().Contain("Probe failed in Half-Open state");
        cb.TotalTrippedCount.Should().Be(2);
    }

    [Fact]
    public void CircuitBreaker_ManualResetAndTrip_AltersStateCorrectly()
    {
        // Arrange
        var testUrl = $"https://api.manual-ctl-{Guid.NewGuid():N}.com/webhook";

        // 1. Manual Trip
        var tripSnapshot = _service.TripCircuitBreaker(testUrl, "Emergency Maintenance Quarantine");
        tripSnapshot.State.Should().Be("Open");
        tripSnapshot.LastTripReason.Should().Be("Emergency Maintenance Quarantine");

        // 2. Manual Reset
        var resetSnapshot = _service.ResetCircuitBreaker(testUrl);
        resetSnapshot.State.Should().Be("Closed");
        resetSnapshot.ConsecutiveFailures.Should().Be(0);

        var cb = _service.GetOrCreateCircuitBreaker(testUrl);
        cb.CanExecute().Should().BeTrue();
    }

    [Fact]
    public void Subscription_CrudOperations_CreateReadUpdateDeleteAndToggle()
    {
        // 1. Create
        var createDto = new CreateMeshSubscriptionDto(
            Name: "Test Webhook Endpoint",
            TargetUrl: "https://hooks.example.com/events",
            Events: new List<string> { "invoice.created", "invoice.paid" },
            MaxRetries: 4,
            TimeoutSeconds: 15
        );

        var sub = _service.CreateSubscription(createDto);
        sub.Should().NotBeNull();
        sub.Name.Should().Be("Test Webhook Endpoint");
        sub.TargetUrl.Should().Be("https://hooks.example.com/events");
        sub.Secret.Should().StartWith("whsec_");
        sub.IsActive.Should().BeTrue();

        // 2. Read
        var fetched = _service.GetSubscriptionById(sub.Id);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("Test Webhook Endpoint");

        var list = _service.ListSubscriptions("Test Webhook", activeOnly: true);
        list.Should().Contain(s => s.Id == sub.Id);

        // 3. Update
        var updateDto = new UpdateMeshSubscriptionDto(
            Name: "Updated Endpoint Name",
            TimeoutSeconds: 20
        );
        var updated = _service.UpdateSubscription(sub.Id, updateDto);
        updated!.Name.Should().Be("Updated Endpoint Name");
        updated.TimeoutSeconds.Should().Be(20);

        // 4. Toggle
        var toggled = _service.ToggleActive(sub.Id);
        toggled!.IsActive.Should().BeFalse();

        // 5. Delete
        var deleted = _service.DeleteSubscription(sub.Id);
        deleted.Should().BeTrue();
        _service.GetSubscriptionById(sub.Id).Should().BeNull();
    }

    [Fact]
    public async Task DispatchPing_ComputesSignatureAndDeliversSuccessfully()
    {
        // Arrange - use pre-seeded Shopify subscription
        var allSubs = _service.ListSubscriptions();
        var shopify = allSubs.First(s => s.TargetUrl.Contains("shopify"));

        // Act
        var result = await _service.PingSubscriptionAsync(shopify.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.Signature.Should().NotBeNullOrWhiteSpace();
        result.Signature!.Length.Should().Be(64);
        result.Timestamp.Should().BeGreaterThan(0);
        result.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task DispatchEvent_WhenCircuitBreakerOpen_BlocksAndEnqueuesToDlq()
    {
        // Arrange - create a sub and trip its circuit breaker
        var sub = _service.CreateSubscription(new CreateMeshSubscriptionDto(
            Name: "Circuit Breaker Test Sub",
            TargetUrl: $"https://api.blocked-{Guid.NewGuid():N}.io/hook",
            Events: new List<string> { "order.cancelled" }
        ));

        _service.TripCircuitBreaker(sub.TargetUrl, "Upstream HTTP 503 Outage");

        // Act - dispatch event
        var results = await _service.DispatchEventAsync("order.cancelled", new { orderId = 123 }, sub.Id);

        // Assert
        results.Should().HaveCount(1);
        var res = results.First();
        res.BlockedByCircuitBreaker.Should().BeTrue();
        res.StatusCode.Should().Be(503);
        res.IsSuccess.Should().BeFalse();

        // Verify DLQ captured the blocked dispatch
        var dlqItems = _service.GetDlqItems(targetUrl: sub.TargetUrl);
        dlqItems.Should().NotBeEmpty();
        var dlq = dlqItems.First();
        dlq.QuarantineReason.Should().Contain("Circuit Breaker OPEN");
        dlq.Status.Should().Be(DlqStatus.Quarantined);
    }

    [Fact]
    public async Task Dlq_ReplaySingleItem_UpdatesStatusToReplayedOnSuccess()
    {
        // Arrange
        var sub = _service.ListSubscriptions().First(s => s.TargetUrl.Contains("shopify"));
        var dlqItem = _service.EnqueueDlq(
            sub.Id,
            sub.Name,
            sub.TargetUrl,
            "orders.created",
            "{\"orderId\":\"ORD-TEST-999\"}",
            500,
            "Initial connection reset",
            null,
            120,
            1,
            "Test Quarantine"
        );

        // Ensure circuit breaker is Closed
        _service.ResetCircuitBreaker(sub.TargetUrl);

        // Act
        var replayResult = await _service.ReplayDlqItemAsync(dlqItem.Id);

        // Assert
        replayResult.Success.Should().BeTrue();
        replayResult.StatusCode.Should().Be(200);

        var updatedItem = _service.GetDlqItemById(dlqItem.Id);
        updatedItem!.Status.Should().Be(DlqStatus.Replayed);
        updatedItem.ReplayedAtUtc.Should().NotBeNull();
        updatedItem.ReplayResult.Should().Contain("200");
    }

    [Fact]
    public async Task Dlq_BulkReplay_ProcessesMultipleItemsAndReportsOutcome()
    {
        // Arrange
        var sub = _service.ListSubscriptions().First(s => s.TargetUrl.Contains("shopify"));
        _service.ResetCircuitBreaker(sub.TargetUrl);

        var item1 = _service.EnqueueDlq(sub.Id, sub.Name, sub.TargetUrl, "event.one", "{}", 500, "err1", null, 50, 1, "Q1");
        var item2 = _service.EnqueueDlq(sub.Id, sub.Name, sub.TargetUrl, "event.two", "{}", 500, "err2", null, 60, 1, "Q2");

        // Act
        var bulkResult = await _service.ReplayBulkDlqAsync(new[] { item1.Id, item2.Id });

        // Assert
        bulkResult.TotalAttempted.Should().Be(2);
        bulkResult.SucceededCount.Should().Be(2);
        bulkResult.FailedCount.Should().Be(0);
        bulkResult.Results.Should().HaveCount(2);
    }

    [Fact]
    public void MeshTelemetry_CalculatesP50P95P99AndSuccessRate()
    {
        // Act
        var stats = _service.GetTelemetryStats();

        // Assert
        stats.Should().NotBeNull();
        stats.P50LatencyMs.Should().BeGreaterThan(0);
        stats.P95LatencyMs.Should().BeGreaterThanOrEqualTo(stats.P50LatencyMs);
        stats.P99LatencyMs.Should().BeGreaterThanOrEqualTo(stats.P95LatencyMs);
        stats.SuccessRatePercent.Should().BeInRange(0.0, 100.0);
        stats.TotalSubscriptionsCount.Should().BeGreaterThanOrEqualTo(5);
        stats.ActiveSubscriptionsCount.Should().BeGreaterThanOrEqualTo(1);
    }
}
