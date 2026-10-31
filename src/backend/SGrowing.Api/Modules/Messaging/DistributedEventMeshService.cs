using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SGrowing.Api.Modules.Messaging;

public enum EventBrokerMode
{
    InMemoryRingBuffer,
    RedisStreams,
    NatsJetStream
}

public enum DlqStatus
{
    Quarantined,
    PendingRetry,
    Replayed,
    Purged
}

public record MeshEventEnvelope(
    string EventId,
    Guid TenantId,
    string Topic,
    string? PartitionKey,
    string EventType,
    string Payload,
    Dictionary<string, string>? Headers = null,
    DateTime TimestampUtc = default,
    int RetryCount = 0,
    string? Fingerprint = null
);

public record PublishEventRequest(
    Guid? TenantId,
    string Topic,
    string? PartitionKey,
    string? EventId,
    string? EventType,
    string Payload,
    EventBrokerMode? BrokerMode = null,
    Dictionary<string, string>? Headers = null
);

public record PublishEventResult(
    bool Success,
    string EventId,
    string Topic,
    int PartitionIndex,
    string EventFingerprint,
    bool Deduplicated,
    EventBrokerMode BrokerMode,
    DateTime PublishedAtUtc,
    long SequenceNumber,
    string? Subject = null,
    string? RedisStreamId = null,
    string? Error = null
);

public record BatchPublishResult(
    int TotalSubmitted,
    int PublishedCount,
    int DeduplicatedCount,
    int FailedCount,
    IReadOnlyList<PublishEventResult> Results
);

public record PartitionLagMetric(
    int PartitionIndex,
    string AssignedWorker,
    long CurrentDepth,
    long ConsumerLag,
    long TotalProcessed,
    DateTime? LastEventTimestampUtc
);

public record EventMeshMetrics(
    long TotalPublished,
    long TotalConsumed,
    long TotalDeduplicated,
    int DlqSize,
    int PartitionCount,
    IReadOnlyList<PartitionLagMetric> PartitionLags,
    EventBrokerMode ActiveBrokerMode,
    double ThroughputPerSecond,
    DateTime? LastEventPublishedAtUtc,
    bool IsHealthy,
    Dictionary<string, string> BrokerMetadata
);

public record DlqItem(
    string Id,
    Guid TenantId,
    string EventId,
    string Topic,
    string? PartitionKey,
    int PartitionIndex,
    string EventType,
    string Payload,
    int AttemptCount,
    string FailureReason,
    string? StackTrace,
    DateTime FirstFailedAtUtc,
    DateTime LastAttemptAtUtc,
    DateTime? NextRetryAtUtc,
    DlqStatus Status,
    Dictionary<string, string>? Headers = null
);

public record ReplayDlqRequest(
    string? DlqId = null,
    string? Topic = null,
    Guid? TenantId = null,
    int? MaxCount = null
);

public record ReplayDlqResult(
    int ReplayedCount,
    int FailedCount,
    IReadOnlyList<string> ReplayedIds,
    string? Message = null
);

public record PurgeDlqRequest(
    string? DlqId = null,
    string? Topic = null,
    Guid? TenantId = null
);

public record PurgeDlqResult(
    int PurgedCount,
    string? Message = null
);

public class DeduplicationEntry
{
    public string Fingerprint { get; init; } = string.Empty;
    public DateTime FirstSeenUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public long HitCount { get; set; }
}

public interface IDistributedEventMeshService
{
    EventBrokerMode CurrentBrokerMode { get; set; }
    int PartitionCount { get; }
    TimeSpan DeduplicationWindow { get; set; }
    int MaxRetryAttempts { get; set; }

    Task<PublishEventResult> PublishAsync(PublishEventRequest request, CancellationToken ct = default);
    Task<BatchPublishResult> PublishBatchAsync(IEnumerable<PublishEventRequest> requests, CancellationToken ct = default);

    EventMeshMetrics GetMetrics();
    IReadOnlyList<PartitionLagMetric> GetPartitions();

    IReadOnlyList<DlqItem> GetDlqItems(Guid? tenantId = null, string? topic = null, DlqStatus? status = null, int limit = 100);
    Task<ReplayDlqResult> ReplayDlqAsync(ReplayDlqRequest request, CancellationToken ct = default);
    Task<PurgeDlqResult> PurgeDlqAsync(PurgeDlqRequest request, CancellationToken ct = default);

    void RegisterHandler(string topicPattern, Func<MeshEventEnvelope, Task> handler);
    void UnregisterHandlers();

    int ComputePartition(string? partitionKey);
    string ComputeFingerprint(Guid tenantId, string topic, string eventId, string payload);

    // Multi-Broker specific helpers
    IReadOnlyList<MeshEventEnvelope> GetRecentRingBufferEvents(int count = 50);
    IReadOnlyDictionary<string, string> GetRedisPendingEntries(string streamKey, string consumerGroup);
    string GetNatsSubjectForEvent(Guid tenantId, string topic, int partitionIndex);
}

public static class MurmurHash3
{
    public static int Hash32(string? input, uint seed = 0)
    {
        if (string.IsNullOrEmpty(input)) return 0;
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        return Hash32(bytes, seed);
    }

    public static int Hash32(ReadOnlySpan<byte> data, uint seed = 0)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;
        uint h1 = seed;
        int length = data.Length;
        int nblocks = length / 4;

        for (int i = 0; i < nblocks; i++)
        {
            uint k1 = BitConverter.ToUInt32(data.Slice(i * 4, 4));
            k1 *= c1;
            k1 = (k1 << 15) | (k1 >> 17);
            k1 *= c2;

            h1 ^= k1;
            h1 = (h1 << 13) | (h1 >> 19);
            h1 = h1 * 5 + 0xe6546b64;
        }

        int tailOffset = nblocks * 4;
        uint kTail = 0;
        int remaining = length & 3;

        if (remaining == 3) kTail ^= (uint)data[tailOffset + 2] << 16;
        if (remaining >= 2) kTail ^= (uint)data[tailOffset + 1] << 8;
        if (remaining >= 1)
        {
            kTail ^= data[tailOffset];
            kTail *= c1;
            kTail = (kTail << 15) | (kTail >> 17);
            kTail *= c2;
            h1 ^= kTail;
        }

        h1 ^= (uint)length;
        h1 ^= h1 >> 16;
        h1 *= 0x85ebca6b;
        h1 ^= h1 >> 13;
        h1 *= 0xc2b2ae35;
        h1 ^= h1 >> 16;

        return (int)h1;
    }
}

public class ConcurrentRingBuffer<T>
{
    private readonly T?[] _buffer;
    private readonly int _capacity;
    private long _writeSequence = 0;
    private readonly object _lock = new();

    public ConcurrentRingBuffer(int capacity = 4096)
    {
        _capacity = capacity > 0 ? capacity : 4096;
        _buffer = new T?[_capacity];
    }

    public long Write(T item)
    {
        lock (_lock)
        {
            long seq = _writeSequence++;
            int index = (int)(seq % _capacity);
            _buffer[index] = item;
            return seq;
        }
    }

    public IReadOnlyList<T> GetSnapshot(int maxItems = 100)
    {
        lock (_lock)
        {
            var list = new List<T>();
            long current = _writeSequence;
            long count = Math.Min(current, (long)_capacity);
            long returnCount = Math.Min(count, (long)maxItems);

            for (long i = current - returnCount; i < current; i++)
            {
                if (i >= 0)
                {
                    int idx = (int)(i % _capacity);
                    var item = _buffer[idx];
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
            }
            return list;
        }
    }

    public int Capacity => _capacity;
    public long TotalWritten => Interlocked.Read(ref _writeSequence);
}

public class DistributedEventMeshService : IDistributedEventMeshService
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public EventBrokerMode CurrentBrokerMode { get; set; } = EventBrokerMode.InMemoryRingBuffer;
    public int PartitionCount { get; } = 8;
    public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromMinutes(30);
    public int MaxRetryAttempts { get; set; } = 3;
    public double BaseRetryDelaySeconds { get; set; } = 0.5;
    public double MaxRetryDelaySeconds { get; set; } = 30.0;

    private long _totalPublished;
    private long _totalConsumed;
    private long _totalDeduplicated;
    private DateTime? _lastEventPublishedAtUtc;
    private long _redisSequenceCounter;
    private long _natsSequenceCounter = 1000;

    private readonly ConcurrentRingBuffer<MeshEventEnvelope> _ringBuffer;
    private readonly ConcurrentDictionary<string, DeduplicationEntry> _dedupCache = new();
    private readonly ConcurrentDictionary<string, DlqItem> _dlqStore = new();
    private readonly ConcurrentDictionary<int, PartitionInternalState> _partitions = new();
    private readonly List<(string Pattern, Func<MeshEventEnvelope, Task> Handler)> _handlers = new();
    private readonly object _handlersLock = new();

    // Redis stream emulation storage
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _redisStreams = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _redisPel = new();

    private class PartitionInternalState
    {
        public int PartitionIndex { get; init; }
        public string AssignedWorker { get; set; } = string.Empty;
        public long CurrentDepth;
        public long ConsumerLag;
        public long TotalProcessed;
        public DateTime? LastEventTimestampUtc { get; set; }
        public ConcurrentQueue<MeshEventEnvelope> Queue { get; } = new();
    }

    public DistributedEventMeshService()
    {
        _ringBuffer = new ConcurrentRingBuffer<MeshEventEnvelope>(4096);

        for (int i = 0; i < PartitionCount; i++)
        {
            _partitions[i] = new PartitionInternalState
            {
                PartitionIndex = i,
                AssignedWorker = $"worker-core-{i % 4}",
                CurrentDepth = 0,
                ConsumerLag = 0,
                TotalProcessed = 0,
                LastEventTimestampUtc = null
            };
        }
    }

    public int ComputePartition(string? partitionKey)
    {
        if (string.IsNullOrEmpty(partitionKey))
        {
            return 0;
        }

        // PartitionIndex = Abs(MurmurHash3(PartitionKey)) % PartitionCount
        long hash = MurmurHash3.Hash32(partitionKey);
        long absHash = Math.Abs(hash);
        return (int)(absHash % PartitionCount);
    }

    public string ComputeFingerprint(Guid tenantId, string topic, string eventId, string payload)
    {
        // Sliding-window idempotent event deduplication using SHA-256 fingerprinting:
        // SHA256(TenantId:Topic:EventId:PayloadHash) where PayloadHash = SHA256(Payload)
        string payloadHash = ComputeSha256(payload ?? string.Empty);
        string composite = $"{tenantId}:{topic}:{eventId}:{payloadHash}";
        return ComputeSha256(composite);
    }

    public static string ComputeSha256(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            byte[] emptyBytes = SHA256.HashData(Array.Empty<byte>());
            return Convert.ToHexStringLower(emptyBytes);
        }
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    public void RegisterHandler(string topicPattern, Func<MeshEventEnvelope, Task> handler)
    {
        lock (_handlersLock)
        {
            _handlers.Add((topicPattern, handler));
        }
    }

    public void UnregisterHandlers()
    {
        lock (_handlersLock)
        {
            _handlers.Clear();
        }
    }

    public async Task<PublishEventResult> PublishAsync(PublishEventRequest request, CancellationToken ct = default)
    {
        var tenantId = request.TenantId ?? DefaultTenantId;
        var eventId = string.IsNullOrWhiteSpace(request.EventId) ? $"evt_{Guid.NewGuid():N}" : request.EventId.Trim();
        var topic = string.IsNullOrWhiteSpace(request.Topic) ? "default" : request.Topic.Trim().ToLowerInvariant();
        var eventType = string.IsNullOrWhiteSpace(request.EventType) ? "event.published" : request.EventType.Trim();
        var brokerMode = request.BrokerMode ?? CurrentBrokerMode;
        var partitionKey = string.IsNullOrWhiteSpace(request.PartitionKey) ? eventId : request.PartitionKey.Trim();

        // 1. Sliding-window deduplication check
        string fingerprint = ComputeFingerprint(tenantId, topic, eventId, request.Payload);
        var now = DateTime.UtcNow;
        CleanupExpiredDeduplications(now);

        bool isDuplicate = false;
        _dedupCache.AddOrUpdate(
            fingerprint,
            _ => new DeduplicationEntry
            {
                Fingerprint = fingerprint,
                FirstSeenUtc = now,
                ExpiresAtUtc = now.Add(DeduplicationWindow),
                HitCount = 1
            },
            (_, existing) =>
            {
                if (existing.ExpiresAtUtc > now)
                {
                    isDuplicate = true;
                    existing.HitCount++;
                    return existing;
                }
                return new DeduplicationEntry
                {
                    Fingerprint = fingerprint,
                    FirstSeenUtc = now,
                    ExpiresAtUtc = now.Add(DeduplicationWindow),
                    HitCount = 1
                };
            }
        );

        if (isDuplicate)
        {
            Interlocked.Increment(ref _totalDeduplicated);
            int cachedPartition = ComputePartition(partitionKey);
            return new PublishEventResult(
                Success: true,
                EventId: eventId,
                Topic: topic,
                PartitionIndex: cachedPartition,
                EventFingerprint: fingerprint,
                Deduplicated: true,
                BrokerMode: brokerMode,
                PublishedAtUtc: now,
                SequenceNumber: 0,
                Subject: GetNatsSubjectForEvent(tenantId, topic, cachedPartition),
                RedisStreamId: null,
                Error: null
            );
        }

        // 2. Consistent Partition Routing
        int partitionIndex = ComputePartition(partitionKey);
        var partition = _partitions[partitionIndex];

        var envelope = new MeshEventEnvelope(
            EventId: eventId,
            TenantId: tenantId,
            Topic: topic,
            PartitionKey: partitionKey,
            EventType: eventType,
            Payload: request.Payload,
            Headers: request.Headers != null ? new Dictionary<string, string>(request.Headers) : null,
            TimestampUtc: now,
            RetryCount: 0,
            Fingerprint: fingerprint
        );

        // 3. Multi-Broker adapter simulation and ring buffer storage
        long seq = _ringBuffer.Write(envelope);
        Interlocked.Increment(ref _totalPublished);
        _lastEventPublishedAtUtc = now;

        Interlocked.Increment(ref partition.CurrentDepth);
        Interlocked.Increment(ref partition.ConsumerLag);
        partition.LastEventTimestampUtc = now;
        partition.Queue.Enqueue(envelope);

        string? redisStreamId = null;
        if (brokerMode == EventBrokerMode.RedisStreams)
        {
            long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long rSeq = Interlocked.Increment(ref _redisSequenceCounter);
            redisStreamId = $"{ms}-{rSeq}";

            string streamKey = $"stream:{tenantId}:{topic}";
            var streamEntries = _redisStreams.GetOrAdd(streamKey, _ => new ConcurrentDictionary<string, string>());
            streamEntries[redisStreamId] = request.Payload;

            // Consumer group Pending Entry List (PEL)
            string groupPelKey = $"{streamKey}:cg:{topic}:workers";
            var pelEntries = _redisPel.GetOrAdd(groupPelKey, _ => new ConcurrentDictionary<string, string>());
            pelEntries[redisStreamId] = envelope.EventId;
        }

        string natsSubject = GetNatsSubjectForEvent(tenantId, topic, partitionIndex);
        if (brokerMode == EventBrokerMode.NatsJetStream)
        {
            Interlocked.Increment(ref _natsSequenceCounter);
        }

        // 4. Consumer Handler Dispatch
        List<Func<MeshEventEnvelope, Task>> matchedHandlers;
        lock (_handlersLock)
        {
            matchedHandlers = _handlers
                .Where(h => IsTopicMatch(h.Pattern, topic))
                .Select(h => h.Handler)
                .ToList();
        }

        if (matchedHandlers.Count > 0)
        {
            foreach (var handler in matchedHandlers)
            {
                try
                {
                    await handler(envelope);
                    Interlocked.Increment(ref _totalConsumed);
                    Interlocked.Increment(ref partition.TotalProcessed);

                    long lag = Interlocked.Decrement(ref partition.ConsumerLag);
                    if (lag < 0) Interlocked.Exchange(ref partition.ConsumerLag, 0);

                    long depth = Interlocked.Decrement(ref partition.CurrentDepth);
                    if (depth < 0) Interlocked.Exchange(ref partition.CurrentDepth, 0);

                    // Acknowledge Redis PEL if applicable
                    if (brokerMode == EventBrokerMode.RedisStreams && redisStreamId != null)
                    {
                        string groupPelKey = $"stream:{tenantId}:{topic}:cg:{topic}:workers";
                        if (_redisPel.TryGetValue(groupPelKey, out var pel))
                        {
                            pel.TryRemove(redisStreamId, out _);
                        }
                    }
                }
                catch (Exception ex)
                {
                    await HandleConsumerFailureAsync(envelope, partitionIndex, ex);
                }
            }
        }

        return new PublishEventResult(
            Success: true,
            EventId: eventId,
            Topic: topic,
            PartitionIndex: partitionIndex,
            EventFingerprint: fingerprint,
            Deduplicated: false,
            BrokerMode: brokerMode,
            PublishedAtUtc: now,
            SequenceNumber: seq,
            Subject: natsSubject,
            RedisStreamId: redisStreamId,
            Error: null
        );
    }

    public async Task<BatchPublishResult> PublishBatchAsync(IEnumerable<PublishEventRequest> requests, CancellationToken ct = default)
    {
        var list = requests.ToList();
        var results = new List<PublishEventResult>(list.Count);
        int published = 0;
        int deduplicated = 0;
        int failed = 0;

        foreach (var req in list)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var res = await PublishAsync(req, ct);
                results.Add(res);
                if (res.Deduplicated)
                {
                    deduplicated++;
                }
                else if (res.Success)
                {
                    published++;
                }
                else
                {
                    failed++;
                }
            }
            catch (Exception ex)
            {
                failed++;
                results.Add(new PublishEventResult(
                    Success: false,
                    EventId: req.EventId ?? string.Empty,
                    Topic: req.Topic ?? "unknown",
                    PartitionIndex: 0,
                    EventFingerprint: string.Empty,
                    Deduplicated: false,
                    BrokerMode: req.BrokerMode ?? CurrentBrokerMode,
                    PublishedAtUtc: DateTime.UtcNow,
                    SequenceNumber: -1,
                    Error: ex.Message
                ));
            }
        }

        return new BatchPublishResult(
            TotalSubmitted: list.Count,
            PublishedCount: published,
            DeduplicatedCount: deduplicated,
            FailedCount: failed,
            Results: results
        );
    }

    private async Task HandleConsumerFailureAsync(MeshEventEnvelope envelope, int partitionIndex, Exception ex)
    {
        int newAttempt = envelope.RetryCount + 1;
        var now = DateTime.UtcNow;

        // Exponential backoff + jitter calculation
        double expDelay = BaseRetryDelaySeconds * Math.Pow(2, Math.Max(0, newAttempt - 1));
        expDelay = Math.Min(expDelay, MaxRetryDelaySeconds);
        double jitter = (Random.Shared.NextDouble() * 0.25) * expDelay;
        TimeSpan nextRetryDelay = TimeSpan.FromSeconds(expDelay + jitter);

        bool isExhausted = newAttempt >= MaxRetryAttempts;
        var status = isExhausted ? DlqStatus.Quarantined : DlqStatus.PendingRetry;

        string dlqId = $"dlq_{envelope.EventId}_{newAttempt}";
        var dlqItem = new DlqItem(
            Id: dlqId,
            TenantId: envelope.TenantId,
            EventId: envelope.EventId,
            Topic: envelope.Topic,
            PartitionKey: envelope.PartitionKey,
            PartitionIndex: partitionIndex,
            EventType: envelope.EventType,
            Payload: envelope.Payload,
            AttemptCount: newAttempt,
            FailureReason: ex.Message,
            StackTrace: ex.StackTrace,
            FirstFailedAtUtc: envelope.TimestampUtc,
            LastAttemptAtUtc: now,
            NextRetryAtUtc: isExhausted ? null : now.Add(nextRetryDelay),
            Status: status,
            Headers: envelope.Headers
        );

        _dlqStore[dlqId] = dlqItem;
        await Task.CompletedTask;
    }

    public EventMeshMetrics GetMetrics()
    {
        var partitions = GetPartitions();
        int dlqCount = _dlqStore.Values.Count(d => d.Status == DlqStatus.Quarantined);

        // Throughput estimate based on published events
        double throughput = _totalPublished > 0 ? Math.Round((double)_totalPublished / Math.Max(1, (DateTime.UtcNow - (_lastEventPublishedAtUtc ?? DateTime.UtcNow)).TotalMinutes * 60 + 1), 2) : 0;

        var metadata = new Dictionary<string, string>
        {
            ["Broker"] = CurrentBrokerMode.ToString(),
            ["RingBufferCapacity"] = _ringBuffer.Capacity.ToString(),
            ["TotalRingBufferWritten"] = _ringBuffer.TotalWritten.ToString(),
            ["NatsStream"] = "STREAM_EVENTS",
            ["RedisConsumerGroup"] = "cg:events:workers",
            ["DeduplicationWindowMinutes"] = DeduplicationWindow.TotalMinutes.ToString("F0"),
            ["PartitionCount"] = PartitionCount.ToString()
        };

        return new EventMeshMetrics(
            TotalPublished: Interlocked.Read(ref _totalPublished),
            TotalConsumed: Interlocked.Read(ref _totalConsumed),
            TotalDeduplicated: Interlocked.Read(ref _totalDeduplicated),
            DlqSize: dlqCount,
            PartitionCount: PartitionCount,
            PartitionLags: partitions,
            ActiveBrokerMode: CurrentBrokerMode,
            ThroughputPerSecond: throughput,
            LastEventPublishedAtUtc: _lastEventPublishedAtUtc,
            IsHealthy: true,
            BrokerMetadata: metadata
        );
    }

    public IReadOnlyList<PartitionLagMetric> GetPartitions()
    {
        var list = new List<PartitionLagMetric>(PartitionCount);
        for (int i = 0; i < PartitionCount; i++)
        {
            if (_partitions.TryGetValue(i, out var p))
            {
                list.Add(new PartitionLagMetric(
                    PartitionIndex: p.PartitionIndex,
                    AssignedWorker: p.AssignedWorker,
                    CurrentDepth: Math.Max(0, Interlocked.Read(ref p.CurrentDepth)),
                    ConsumerLag: Math.Max(0, Interlocked.Read(ref p.ConsumerLag)),
                    TotalProcessed: Interlocked.Read(ref p.TotalProcessed),
                    LastEventTimestampUtc: p.LastEventTimestampUtc
                ));
            }
        }
        return list;
    }

    public IReadOnlyList<DlqItem> GetDlqItems(Guid? tenantId = null, string? topic = null, DlqStatus? status = null, int limit = 100)
    {
        var query = _dlqStore.Values.AsEnumerable();

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(d => d.TenantId == tenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            query = query.Where(d => d.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase));
        }

        if (status.HasValue)
        {
            query = query.Where(d => d.Status == status.Value);
        }

        return query
            .OrderByDescending(d => d.LastAttemptAtUtc)
            .Take(limit > 0 ? limit : 100)
            .ToList();
    }

    public async Task<ReplayDlqResult> ReplayDlqAsync(ReplayDlqRequest request, CancellationToken ct = default)
    {
        var targetItems = _dlqStore.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.DlqId))
        {
            targetItems = targetItems.Where(d => d.Id == request.DlqId);
        }
        else
        {
            if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
            {
                targetItems = targetItems.Where(d => d.TenantId == request.TenantId.Value);
            }
            if (!string.IsNullOrWhiteSpace(request.Topic))
            {
                targetItems = targetItems.Where(d => d.Topic.Equals(request.Topic, StringComparison.OrdinalIgnoreCase));
            }
            targetItems = targetItems.Where(d => d.Status == DlqStatus.Quarantined || d.Status == DlqStatus.PendingRetry);
        }

        int max = request.MaxCount ?? 100;
        var toReplay = targetItems.Take(max).ToList();

        int replayedCount = 0;
        int failedCount = 0;
        var replayedIds = new List<string>();

        foreach (var item in toReplay)
        {
            if (ct.IsCancellationRequested) break;

            var envelope = new MeshEventEnvelope(
                EventId: item.EventId,
                TenantId: item.TenantId,
                Topic: item.Topic,
                PartitionKey: item.PartitionKey,
                EventType: item.EventType,
                Payload: item.Payload,
                Headers: item.Headers,
                TimestampUtc: DateTime.UtcNow,
                RetryCount: item.AttemptCount
            );

            List<Func<MeshEventEnvelope, Task>> matchedHandlers;
            lock (_handlersLock)
            {
                matchedHandlers = _handlers
                    .Where(h => IsTopicMatch(h.Pattern, item.Topic))
                    .Select(h => h.Handler)
                    .ToList();
            }

            bool replaySuccess = true;
            if (matchedHandlers.Count > 0)
            {
                foreach (var handler in matchedHandlers)
                {
                    try
                    {
                        await handler(envelope);
                    }
                    catch (Exception ex)
                    {
                        replaySuccess = false;
                        _dlqStore[item.Id] = item with
                        {
                            AttemptCount = item.AttemptCount + 1,
                            LastAttemptAtUtc = DateTime.UtcNow,
                            FailureReason = $"Replay failed: {ex.Message}",
                            StackTrace = ex.StackTrace,
                            Status = DlqStatus.Quarantined
                        };
                        failedCount++;
                        break;
                    }
                }
            }

            if (replaySuccess)
            {
                _dlqStore[item.Id] = item with
                {
                    Status = DlqStatus.Replayed,
                    LastAttemptAtUtc = DateTime.UtcNow
                };
                Interlocked.Increment(ref _totalConsumed);
                replayedCount++;
                replayedIds.Add(item.Id);
            }
        }

        return new ReplayDlqResult(
            ReplayedCount: replayedCount,
            FailedCount: failedCount,
            ReplayedIds: replayedIds,
            Message: $"Successfully replayed {replayedCount} DLQ events ({failedCount} failed)."
        );
    }

    public Task<PurgeDlqResult> PurgeDlqAsync(PurgeDlqRequest request, CancellationToken ct = default)
    {
        var targetItems = _dlqStore.Values.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(request.DlqId))
        {
            targetItems = targetItems.Where(d => d.Id == request.DlqId);
        }
        else
        {
            if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
            {
                targetItems = targetItems.Where(d => d.TenantId == request.TenantId.Value);
            }
            if (!string.IsNullOrWhiteSpace(request.Topic))
            {
                targetItems = targetItems.Where(d => d.Topic.Equals(request.Topic, StringComparison.OrdinalIgnoreCase));
            }
        }

        var toPurge = targetItems.ToList();
        int count = 0;
        foreach (var item in toPurge)
        {
            if (_dlqStore.TryRemove(item.Id, out _))
            {
                count++;
            }
        }

        return Task.FromResult(new PurgeDlqResult(
            PurgedCount: count,
            Message: $"Successfully purged {count} events from Dead-Letter Queue."
        ));
    }

    public IReadOnlyList<MeshEventEnvelope> GetRecentRingBufferEvents(int count = 50)
    {
        return _ringBuffer.GetSnapshot(count);
    }

    public IReadOnlyDictionary<string, string> GetRedisPendingEntries(string streamKey, string consumerGroup)
    {
        string groupPelKey = $"{streamKey}:cg:{consumerGroup}";
        if (_redisPel.TryGetValue(groupPelKey, out var pel))
        {
            return new Dictionary<string, string>(pel);
        }
        return new Dictionary<string, string>();
    }

    public string GetNatsSubjectForEvent(Guid tenantId, string topic, int partitionIndex)
    {
        var sanitizedTopic = topic.Replace('/', '.').Replace(' ', '-').ToLowerInvariant();
        return $"events.{tenantId:N}.{sanitizedTopic}.p{partitionIndex}";
    }

    private void CleanupExpiredDeduplications(DateTime now)
    {
        if (_dedupCache.Count > 1000)
        {
            foreach (var kvp in _dedupCache)
            {
                if (kvp.Value.ExpiresAtUtc <= now)
                {
                    _dedupCache.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    private static bool IsTopicMatch(string pattern, string topic)
    {
        if (pattern == "*" || pattern == "#" || pattern == topic) return true;
        if (pattern.EndsWith(".*"))
        {
            var prefix = pattern[..^2];
            return topic.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.EndsWith(".#") || pattern.EndsWith(".>"))
        {
            var prefix = pattern[..^2];
            return topic.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return string.Equals(pattern, topic, StringComparison.OrdinalIgnoreCase);
    }
}
