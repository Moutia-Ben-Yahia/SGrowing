using System.Text.Json;
using FluentAssertions;
using SGrowing.Api.Modules.Security;
using Xunit;

namespace SGrowing.Tests.Security;

/// <summary>
/// Unit and concurrency test suite for Distributed Multi-Tenant Idempotency Service.
/// </summary>
public class DistributedIdempotencyServiceTests
{
    private readonly DistributedIdempotencyService _service;
    private readonly DateTimeOffset _baseTime;
    private const string DefaultTenant = "tenant-test-corp";

    public DistributedIdempotencyServiceTests()
    {
        _service = new DistributedIdempotencyService();
        _baseTime = new DateTimeOffset(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public void ComputeFingerprint_Deterministic_GeneratesIdenticalHashForIdenticalRequests()
    {
        var body = "{\"amount\": 1500, \"currency\": \"USD\"}";
        var hash1 = _service.ComputeFingerprint(DefaultTenant, "POST", "/api/v1/payments", body);
        var hash2 = _service.ComputeFingerprint(DefaultTenant, "POST", "/api/v1/payments", body);

        hash1.Should().NotBeNullOrWhiteSpace();
        hash1.Should().Be(hash2);
        hash1.Length.Should().Be(64); // SHA-256 hex string length
    }

    [Fact]
    public void ComputeFingerprint_DifferentTenants_ProducesDifferentFingerprints()
    {
        var body = "{\"amount\": 1500, \"currency\": \"USD\"}";
        var hash1 = _service.ComputeFingerprint("tenant-alpha", "POST", "/api/v1/payments", body);
        var hash2 = _service.ComputeFingerprint("tenant-beta", "POST", "/api/v1/payments", body);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeFingerprint_DifferentHttpMethods_ProducesDifferentFingerprints()
    {
        var body = "{\"action\": \"sync\"}";
        var hashPost = _service.ComputeFingerprint(DefaultTenant, "POST", "/api/v1/sync", body);
        var hashPut = _service.ComputeFingerprint(DefaultTenant, "PUT", "/api/v1/sync", body);

        hashPost.Should().NotBe(hashPut);
    }

    [Fact]
    public void ComputeFingerprint_DifferentPaths_ProducesDifferentFingerprints()
    {
        var body = "{\"action\": \"charge\"}";
        var hash1 = _service.ComputeFingerprint(DefaultTenant, "POST", "/api/v1/charges", body);
        var hash2 = _service.ComputeFingerprint(DefaultTenant, "POST", "/api/v1/refunds", body);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeFingerprint_DifferentBodies_ProducesDifferentFingerprints()
    {
        var hash1 = _service.ComputeFingerprint(DefaultTenant, "POST", "/api/v1/orders", "{\"items\": [1, 2]}");
        var hash2 = _service.ComputeFingerprint(DefaultTenant, "POST", "/api/v1/orders", "{\"items\": [1, 3]}");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeFingerprint_NullAndEmptyBodies_TreatedEquivalently()
    {
        var hashNull = _service.ComputeFingerprint(DefaultTenant, "GET", "/api/v1/resource", null);
        var hashEmpty = _service.ComputeFingerprint(DefaultTenant, "GET", "/api/v1/resource", "");
        var hashWhitespace = _service.ComputeFingerprint(DefaultTenant, "GET", "/api/v1/resource", "   ");

        hashNull.Should().Be(hashEmpty);
        hashEmpty.Should().Be(hashWhitespace);
    }

    [Fact]
    public async Task TryAcquireLockAsync_InitialRequest_AcquiresLockWithInProgressStatus()
    {
        var key = $"key_{Guid.NewGuid():N}";
        var body = "{\"product\": \"license_annual\", \"seats\": 10}";

        var result = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/licenses", body, _baseTime);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(IdempotencyStatus.InProgress);
        result.StatusCode.Should().Be(200);
        result.ReplayDetected.Should().BeFalse();
        result.Fingerprint.Should().NotBeNullOrWhiteSpace();
        result.CreatedAt.Should().Be(_baseTime);
        result.ExpiresAt.Should().Be(_baseTime.AddSeconds(60));
    }

    [Fact]
    public async Task TryAcquireLockAsync_InitialRequest_Sets60SecondExpirationTtl()
    {
        var key = $"key_ttl_{Guid.NewGuid():N}";
        var result = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/checkout", "{}", _baseTime);

        result.ExpiresAt.Should().HaveValue();
        result.ExpiresAt!.Value.Subtract(_baseTime).TotalSeconds.Should().Be(60);
    }

    [Fact]
    public async Task TryAcquireLockAsync_ConcurrentInFlightRequest_ReturnsConflict409()
    {
        var key = $"key_conflict_{Guid.NewGuid():N}";
        var body = "{\"amount\": 2500}";

        // Thread 1 acquires lock at t = 0s
        var first = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/charges", body, _baseTime);
        first.Success.Should().BeTrue();

        // Thread 2 attempts same key at t = 15s (before 60s TTL expires)
        var second = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/charges", body, _baseTime.AddSeconds(15));

        second.Success.Should().BeFalse();
        second.Status.Should().Be(IdempotencyStatus.InProgress);
        second.StatusCode.Should().Be(409);
        second.Message.Should().Contain("in-flight request detected");
        second.ReplayDetected.Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquireLockAsync_ExpiredInProgressLock_AllowsReAcquisitionWithNewTtl()
    {
        var key = $"key_expired_lock_{Guid.NewGuid():N}";
        var body = "{\"task\": \"generate_report\"}";

        // Initial lock at t = 0s (expires at t = 60s)
        var first = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/reports", body, _baseTime);
        first.Success.Should().BeTrue();

        // Second attempt at t = 61s (lock has expired without completion)
        var second = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/reports", body, _baseTime.AddSeconds(61));

        second.Success.Should().BeTrue();
        second.Status.Should().Be(IdempotencyStatus.InProgress);
        second.StatusCode.Should().Be(200);
        second.CreatedAt.Should().Be(_baseTime.AddSeconds(61));
        second.ExpiresAt.Should().Be(_baseTime.AddSeconds(121));
    }

    [Fact]
    public async Task RecordCompletedResponseAsync_StoresResponseAndSetsCompletedStatus()
    {
        var key = $"key_record_{Guid.NewGuid():N}";
        var reqBody = "{\"amount\": 9900}";
        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/pay", reqBody, _baseTime);

        var headers = new Dictionary<string, string> { ["X-Invoice-Id"] = "INV-2026-99" };
        var respBody = "{\"id\": \"ch_9921\", \"status\": \"paid\"}";

        var recorded = await _service.RecordCompletedResponseAsync(DefaultTenant, key, 201, headers, respBody, _baseTime.AddSeconds(5));
        recorded.Should().BeTrue();

        var records = await _service.GetRecordsAsync(DefaultTenant);
        var match = records.FirstOrDefault(r => r.Key == key);
        match.Should().NotBeNull();
        match!.Status.Should().Be(IdempotencyStatus.Completed);
        match.ResponseStatusCode.Should().Be(201);
        match.ResponseBody.Should().Be(respBody);
        match.ResponseHeaders.Should().ContainKey("X-Invoice-Id").WhoseValue.Should().Be("INV-2026-99");
    }

    [Fact]
    public async Task RecordCompletedResponseAsync_ExtendsTtlTo24Hours()
    {
        var key = $"key_24h_{Guid.NewGuid():N}";
        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/pay", "{}", _baseTime);
        await _service.RecordCompletedResponseAsync(DefaultTenant, key, 200, null, "{}", _baseTime.AddSeconds(2));

        var records = await _service.GetRecordsAsync(DefaultTenant);
        var match = records.First(r => r.Key == key);
        match.LockTtlSeconds.Should().Be(86400);
        match.ExpiresAt.Subtract(_baseTime.AddSeconds(2)).TotalHours.Should().BeApproximately(24.0, 0.01);
    }

    [Fact]
    public async Task TryAcquireLockAsync_CompletedRequestMatchingPayload_ReturnsDeterministicReplay()
    {
        var key = $"key_replay_{Guid.NewGuid():N}";
        var reqBody = "{\"action\": \"credit_wallet\", \"amount\": 500}";
        var respBody = "{\"wallet_balance\": 1500, \"credited\": 500}";
        var headers = new Dictionary<string, string> { ["X-Ledger-Seq"] = "88991" };

        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/wallets", reqBody, _baseTime);
        await _service.RecordCompletedResponseAsync(DefaultTenant, key, 200, headers, respBody, _baseTime.AddSeconds(1));

        // Replay attempt at t = 5 minutes later with identical payload
        var replay = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/wallets", reqBody, _baseTime.AddMinutes(5));

        replay.Success.Should().BeTrue();
        replay.ReplayDetected.Should().BeTrue();
        replay.Status.Should().Be(IdempotencyStatus.Completed);
        replay.StatusCode.Should().Be(200);
        replay.CachedStatusCode.Should().Be(200);
        replay.CachedResponseBody.Should().Be(respBody);
        replay.CachedHeaders.Should().ContainKey("X-Ledger-Seq").WhoseValue.Should().Be("88991");
        replay.ReplayCount.Should().Be(1);
    }

    [Fact]
    public async Task TryAcquireLockAsync_ReplayRequest_IncrementsReplayCountAndUpdatesLastReplayed()
    {
        var key = $"key_replay_count_{Guid.NewGuid():N}";
        var body = "{\"ping\": \"pong\"}";

        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/ping", body, _baseTime);
        await _service.RecordCompletedResponseAsync(DefaultTenant, key, 200, null, "{\"pong\": true}", _baseTime.AddSeconds(1));

        // Replay 1
        var rep1 = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/ping", body, _baseTime.AddSeconds(10));
        rep1.ReplayCount.Should().Be(1);

        // Replay 2
        var rep2 = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/ping", body, _baseTime.AddSeconds(20));
        rep2.ReplayCount.Should().Be(2);

        var records = await _service.GetRecordsAsync(DefaultTenant);
        var entry = records.First(r => r.Key == key);
        entry.ReplayCount.Should().Be(2);
        entry.LastReplayedAt.Should().Be(_baseTime.AddSeconds(20));
    }

    [Fact]
    public async Task TryAcquireLockAsync_CompletedRequestAlteredPayload_ReturnsPayloadMismatch422()
    {
        var key = $"key_tamper_{Guid.NewGuid():N}";
        var originalBody = "{\"amount\": 100, \"recipient\": \"user_10\"}";
        var alteredBody = "{\"amount\": 10000, \"recipient\": \"user_99\"}"; // Tampered!

        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/transfers", originalBody, _baseTime);
        await _service.RecordCompletedResponseAsync(DefaultTenant, key, 200, null, "{\"status\": \"sent\"}", _baseTime.AddSeconds(1));

        // Attempt reuse of the exact same key with a different body
        var tamperedResult = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/transfers", alteredBody, _baseTime.AddMinutes(2));

        tamperedResult.Success.Should().BeFalse();
        tamperedResult.Status.Should().Be(IdempotencyStatus.PayloadMismatch);
        tamperedResult.StatusCode.Should().Be(422);
        tamperedResult.ReplayDetected.Should().BeFalse();
        tamperedResult.Message.Should().Contain("Reuse with altered parameters is prohibited");
    }

    [Fact]
    public async Task TryAcquireLockAsync_PayloadMismatch_IncrementsMismatchesBlockedMetric()
    {
        await _service.ResetAllAsync();
        var key = $"key_metric_mismatch_{Guid.NewGuid():N}";

        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/action", "{\"a\": 1}", _baseTime);
        await _service.RecordCompletedResponseAsync(DefaultTenant, key, 200, null, "{\"ok\": true}", _baseTime);

        // Fire 2 mismatches
        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/action", "{\"a\": 2}", _baseTime.AddSeconds(5));
        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/action", "{\"a\": 3}", _baseTime.AddSeconds(10));

        var metrics = await _service.GetMetricsAsync(DefaultTenant);
        metrics.MismatchesBlocked.Should().Be(2);
    }

    [Fact]
    public async Task TryAcquireLockAsync_CompletedRequestExpired24Hours_AllowsReAcquisition()
    {
        var key = $"key_expired_24h_{Guid.NewGuid():N}";
        var body = "{\"batch\": \"sync_run\"}";

        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/batch", body, _baseTime);
        await _service.RecordCompletedResponseAsync(DefaultTenant, key, 200, null, "{\"synced\": 10}", _baseTime);

        // Advance 25 hours later (24-hour TTL expired)
        var tExpired = _baseTime.AddHours(25);
        var freshLock = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/batch", body, tExpired);

        freshLock.Success.Should().BeTrue();
        freshLock.Status.Should().Be(IdempotencyStatus.InProgress);
        freshLock.StatusCode.Should().Be(200);
        freshLock.ReplayDetected.Should().BeFalse();
        freshLock.CreatedAt.Should().Be(tExpired);
    }

    [Fact]
    public async Task RecordFailedResponseAsync_MarksStatusFailed_AllowsSubsequentRetry()
    {
        var key = $"key_failed_{Guid.NewGuid():N}";
        var body = "{\"action\": \"call_upstream\"}";

        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/upstream", body, _baseTime);

        var markedFailed = await _service.RecordFailedResponseAsync(DefaultTenant, key, "Upstream gateway connection timeout (504)", 504, _baseTime.AddSeconds(2));
        markedFailed.Should().BeTrue();

        var records = await _service.GetRecordsAsync(DefaultTenant);
        var entry = records.First(r => r.Key == key);
        entry.Status.Should().Be(IdempotencyStatus.Failed);
        entry.ResponseStatusCode.Should().Be(504);
        entry.ErrorMessage.Should().Contain("Upstream gateway");

        // Subsequent retry with same key is permitted
        var retry = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/upstream", body, _baseTime.AddSeconds(10));
        retry.Success.Should().BeTrue();
        retry.Status.Should().Be(IdempotencyStatus.InProgress);
    }

    [Fact]
    public async Task InvalidateKeyAsync_ExistingKey_RemovesFromLedgerAndReturnsTrue()
    {
        var key = $"key_to_purge_{Guid.NewGuid():N}";
        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/items", "{}", _baseTime);

        var removed = await _service.InvalidateKeyAsync(DefaultTenant, key);
        removed.Should().BeTrue();

        var records = await _service.GetRecordsAsync(DefaultTenant);
        records.Should().NotContain(r => r.Key == key);

        // Can acquire again as fresh key
        var fresh = await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/items", "{}", _baseTime);
        fresh.Success.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidateKeyAsync_NonExistentKey_ReturnsFalse()
    {
        var removed = await _service.InvalidateKeyAsync(DefaultTenant, "non_existent_key_9999");
        removed.Should().BeFalse();
    }

    [Fact]
    public async Task GetMetricsAsync_TracksReplaysConflictsAndMismatchesAccurately()
    {
        await _service.ResetAllAsync();

        var keyA = "key_kpi_a";
        var keyB = "key_kpi_b";

        // Initial locks
        await _service.TryAcquireLockAsync(DefaultTenant, keyA, "POST", "/api/v1/a", "{\"val\": 1}", _baseTime);
        await _service.TryAcquireLockAsync(DefaultTenant, keyB, "POST", "/api/v1/b", "{\"val\": 2}", _baseTime);

        // Conflict on keyA while in-flight
        await _service.TryAcquireLockAsync(DefaultTenant, keyA, "POST", "/api/v1/a", "{\"val\": 1}", _baseTime.AddSeconds(1));

        // Complete keyA
        await _service.RecordCompletedResponseAsync(DefaultTenant, keyA, 200, null, "{\"ok\": true}", _baseTime.AddSeconds(2));

        // Replay on keyA
        await _service.TryAcquireLockAsync(DefaultTenant, keyA, "POST", "/api/v1/a", "{\"val\": 1}", _baseTime.AddSeconds(5));

        // Mismatch on keyA
        await _service.TryAcquireLockAsync(DefaultTenant, keyA, "POST", "/api/v1/a", "{\"val\": 999}", _baseTime.AddSeconds(6));

        var metrics = await _service.GetMetricsAsync(DefaultTenant);
        metrics.TotalTrackedKeys.Should().Be(2);
        metrics.ActiveLocksCount.Should().Be(1); // keyB is still InProgress
        metrics.CompletedCount.Should().Be(1);   // keyA is Completed
        metrics.ConflictsDetected.Should().Be(1);
        metrics.ReplayHits.Should().Be(1);
        metrics.MismatchesBlocked.Should().Be(1);
    }

    [Fact]
    public async Task GetMetricsAsync_CalculatesReplayHitRatePercentageCorrectly()
    {
        await _service.ResetAllAsync();

        var key = "key_rate_test";
        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/test", "{}", _baseTime);
        await _service.RecordCompletedResponseAsync(DefaultTenant, key, 200, null, "{}", _baseTime);

        // 3 replay hits
        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/test", "{}", _baseTime.AddSeconds(1));
        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/test", "{}", _baseTime.AddSeconds(2));
        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/test", "{}", _baseTime.AddSeconds(3));

        var metrics = await _service.GetMetricsAsync(DefaultTenant);
        metrics.ReplayHits.Should().Be(3);
        metrics.ReplayHitRatePct.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task GetRecordsAsync_SupportsTenantFilterAndPagination()
    {
        var tenantX = $"tenant_x_{Guid.NewGuid():N}";
        var tenantY = $"tenant_y_{Guid.NewGuid():N}";

        for (int i = 0; i < 5; i++)
        {
            await _service.TryAcquireLockAsync(tenantX, $"key_x_{i}", "POST", "/api/v1/x", "{}", _baseTime.AddMinutes(i));
        }
        for (int i = 0; i < 3; i++)
        {
            await _service.TryAcquireLockAsync(tenantY, $"key_y_{i}", "POST", "/api/v1/y", "{}", _baseTime.AddMinutes(i));
        }

        var xRecords = await _service.GetRecordsAsync(tenantX, limit: 10);
        xRecords.Should().HaveCount(5);
        xRecords.Should().OnlyContain(r => r.TenantId == tenantX);

        var paged = await _service.GetRecordsAsync(tenantX, limit: 2, offset: 1);
        paged.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecordsAsync_SupportsStatusFilter()
    {
        var tenant = $"tenant_filter_{Guid.NewGuid():N}";
        var key1 = "key_in_progress";
        var key2 = "key_completed";

        await _service.TryAcquireLockAsync(tenant, key1, "POST", "/api/v1/res1", "{}", _baseTime);
        await _service.TryAcquireLockAsync(tenant, key2, "POST", "/api/v1/res2", "{}", _baseTime);
        await _service.RecordCompletedResponseAsync(tenant, key2, 200, null, "{}", _baseTime);

        var inProgressOnly = await _service.GetRecordsAsync(tenant, statusFilter: "InProgress");
        inProgressOnly.Should().HaveCount(1);
        inProgressOnly.First().Key.Should().Be(key1);

        var completedOnly = await _service.GetRecordsAsync(tenant, statusFilter: "Completed");
        completedOnly.Should().HaveCount(1);
        completedOnly.First().Key.Should().Be(key2);
    }

    [Fact]
    public async Task MultiTenantIsolation_SameKeyDifferentTenants_OperateIndependently()
    {
        var sharedKey = "idem_shared_key_100";
        var tenantA = "tenant_alpha_inc";
        var tenantB = "tenant_beta_inc";

        var resA = await _service.TryAcquireLockAsync(tenantA, sharedKey, "POST", "/api/v1/orders", "{\"cust\": 1}", _baseTime);
        var resB = await _service.TryAcquireLockAsync(tenantB, sharedKey, "POST", "/api/v1/orders", "{\"cust\": 2}", _baseTime);

        resA.Success.Should().BeTrue("Tenant Alpha should acquire lock for the key");
        resB.Success.Should().BeTrue("Tenant Beta should also acquire lock independently for the same key");

        var recA = (await _service.GetRecordsAsync(tenantA)).First(r => r.Key == sharedKey);
        var recB = (await _service.GetRecordsAsync(tenantB)).First(r => r.Key == sharedKey);

        recA.TenantId.Should().Be(tenantA);
        recB.TenantId.Should().Be(tenantB);
        recA.Fingerprint.Should().NotBe(recB.Fingerprint);
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentAcquireLockAttempts_OnlyOneSucceedsOthersGet409()
    {
        var key = $"race_key_{Guid.NewGuid():N}";
        var body = "{\"tx\": \"critical_balance_debit\"}";
        const int concurrentTasks = 20;

        var tasks = Enumerable.Range(0, concurrentTasks).Select(_ => Task.Run(async () =>
        {
            return await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/debit", body, _baseTime);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        var successfulLocks = results.Count(r => r.Success && r.StatusCode == 200);
        var conflictLocks = results.Count(r => !r.Success && r.StatusCode == 409);

        successfulLocks.Should().Be(1, "Exactly one thread must win the in-flight lock race");
        conflictLocks.Should().Be(concurrentTasks - 1, "All other competing threads must receive 409 Conflict");
    }

    [Fact]
    public async Task ThreadSafety_ConcurrentReplays_ThreadSafelyIncrementsReplayCounter()
    {
        var key = $"concurrent_replay_{Guid.NewGuid():N}";
        var body = "{\"item\": \"fast_read\"}";

        await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/read", body, _baseTime);
        await _service.RecordCompletedResponseAsync(DefaultTenant, key, 200, null, "{\"cached\": true}", _baseTime);

        const int replayCount = 50;
        var tasks = Enumerable.Range(0, replayCount).Select(_ => Task.Run(async () =>
        {
            return await _service.TryAcquireLockAsync(DefaultTenant, key, "POST", "/api/v1/read", body, _baseTime.AddSeconds(5));
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.ReplayDetected && r.StatusCode == 200);

        var records = await _service.GetRecordsAsync(DefaultTenant);
        var entry = records.First(r => r.Key == key);
        entry.ReplayCount.Should().Be(replayCount);
    }

    [Fact]
    public async Task SeedData_ContainsRealisticPastRequests_OnInitialization()
    {
        var freshSvc = new DistributedIdempotencyService();
        var records = await freshSvc.GetRecordsAsync("tenant-default");

        records.Should().NotBeEmpty();
        records.Count.Should().BeGreaterThanOrEqualTo(6);

        // Check realistic seeded transactions
        records.Should().Contain(r => r.Key.StartsWith("idem_pi_") && r.Status == IdempotencyStatus.Completed);
        records.Should().Contain(r => r.Key.StartsWith("idem_chk_") && r.Status == IdempotencyStatus.Completed);
        records.Should().Contain(r => r.Key.StartsWith("idem_wh_") && r.Status == IdempotencyStatus.Completed);
        records.Should().Contain(r => r.Key.StartsWith("idem_sub_") && r.Status == IdempotencyStatus.InProgress);
        records.Should().Contain(r => r.Key.StartsWith("idem_refund_") && r.Status == IdempotencyStatus.PayloadMismatch);

        var metrics = await freshSvc.GetMetricsAsync("tenant-default");
        metrics.ReplayHits.Should().BeGreaterThan(0);
        metrics.ConflictsDetected.Should().BeGreaterThan(0);
        metrics.MismatchesBlocked.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ResetAllAsync_PurgesAllLedgerEntriesAndResetsCounters()
    {
        await _service.ResetAllAsync();

        var records = await _service.GetRecordsAsync();
        records.Should().BeEmpty();

        var metrics = await _service.GetMetricsAsync();
        metrics.TotalTrackedKeys.Should().Be(0);
        metrics.ReplayHits.Should().Be(0);
        metrics.ConflictsDetected.Should().Be(0);
        metrics.MismatchesBlocked.Should().Be(0);
    }
}
