using System.Collections.Concurrent;
using FluentAssertions;
using SGrowing.Api.Modules.Messaging;
using Xunit;

namespace SGrowing.Tests.Messaging;

public class DistributedEventMeshServiceTests
{
    private readonly DistributedEventMeshService _service;
    private readonly Guid _testTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public DistributedEventMeshServiceTests()
    {
        _service = new DistributedEventMeshService();
    }

    [Fact]
    public async Task PublishAsync_ValidEvent_SucceedsWithSequenceNumberAndMetadata()
    {
        // Arrange
        var req = new PublishEventRequest(
            TenantId: _testTenantId,
            Topic: "orders.created",
            PartitionKey: "tenant-cust-123",
            EventId: $"evt_{Guid.NewGuid():N}",
            EventType: "OrderCreated",
            Payload: "{\"orderId\":\"1001\",\"total\":99.95}"
        );

        // Act
        var result = await _service.PublishAsync(req);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Deduplicated.Should().BeFalse();
        result.SequenceNumber.Should().BeGreaterThanOrEqualTo(0);
        result.PartitionIndex.Should().BeInRange(0, _service.PartitionCount - 1);
        result.EventFingerprint.Should().NotBeNullOrWhiteSpace();
        result.Subject.Should().Contain("orders.created");
    }

    [Fact]
    public async Task PublishAsync_DispatchesToRegisteredMatchingHandler_IncrementsTotalConsumed()
    {
        // Arrange
        MeshEventEnvelope? receivedEnvelope = null;
        var tcs = new TaskCompletionSource<bool>();

        _service.RegisterHandler("orders.created", env =>
        {
            receivedEnvelope = env;
            tcs.SetResult(true);
            return Task.CompletedTask;
        });

        var req = new PublishEventRequest(
            TenantId: _testTenantId,
            Topic: "orders.created",
            PartitionKey: "cust_777",
            EventId: "evt_single_test",
            EventType: "OrderCreated",
            Payload: "{\"status\":\"paid\"}"
        );

        // Act
        var res = await _service.PublishAsync(req);
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        // Assert
        res.Success.Should().BeTrue();
        receivedEnvelope.Should().NotBeNull();
        receivedEnvelope!.Topic.Should().Be("orders.created");
        receivedEnvelope.Payload.Should().Be("{\"status\":\"paid\"}");

        var metrics = _service.GetMetrics();
        metrics.TotalConsumed.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_WildcardTopicPattern_DispatchesCorrectly()
    {
        // Arrange
        var eventsReceived = new List<string>();

        _service.RegisterHandler("telemetry.*", env =>
        {
            eventsReceived.Add(env.Topic);
            return Task.CompletedTask;
        });

        // Act
        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "telemetry.click", "p1", null, "Click", "{}"));
        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "telemetry.scroll", "p2", null, "Scroll", "{}"));
        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "billing.invoice", "p3", null, "Invoice", "{}"));

        // Assert
        eventsReceived.Should().HaveCount(2);
        eventsReceived.Should().Contain("telemetry.click");
        eventsReceived.Should().Contain("telemetry.scroll");
        eventsReceived.Should().NotContain("billing.invoice");
    }

    [Fact]
    public async Task PublishAsync_UnmatchedTopic_DoesNotDispatchToHandler()
    {
        // Arrange
        int handlerCalls = 0;
        _service.RegisterHandler("orders.shipped", _ =>
        {
            Interlocked.Increment(ref handlerCalls);
            return Task.CompletedTask;
        });

        // Act
        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "crm.lead_added", "k1", null, "Lead", "{}"));

        // Assert
        handlerCalls.Should().Be(0);
        var metrics = _service.GetMetrics();
        metrics.TotalConsumed.Should().Be(0);
    }

    [Fact]
    public async Task PublishAsync_MultipleHandlers_AllReceiveEventPayload()
    {
        // Arrange
        int handler1Count = 0;
        int handler2Count = 0;

        _service.RegisterHandler("inventory.*", _ =>
        {
            Interlocked.Increment(ref handler1Count);
            return Task.CompletedTask;
        });

        _service.RegisterHandler("inventory.stock_updated", _ =>
        {
            Interlocked.Increment(ref handler2Count);
            return Task.CompletedTask;
        });

        // Act
        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "inventory.stock_updated", "sku_1", null, "Stock", "{}"));

        // Assert
        handler1Count.Should().Be(1);
        handler2Count.Should().Be(1);
    }

    [Fact]
    public void ComputePartition_IdenticalKey_ProducesSamePartitionEveryTime()
    {
        // Arrange
        string partitionKey = "account_uuid_99999_shard_alpha";

        // Act
        int p1 = _service.ComputePartition(partitionKey);
        int p2 = _service.ComputePartition(partitionKey);
        int p3 = _service.ComputePartition(partitionKey);

        // Assert
        p1.Should().Be(p2);
        p2.Should().Be(p3);
        p1.Should().BeInRange(0, _service.PartitionCount - 1);
    }

    [Fact]
    public void ComputePartition_DifferentKeys_DistributesAcrossPartitions()
    {
        // Arrange
        var keys = Enumerable.Range(1, 200).Select(i => $"entity-key-{i}-{Guid.NewGuid():N}").ToList();
        var partitionBuckets = new HashSet<int>();

        // Act
        foreach (var key in keys)
        {
            partitionBuckets.Add(_service.ComputePartition(key));
        }

        // Assert
        partitionBuckets.Count.Should().BeGreaterThan(1, "distinct partition keys should hash across multiple buckets");
    }

    [Fact]
    public void ComputePartition_NullOrEmptyKey_ReturnsDefaultPartitionZero()
    {
        // Act & Assert
        _service.ComputePartition(null).Should().Be(0);
        _service.ComputePartition("").Should().Be(0);
    }

    [Fact]
    public void ComputePartition_ExtremeValuesAndCollisions_NeverThrowsOrExceedsBounds()
    {
        // Act & Assert
        var extremeKeys = new[] { "0", "-1", "\0", new string('A', 10000), "!@#$%^&*()_+=-~`[]{};:'\",.<>?/\\|" };
        foreach (var key in extremeKeys)
        {
            int partition = _service.ComputePartition(key);
            partition.Should().BeInRange(0, _service.PartitionCount - 1);
        }
    }

    [Fact]
    public void MurmurHash3_DeterministicAcrossRepeatedInvocations()
    {
        // Arrange
        string testInput = "SGrowing_Distributed_Event_Mesh_Consistency_Token";

        // Act
        int h1 = MurmurHash3.Hash32(testInput);
        int h2 = MurmurHash3.Hash32(testInput);

        // Assert
        h1.Should().Be(h2);
    }

    [Fact]
    public void MurmurHash3_EmptyAndNullStrings_ReturnsZero()
    {
        MurmurHash3.Hash32((string?)null).Should().Be(0);
        MurmurHash3.Hash32("").Should().Be(0);
    }

    [Fact]
    public async Task Deduplication_ExactSameEventInWindow_IsMarkedDeduplicatedAndSuppressed()
    {
        // Arrange
        var req = new PublishEventRequest(
            TenantId: _testTenantId,
            Topic: "payments.captured",
            PartitionKey: "cust_10",
            EventId: "evt_payment_unique_888",
            EventType: "PaymentCaptured",
            Payload: "{\"amount\":450.00,\"currency\":\"USD\"}"
        );

        // Act
        var res1 = await _service.PublishAsync(req);
        var res2 = await _service.PublishAsync(req);

        // Assert
        res1.Success.Should().BeTrue();
        res1.Deduplicated.Should().BeFalse();

        res2.Success.Should().BeTrue();
        res2.Deduplicated.Should().BeTrue("identical event published within deduplication window must be suppressed");

        var metrics = _service.GetMetrics();
        metrics.TotalPublished.Should().Be(1);
        metrics.TotalDeduplicated.Should().Be(1);
    }

    [Fact]
    public async Task Deduplication_DifferentPayloadsSameEventId_GeneratesDifferentFingerprints()
    {
        // Arrange
        var req1 = new PublishEventRequest(_testTenantId, "leads.sync", "k1", "evt_1", "Lead", "{\"v\":1}");
        var req2 = new PublishEventRequest(_testTenantId, "leads.sync", "k1", "evt_1", "Lead", "{\"v\":2}");

        // Act
        var res1 = await _service.PublishAsync(req1);
        var res2 = await _service.PublishAsync(req2);

        // Assert
        res1.EventFingerprint.Should().NotBe(res2.EventFingerprint);
        res2.Deduplicated.Should().BeFalse();
    }

    [Fact]
    public async Task Deduplication_DifferentTenantsSameEventId_AreNotTreatedAsDuplicates()
    {
        // Arrange
        var tenant2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var req1 = new PublishEventRequest(_testTenantId, "common.topic", "k", "evt_shared_id", "Evt", "{}");
        var req2 = new PublishEventRequest(tenant2, "common.topic", "k", "evt_shared_id", "Evt", "{}");

        // Act
        var res1 = await _service.PublishAsync(req1);
        var res2 = await _service.PublishAsync(req2);

        // Assert
        res1.Deduplicated.Should().BeFalse();
        res2.Deduplicated.Should().BeFalse("different tenants have isolated deduplication namespaces");
    }

    [Fact]
    public async Task Deduplication_ExpiredWindow_AllowsRepublishing()
    {
        // Arrange
        _service.DeduplicationWindow = TimeSpan.FromMilliseconds(50);
        var req = new PublishEventRequest(_testTenantId, "window.test", "pk", "evt_expiring", "Evt", "{\"x\":1}");

        // Act
        var res1 = await _service.PublishAsync(req);
        await Task.Delay(100); // Wait for expiration
        var res2 = await _service.PublishAsync(req);

        // Assert
        res1.Deduplicated.Should().BeFalse();
        res2.Deduplicated.Should().BeFalse("expired deduplication record should allow re-publication");
    }

    [Fact]
    public async Task Deduplication_HitCount_IncrementsOnRepeatedDuplicateAttempts()
    {
        // Arrange
        var req = new PublishEventRequest(_testTenantId, "repeat.test", "pk", "evt_repeat", "Evt", "{\"cnt\":1}");

        // Act
        await _service.PublishAsync(req);
        await _service.PublishAsync(req);
        await _service.PublishAsync(req);

        // Assert
        var metrics = _service.GetMetrics();
        metrics.TotalDeduplicated.Should().Be(2);
    }

    [Fact]
    public async Task ConsumerFailure_CapturesIntoDeadLetterQueue_WithRetryCountAndReason()
    {
        // Arrange
        _service.RegisterHandler("orders.failing", _ =>
        {
            throw new InvalidOperationException("External Payment Gateway Connection Timed Out");
        });

        var req = new PublishEventRequest(
            TenantId: _testTenantId,
            Topic: "orders.failing",
            PartitionKey: "fail_pk",
            EventId: "evt_fail_1",
            EventType: "OrderPayment",
            Payload: "{\"cartId\":99}"
        );

        // Act
        await _service.PublishAsync(req);

        // Assert
        var dlqItems = _service.GetDlqItems(_testTenantId, "orders.failing");
        dlqItems.Should().NotBeEmpty();
        var item = dlqItems.First();
        item.EventId.Should().Be("evt_fail_1");
        item.FailureReason.Should().Contain("External Payment Gateway Connection Timed Out");
        item.AttemptCount.Should().Be(1);
        item.Status.Should().Be(DlqStatus.PendingRetry);
        item.NextRetryAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ConsumerFailure_ExceedsMaxRetries_MarksAsQuarantined()
    {
        // Arrange
        _service.MaxRetryAttempts = 1;
        _service.RegisterHandler("critical.failure", _ =>
        {
            throw new FormatException("Corrupt binary payload payload decompression failed");
        });

        var req = new PublishEventRequest(_testTenantId, "critical.failure", "k", "evt_fatal", "Fatal", "corrupt");

        // Act
        await _service.PublishAsync(req);

        // Assert
        var dlqItems = _service.GetDlqItems(_testTenantId, "critical.failure", DlqStatus.Quarantined);
        dlqItems.Should().HaveCount(1);
        dlqItems[0].Status.Should().Be(DlqStatus.Quarantined);
        dlqItems[0].NextRetryAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ConsumerFailure_PendingRetry_RecordsNextRetryAtUtcWithBackoff()
    {
        // Arrange
        _service.MaxRetryAttempts = 4;
        _service.BaseRetryDelaySeconds = 2.0;

        _service.RegisterHandler("backoff.test", _ => throw new TimeoutException("Database connection timeout"));

        // Act
        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "backoff.test", "k", "evt_b1", "Evt", "{}"));

        // Assert
        var dlq = _service.GetDlqItems(_testTenantId, "backoff.test").FirstOrDefault();
        dlq.Should().NotBeNull();
        dlq!.NextRetryAtUtc.Should().BeAfter(DateTime.UtcNow.AddSeconds(1.5));
    }

    [Fact]
    public async Task ReplayDlqAsync_SingleEvent_ReExecutesAndMarksAsReplayed()
    {
        // Arrange
        bool succeedOnReplay = false;
        _service.MaxRetryAttempts = 1;

        _service.RegisterHandler("orders.transient", _ =>
        {
            if (!succeedOnReplay)
            {
                throw new ApplicationException("Transient network blip");
            }
            return Task.CompletedTask;
        });

        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "orders.transient", "k", "evt_replay_single", "Order", "{}"));

        var dlq = _service.GetDlqItems(_testTenantId, "orders.transient", DlqStatus.Quarantined).First();

        // Act: Fix transient error and replay
        succeedOnReplay = true;
        var replayResult = await _service.ReplayDlqAsync(new ReplayDlqRequest(DlqId: dlq.Id));

        // Assert
        replayResult.ReplayedCount.Should().Be(1);
        replayResult.FailedCount.Should().Be(0);

        var updatedDlq = _service.GetDlqItems(_testTenantId, "orders.transient", DlqStatus.Replayed).First();
        updatedDlq.Status.Should().Be(DlqStatus.Replayed);
    }

    [Fact]
    public async Task ReplayDlqAsync_BulkEvents_RestoresAllMatchingQuarantinedItems()
    {
        // Arrange
        bool fail = true;
        _service.MaxRetryAttempts = 1;
        _service.RegisterHandler("bulk.topic", _ =>
        {
            if (fail) throw new Exception("Bulk crash");
            return Task.CompletedTask;
        });

        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "bulk.topic", "k1", "evt_b_1", "Bulk", "{}"));
        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "bulk.topic", "k2", "evt_b_2", "Bulk", "{}"));

        _service.GetDlqItems(_testTenantId, "bulk.topic", DlqStatus.Quarantined).Should().HaveCount(2);

        // Act
        fail = false;
        var bulkReplay = await _service.ReplayDlqAsync(new ReplayDlqRequest(Topic: "bulk.topic", TenantId: _testTenantId));

        // Assert
        bulkReplay.ReplayedCount.Should().Be(2);
        bulkReplay.FailedCount.Should().Be(0);
        _service.GetDlqItems(_testTenantId, "bulk.topic", DlqStatus.Replayed).Should().HaveCount(2);
    }

    [Fact]
    public async Task ReplayDlqAsync_FailedReplay_IncrementsAttemptCountAndKeepsQuarantined()
    {
        // Arrange
        _service.MaxRetryAttempts = 1;
        _service.RegisterHandler("always.fail", _ => throw new ArgumentException("Still failing"));

        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "always.fail", "k", "evt_af", "Evt", "{}"));
        var dlq = _service.GetDlqItems(_testTenantId, "always.fail").First();

        // Act
        var replayResult = await _service.ReplayDlqAsync(new ReplayDlqRequest(DlqId: dlq.Id));

        // Assert
        replayResult.FailedCount.Should().Be(1);
        var updated = _service.GetDlqItems(_testTenantId, "always.fail").First();
        updated.AttemptCount.Should().Be(2);
        updated.Status.Should().Be(DlqStatus.Quarantined);
    }

    [Fact]
    public async Task PurgeDlqAsync_ByDlqId_RemovesSingleEntry()
    {
        // Arrange
        _service.MaxRetryAttempts = 1;
        _service.RegisterHandler("purge.single", _ => throw new Exception("Crash"));

        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "purge.single", "k", "evt_p1", "Evt", "{}"));
        var dlq = _service.GetDlqItems(_testTenantId, "purge.single").First();

        // Act
        var purgeResult = await _service.PurgeDlqAsync(new PurgeDlqRequest(DlqId: dlq.Id));

        // Assert
        purgeResult.PurgedCount.Should().Be(1);
        _service.GetDlqItems(_testTenantId, "purge.single").Should().BeEmpty();
    }

    [Fact]
    public async Task PurgeDlqAsync_ByTopicOrTenant_PurgesMatchingEntries()
    {
        // Arrange
        _service.MaxRetryAttempts = 1;
        _service.RegisterHandler("purge.bulk", _ => throw new Exception("Crash"));

        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "purge.bulk", "k1", "evt_pb1", "Evt", "{}"));
        await _service.PublishAsync(new PublishEventRequest(_testTenantId, "purge.bulk", "k2", "evt_pb2", "Evt", "{}"));

        // Act
        var purgeResult = await _service.PurgeDlqAsync(new PurgeDlqRequest(Topic: "purge.bulk", TenantId: _testTenantId));

        // Assert
        purgeResult.PurgedCount.Should().Be(2);
        _service.GetDlqItems(_testTenantId, "purge.bulk").Should().BeEmpty();
    }

    [Fact]
    public async Task Metrics_PublishedAndConsumedCounts_IncrementAccurately()
    {
        // Arrange
        _service.RegisterHandler("metrics.test", _ => Task.CompletedTask);

        // Act
        for (int i = 0; i < 5; i++)
        {
            await _service.PublishAsync(new PublishEventRequest(_testTenantId, "metrics.test", $"k_{i}", $"evt_m_{i}", "Evt", "{}"));
        }

        // Assert
        var metrics = _service.GetMetrics();
        metrics.TotalPublished.Should().Be(5);
        metrics.TotalConsumed.Should().Be(5);
        metrics.TotalDeduplicated.Should().Be(0);
        metrics.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task Metrics_DeduplicationDrops_ReflectedInTelemetry()
    {
        // Arrange
        var req = new PublishEventRequest(_testTenantId, "dedup.metric", "k", "evt_dm_1", "Evt", "{\"a\":1}");

        // Act
        await _service.PublishAsync(req);
        await _service.PublishAsync(req);
        await _service.PublishAsync(req);

        // Assert
        var metrics = _service.GetMetrics();
        metrics.TotalPublished.Should().Be(1);
        metrics.TotalDeduplicated.Should().Be(2);
    }

    [Fact]
    public async Task Metrics_PartitionLags_ReflectUnprocessedEvents()
    {
        // Arrange - No consumer handler registered
        var req = new PublishEventRequest(_testTenantId, "unhandled.topic", "key_lag", "evt_lag", "Lag", "{}");

        // Act
        var res = await _service.PublishAsync(req);

        // Assert
        var partitions = _service.GetPartitions();
        var targetPartition = partitions.First(p => p.PartitionIndex == res.PartitionIndex);
        targetPartition.CurrentDepth.Should().BeGreaterThanOrEqualTo(1);
        targetPartition.ConsumerLag.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task MultiBroker_RedisStreamsMode_EmulatesStreamIdAndPelTracking()
    {
        // Arrange
        var req = new PublishEventRequest(
            TenantId: _testTenantId,
            Topic: "redis.events",
            PartitionKey: "cust_stream_1",
            EventId: "evt_redis_1",
            EventType: "RedisEvent",
            Payload: "{\"xadd\":true}",
            BrokerMode: EventBrokerMode.RedisStreams
        );

        // Act
        var result = await _service.PublishAsync(req);

        // Assert
        result.BrokerMode.Should().Be(EventBrokerMode.RedisStreams);
        result.RedisStreamId.Should().NotBeNullOrWhiteSpace();
        result.RedisStreamId.Should().MatchRegex(@"^\d+-\d+$");

        string streamKey = $"stream:{_testTenantId}:redis.events";
        var pending = _service.GetRedisPendingEntries(streamKey, "events:workers");
        pending.Should().NotBeNull();
    }

    [Fact]
    public async Task MultiBroker_NatsJetStreamMode_MapsSubjectWithPartitionHierarchy()
    {
        // Arrange
        var req = new PublishEventRequest(
            TenantId: _testTenantId,
            Topic: "nats/logistics/trucks",
            PartitionKey: "truck-99",
            EventId: "evt_nats_1",
            EventType: "TruckPing",
            Payload: "{\"lat\":37.77}",
            BrokerMode: EventBrokerMode.NatsJetStream
        );

        // Act
        var result = await _service.PublishAsync(req);

        // Assert
        result.BrokerMode.Should().Be(EventBrokerMode.NatsJetStream);
        result.Subject.Should().StartWith($"events.{_testTenantId:N}.nats.logistics.trucks.p");
    }

    [Fact]
    public async Task MultiBroker_InMemoryRingBuffer_RetainsSnapshotsAndWrapsAroundCapacity()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            await _service.PublishAsync(new PublishEventRequest(
                _testTenantId, "ring.test", $"k_{i}", $"evt_rb_{i}", "Ring", $"{{\"seq\":{i}}}"
            ));
        }

        // Act
        var snapshot = _service.GetRecentRingBufferEvents(10);

        // Assert
        snapshot.Should().HaveCount(10);
        snapshot.Last().Payload.Should().Contain("\"seq\":14");
    }

    [Fact]
    public async Task ConcurrentPublishing_MultithreadedHighLoad_MaintainsThreadSafetyAndIntegrity()
    {
        // Arrange
        const int concurrentTasks = 10;
        const int eventsPerTask = 50;
        var consumedCounter = 0;

        _service.RegisterHandler("concurrent.*", _ =>
        {
            Interlocked.Increment(ref consumedCounter);
            return Task.CompletedTask;
        });

        // Act
        var tasks = Enumerable.Range(0, concurrentTasks).Select(async taskId =>
        {
            for (int i = 0; i < eventsPerTask; i++)
            {
                await _service.PublishAsync(new PublishEventRequest(
                    TenantId: _testTenantId,
                    Topic: "concurrent.ingest",
                    PartitionKey: $"worker-{taskId}-item-{i}",
                    EventId: $"evt_c_{taskId}_{i}",
                    EventType: "ConcurrentEvent",
                    Payload: $"{{\"taskId\":{taskId},\"i\":{i}}}"
                ));
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        var metrics = _service.GetMetrics();
        metrics.TotalPublished.Should().Be(concurrentTasks * eventsPerTask);
        metrics.TotalConsumed.Should().Be(concurrentTasks * eventsPerTask);
        consumedCounter.Should().Be(concurrentTasks * eventsPerTask);

        var partitions = _service.GetPartitions();
        long totalProcessedAcrossPartitions = partitions.Sum(p => p.TotalProcessed);
        totalProcessedAcrossPartitions.Should().Be(concurrentTasks * eventsPerTask);
    }

    [Fact]
    public async Task PublishBatchAsync_AtomicallyProcessesAllEventsAndReportsDeduplications()
    {
        // Arrange
        var events = new List<PublishEventRequest>
        {
            new(_testTenantId, "batch.topic", "k1", "evt_b1", "B1", "{\"val\":1}"),
            new(_testTenantId, "batch.topic", "k2", "evt_b2", "B2", "{\"val\":2}"),
            new(_testTenantId, "batch.topic", "k1", "evt_b1", "B1", "{\"val\":1}") // Duplicate!
        };

        // Act
        var batchResult = await _service.PublishBatchAsync(events);

        // Assert
        batchResult.TotalSubmitted.Should().Be(3);
        batchResult.PublishedCount.Should().Be(2);
        batchResult.DeduplicatedCount.Should().Be(1);
        batchResult.FailedCount.Should().Be(0);
        batchResult.Results.Should().HaveCount(3);
        batchResult.Results[2].Deduplicated.Should().BeTrue();
    }
}
