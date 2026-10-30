using FluentAssertions;
using SGrowing.Api.Modules.Security;
using Xunit;

namespace SGrowing.Tests.Security;

public class DistributedRateLimiterServiceTests
{
    private readonly DistributedRateLimiterService _limiter;
    private readonly DateTimeOffset _baseTime;

    public DistributedRateLimiterServiceTests()
    {
        _limiter = new DistributedRateLimiterService();
        // Fixed reference time at minute boundary (e.g. 2026-10-30 12:00:00 UTC)
        _baseTime = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void SlidingWindowFormula_CalculatesExpectedWeightedCount_BasedOnTimeIntoWindow()
    {
        // Formula: Count = Count_current + Count_previous * (1.0 - (TimeIntoCurrentWindow / WindowDuration))
        var clientKey = $"test_formula_{Guid.NewGuid()}";
        var request = new RateLimitRequest(ClientIp: clientKey, Tier: DistributedRateLimiterService.ProTier);

        // Window 1: Fire 40 requests at t = 10s into window 1
        var tWindow1 = _baseTime.AddSeconds(10);
        for (int i = 0; i < 40; i++)
        {
            var res = _limiter.CheckRateLimit(request, tWindow1);
            res.IsAllowed.Should().BeTrue();
        }

        // Advance to Window 2: t = 30s into window 2 (60s window duration)
        // Elapsed in window 2: 30s -> Weight = 1.0 - (30 / 60) = 0.50
        // Previous count = 40.0 -> Weighted contribution from previous window = 40 * 0.5 = 20.0
        var tWindow2 = _baseTime.AddSeconds(60 + 30);

        // Fire 1 request in window 2 (CurrentCount becomes 1)
        var decision = _limiter.CheckRateLimit(request, tWindow2);

        decision.IsAllowed.Should().BeTrue();
        // CurrentCount (1) + PreviousCount (40) * (1 - 30/60) = 1 + 20 = 21.0
        decision.CurrentWeightedCount.Should().BeApproximately(21.0, 0.01);
    }

    [Fact]
    public void TierQuotaEnforcement_FreeTier_AllowsUpToLimitPlusBurst()
    {
        // FreeTier: Limit 60, Burst 15 => EffectiveLimit = 75
        var clientKey = $"free_client_{Guid.NewGuid()}";
        var request = new RateLimitRequest(ClientIp: clientKey, Tier: DistributedRateLimiterService.FreeTier);
        var t = _baseTime.AddSeconds(5);

        // Requests 1 through 75 should all be allowed
        for (int i = 1; i <= 75; i++)
        {
            var result = _limiter.CheckRateLimit(request, t);
            result.IsAllowed.Should().BeTrue($"Request #{i} should be permitted within quota + burst");
            result.StatusCode.Should().Be(200);
            result.Remaining.Should().Be(75 - i);
        }

        // Request 76 exceeds effective limit (75) -> 429 Too Many Requests
        var throttledResult = _limiter.CheckRateLimit(request, t);
        throttledResult.IsAllowed.Should().BeFalse();
        throttledResult.StatusCode.Should().Be(429);
        throttledResult.Remaining.Should().Be(0);
        throttledResult.Headers.Should().ContainKey("Retry-After");
        throttledResult.Headers["RateLimit-Remaining"].Should().Be("0");
    }

    [Fact]
    public void TierQuotaEnforcement_ProTier_AllowsHigherThroughput()
    {
        // ProTier: 600 req/min, Burst 60 => EffectiveLimit = 660
        var clientKey = $"pro_client_{Guid.NewGuid()}";
        var request = new RateLimitRequest(ClientIp: clientKey, Tier: DistributedRateLimiterService.ProTier);
        var t = _baseTime.AddSeconds(12);

        var first = _limiter.CheckRateLimit(request, t);
        first.IsAllowed.Should().BeTrue();
        first.Limit.Should().Be(660);
        first.Remaining.Should().Be(659);
    }

    [Fact]
    public void BurstCapacityAllowance_PermitsRequestsExceedingBaseLimit_BeforeRejection()
    {
        // FreeTier base limit = 60, burst = 15.
        // Requests between 61 and 75 are allowed through the burst capacity buffer.
        var clientKey = $"burst_test_{Guid.NewGuid()}";
        var request = new RateLimitRequest(ClientIp: clientKey, Tier: DistributedRateLimiterService.FreeTier);
        var t = _baseTime.AddSeconds(20);

        // Consume all base 60 requests
        for (int i = 0; i < 60; i++)
        {
            _limiter.CheckRateLimit(request, t);
        }

        // Request 61 is in the burst buffer (beyond 60, but within 75)
        var burstReq61 = _limiter.CheckRateLimit(request, t);
        burstReq61.IsAllowed.Should().BeTrue("Request #61 should be accepted via burst allowance");
        burstReq61.StatusCode.Should().Be(200);
        burstReq61.Remaining.Should().Be(14); // 75 - 61 = 14

        // Request 75 is the final burst request
        for (int i = 62; i < 75; i++)
        {
            _limiter.CheckRateLimit(request, t);
        }
        var burstReq75 = _limiter.CheckRateLimit(request, t);
        burstReq75.IsAllowed.Should().BeTrue();
        burstReq75.Remaining.Should().Be(0);

        // Request 76 is rejected
        var burstReq76 = _limiter.CheckRateLimit(request, t);
        burstReq76.IsAllowed.Should().BeFalse();
        burstReq76.StatusCode.Should().Be(429);
    }

    [Fact]
    public void HeadersGeneration_ReturnsRfcCompliantHeaders_OnSuccessAnd429()
    {
        var clientKey = $"rfc_headers_{Guid.NewGuid()}";
        var request = new RateLimitRequest(ClientIp: clientKey, Tier: DistributedRateLimiterService.FreeTier);
        // t = 15 seconds into minute window -> reset is 45 seconds
        var t = _baseTime.AddSeconds(15);

        // 1. Success headers
        var ok = _limiter.CheckRateLimit(request, t);
        ok.Headers.Should().ContainKey("RateLimit-Limit");
        ok.Headers["RateLimit-Limit"].Should().Be("75");
        ok.Headers.Should().ContainKey("RateLimit-Remaining");
        ok.Headers["RateLimit-Remaining"].Should().Be("74");
        ok.Headers.Should().ContainKey("RateLimit-Reset");
        ok.Headers["RateLimit-Reset"].Should().Be("45");
        ok.Headers.Should().NotContainKey("Retry-After");

        // 2. Exhaust quota and verify 429 headers
        for (int i = 0; i < 74; i++)
        {
            _limiter.CheckRateLimit(request, t);
        }

        var throttled = _limiter.CheckRateLimit(request, t);
        throttled.StatusCode.Should().Be(429);
        throttled.Headers.Should().ContainKey("RateLimit-Limit");
        throttled.Headers.Should().ContainKey("RateLimit-Remaining");
        throttled.Headers["RateLimit-Remaining"].Should().Be("0");
        throttled.Headers.Should().ContainKey("RateLimit-Reset");
        throttled.Headers.Should().ContainKey("Retry-After");
        throttled.Headers["Retry-After"].Should().Be("45");
    }

    [Fact]
    public void SubSecondAccuracy_SmoothWindowTransition_DecaysPreviousWindowWeight()
    {
        var clientKey = $"decay_test_{Guid.NewGuid()}";
        var request = new RateLimitRequest(ClientIp: clientKey, Tier: DistributedRateLimiterService.EnterpriseTier);

        // Window 1: 100 requests at t = 30s
        for (int i = 0; i < 100; i++)
        {
            _limiter.CheckRateLimit(request, _baseTime.AddSeconds(30));
        }

        // Window 2 at 15s (75% weight remaining from prev window): prev contribution = 75
        var decAt15 = _limiter.CheckRateLimit(request, _baseTime.AddSeconds(60 + 15));
        decAt15.CurrentWeightedCount.Should().BeApproximately(1.0 + 75.0, 0.5);

        // Clean up bucket for fresh isolation
        _limiter.ResetKey(clientKey);

        // Re-seed 100 in Window 1
        for (int i = 0; i < 100; i++)
        {
            _limiter.CheckRateLimit(request, _baseTime.AddSeconds(30));
        }

        // Window 2 at 45s (25% weight remaining): prev contribution = 25
        var decAt45 = _limiter.CheckRateLimit(request, _baseTime.AddSeconds(60 + 45));
        decAt45.CurrentWeightedCount.Should().BeApproximately(1.0 + 25.0, 0.5);
    }

    [Fact]
    public void OffenderEscalation_5ConsecutiveViolations_TriggersLevel1Penalty_5MinuteBan()
    {
        var attackerIp = $"bot_{Guid.NewGuid()}";
        var request = new RateLimitRequest(ClientIp: attackerIp, Tier: DistributedRateLimiterService.FreeTier);
        var t = _baseTime.AddSeconds(10);

        // Exhaust quota (75 requests)
        for (int i = 0; i < 75; i++)
        {
            _limiter.CheckRateLimit(request, t);
        }

        // Fire 4 consecutive 429 violations (not yet banned)
        for (int v = 1; v <= 4; v++)
        {
            var res = _limiter.CheckRateLimit(request, t.AddSeconds(v));
            res.StatusCode.Should().Be(429);
            res.BanLevel.Should().Be(0);
        }

        // 5th consecutive violation within 1 minute triggers Level 1 ban (5 minutes)
        var banDecision = _limiter.CheckRateLimit(request, t.AddSeconds(5));
        banDecision.StatusCode.Should().Be(429);
        banDecision.BanLevel.Should().Be(1);
        banDecision.Reason.Should().Contain("Level 1");
        banDecision.RetryAfterSeconds.Should().Be(300); // 5 minutes = 300 seconds
        banDecision.Headers["Retry-After"].Should().Be("300");

        // Verify key is now present in Blacklist
        var blacklist = _limiter.GetBlacklist(t.AddSeconds(5));
        blacklist.Should().Contain(b => b.Key == attackerIp && b.BanLevel == 1);
    }

    [Fact]
    public void OffenderEscalation_SubsequentViolations_EscalatesToLevel2AndLevel3Penalties()
    {
        var recidivistIp = $"recidivist_{Guid.NewGuid()}";
        var request = new RateLimitRequest(ClientIp: recidivistIp, Tier: DistributedRateLimiterService.FreeTier);
        var t = _baseTime;

        // Exhaust quota
        for (int i = 0; i < 75; i++)
        {
            _limiter.CheckRateLimit(request, t);
        }

        // 5 violations -> triggers Level 1 ban (5m)
        for (int v = 1; v <= 5; v++)
        {
            _limiter.CheckRateLimit(request, t.AddSeconds(v));
        }

        // Fast forward past the 5-minute ban (e.g. 6 minutes later)
        var tAfterBan1 = t.AddMinutes(6);

        // Exhaust quota again
        for (int i = 0; i < 75; i++)
        {
            _limiter.CheckRateLimit(request, tAfterBan1);
        }

        // 5 more violations -> triggers Level 2 ban (15m)
        RateLimitDecision? level2Decision = null;
        for (int v = 1; v <= 5; v++)
        {
            level2Decision = _limiter.CheckRateLimit(request, tAfterBan1.AddSeconds(v));
        }

        level2Decision.Should().NotBeNull();
        level2Decision!.BanLevel.Should().Be(2);
        level2Decision.RetryAfterSeconds.Should().Be(900); // 15 minutes = 900s
    }

    [Fact]
    public void BlacklistOverride_RejectsImmediately_With429AndRetryAfter()
    {
        // Pre-seeded attacker IP: 198.51.100.42
        var request = new RateLimitRequest(ClientIp: "198.51.100.42");
        var decision = _limiter.CheckRateLimit(request, _baseTime);

        decision.IsAllowed.Should().BeFalse();
        decision.StatusCode.Should().Be(429);
        decision.Tier.Should().Be("Blacklisted");
        decision.Reason.Should().Contain("anti-abuse guard");
        decision.Headers.Should().ContainKey("Retry-After");
        decision.Headers["RateLimit-Remaining"].Should().Be("0");
    }

    [Fact]
    public void WhitelistOverride_AlwaysAllowsRequests_BypassingRateLimits()
    {
        // Pre-seeded localhost proxy: 127.0.0.1
        var request = new RateLimitRequest(ClientIp: "127.0.0.1", Tier: DistributedRateLimiterService.FreeTier);
        var t = _baseTime.AddSeconds(5);

        // Hammer with 200 requests (far beyond FreeTier limit of 75)
        for (int i = 0; i < 200; i++)
        {
            var res = _limiter.CheckRateLimit(request, t);
            res.IsAllowed.Should().BeTrue();
            res.StatusCode.Should().Be(200);
            res.Tier.Should().Be("Whitelisted");
            res.Remaining.Should().Be(999999);
        }
    }

    [Fact]
    public void ManualBlacklistAndUnban_BlocksAndRestoresAccess()
    {
        var targetIp = "192.168.1.105";
        var req = new RateLimitRequest(ClientIp: targetIp, Tier: DistributedRateLimiterService.FreeTier);

        // Initially allowed
        var initial = _limiter.CheckRateLimit(req, _baseTime);
        initial.IsAllowed.Should().BeTrue();

        // 1. Manually blacklist key with reference time
        var ban = _limiter.BlacklistKey(targetIp, "IP", "Suspicious scanning activity", TimeSpan.FromMinutes(30), banLevel: 2, simulatedNow: _baseTime);
        ban.Key.Should().Be(targetIp);
        ban.BanLevel.Should().Be(2);

        // 2. Request should now be blocked
        var blocked = _limiter.CheckRateLimit(req, _baseTime);
        blocked.IsAllowed.Should().BeFalse();
        blocked.StatusCode.Should().Be(429);
        blocked.Reason.Should().Contain("Suspicious scanning activity");

        // 3. Unban key
        var removed = _limiter.RemoveFromBlacklist(targetIp);
        removed.Should().BeTrue();

        // 4. Request should now be permitted again
        var restored = _limiter.CheckRateLimit(req, _baseTime);
        restored.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void CustomOverridePolicy_PerIpOrApiKey_OverridesDefaultTier()
    {
        var partnerApiKey = "ak_vip_enterprise_partner_441";
        var customPolicy = new RateLimitPolicy(
            TierName: "PartnerVIP",
            RequestsPerMinute: 12000,
            BurstCapacity: 1000,
            WindowSeconds: 60,
            Description: "Dedicated high-volume integration partner"
        );

        _limiter.SetCustomOverride(partnerApiKey, "ApiKey", customPolicy);

        var req = new RateLimitRequest(ClientIp: "203.0.113.1", ApiKey: partnerApiKey);
        var decision = _limiter.CheckRateLimit(req, _baseTime);

        decision.IsAllowed.Should().BeTrue();
        decision.Tier.Should().Be("PartnerVIP");
        decision.Limit.Should().Be(13000); // 12,000 + 1,000 burst
        decision.Remaining.Should().Be(12999);

        // Clean up
        _limiter.RemoveCustomOverride(partnerApiKey);
    }

    [Fact]
    public void RouteTypeResolution_ResolvesAiInferenceAndWebhookTiersCorrectly()
    {
        // 1. AI Inference route -> AiInferenceTier: 30 req/min + 5 burst = 35 effective limit
        var aiReq = new RateLimitRequest(ClientIp: $"ai_user_{Guid.NewGuid()}", RouteType: "AiInference");
        var aiDecision = _limiter.CheckRateLimit(aiReq, _baseTime);
        aiDecision.Tier.Should().Be(DistributedRateLimiterService.AiInferenceTier);
        aiDecision.Limit.Should().Be(35);

        // 2. Webhook route -> PublicWebhookTier: 120 req/min + 30 burst = 150 effective limit
        var hookReq = new RateLimitRequest(ClientIp: $"hook_sender_{Guid.NewGuid()}", RouteType: "Webhook");
        var hookDecision = _limiter.CheckRateLimit(hookReq, _baseTime);
        hookDecision.Tier.Should().Be(DistributedRateLimiterService.PublicWebhookTier);
        hookDecision.Limit.Should().Be(150);
    }

    [Fact]
    public void TelemetryTracking_RecordsRequestsThrottledAndRejectionRate()
    {
        var initialTelemetry = _limiter.GetTelemetry(_baseTime);
        initialTelemetry.TotalRequestsAnalyzed.Should().BeGreaterThan(0);
        initialTelemetry.TotalThrottledRequests.Should().BeGreaterThan(0);
        initialTelemetry.RejectionRatePercentage.Should().BeGreaterThan(0.0);
        initialTelemetry.TopThrottledClients.Should().NotBeEmpty();

        // Cause a 429
        var victim = $"victim_telemetry_{Guid.NewGuid()}";
        var req = new RateLimitRequest(ClientIp: victim, Tier: DistributedRateLimiterService.FreeTier);
        for (int i = 0; i < 76; i++)
        {
            _limiter.CheckRateLimit(req, _baseTime);
        }

        var updated = _limiter.GetTelemetry(_baseTime);
        updated.TotalRequestsAnalyzed.Should().Be(initialTelemetry.TotalRequestsAnalyzed + 76);
        updated.TotalThrottledRequests.Should().Be(initialTelemetry.TotalThrottledRequests + 1);
        updated.TopThrottledClients.Should().Contain(t => t.Key == victim);
    }

    [Fact]
    public void ResetKey_ClearsCountersAndViolations()
    {
        var testKey = $"reset_target_{Guid.NewGuid()}";
        var req = new RateLimitRequest(ClientIp: testKey, Tier: DistributedRateLimiterService.FreeTier);

        // Exhaust quota
        for (int i = 0; i < 75; i++)
        {
            _limiter.CheckRateLimit(req, _baseTime);
        }

        var blocked = _limiter.CheckRateLimit(req, _baseTime);
        blocked.IsAllowed.Should().BeFalse();

        // Reset key
        var didReset = _limiter.ResetKey(testKey);
        didReset.Should().BeTrue();

        // Immediate next request should succeed
        var unblocked = _limiter.CheckRateLimit(req, _baseTime);
        unblocked.IsAllowed.Should().BeTrue();
        unblocked.Remaining.Should().Be(74);
    }

    [Fact]
    public async Task ConcurrentRequests_ThreadSafety_DoesNotCorruptCountersOrState()
    {
        var sharedKey = $"concurrency_test_{Guid.NewGuid()}";
        var request = new RateLimitRequest(ClientIp: sharedKey, Tier: DistributedRateLimiterService.EnterpriseTier);
        var t = _baseTime;

        // EnterpriseTier: limit 6000, burst 300 = 6300.
        // Send 100 parallel requests concurrently.
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => _limiter.CheckRateLimit(request, t)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(100);
        results.All(r => r.IsAllowed).Should().BeTrue();
        // Since each took cost 1, the remaining count on the last request should be exactly 6300 - 100 = 6200
        var finalCheck = _limiter.CheckRateLimit(request, t);
        finalCheck.Remaining.Should().Be(6300 - 101);
    }
}
