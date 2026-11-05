using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Common.Streaming;

public record StreamMessage<T>(
    string MessageId,
    string Topic,
    int PartitionId,
    long Offset,
    string PartitionKey,
    T Payload,
    DateTime PublishedAtUtc
);

public record PublishStreamResult(
    string MessageId,
    string Topic,
    int PartitionId,
    long Offset,
    DateTime PublishedAtUtc
);

public record DeadLetterStreamMessage(
    string MessageId,
    string OriginalTopic,
    int OriginalPartitionId,
    string PartitionKey,
    string RawPayload,
    string FailureReason,
    DateTime QuarantinedAtUtc
);

public interface IEventStreamingMesh
{
    int GetPartitionCount(string topic);
    int ComputePartition(string topic, string partitionKey);

    Task<PublishStreamResult> PublishEventAsync<T>(
        string topic,
        string partitionKey,
        T payload,
        CancellationToken ct = default);

    Task<IReadOnlyList<StreamMessage<T>>> ConsumePartitionBatchAsync<T>(
        string topic,
        int partitionId,
        long fromOffset,
        int maxBatchSize = 100,
        CancellationToken ct = default);

    Task CommitConsumerOffsetAsync(
        string consumerGroup,
        string topic,
        int partitionId,
        long offset,
        CancellationToken ct = default);

    Task<long> GetConsumerOffsetAsync(
        string consumerGroup,
        string topic,
        int partitionId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DeadLetterStreamMessage>> GetDeadLetterMessagesAsync(
        string topic,
        int maxCount = 50,
        CancellationToken ct = default);
}

/// <summary>
/// High-throughput partitioned event streaming mesh.
/// Guarantees deterministic partition key routing, strict FIFO ordered delivery per partition,
/// consumer group offset commits, and poison message DLQ quarantine (PRD Section 5.10 & 9.1).
/// </summary>
public class EventStreamingMeshService : IEventStreamingMesh
{
    private readonly ILogger<EventStreamingMeshService>? _logger;
    private const int DefaultPartitionCount = 8;

    // Topic -> PartitionId -> List<RawRecord>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, List<PartitionRecord>>> _topicPartitions = new();

    // ConsumerGroup:Topic:PartitionId -> CommittedOffset
    private readonly ConcurrentDictionary<string, long> _consumerOffsets = new();

    // Topic -> List<DeadLetterStreamMessage>
    private readonly ConcurrentDictionary<string, List<DeadLetterStreamMessage>> _deadLetterQueues = new();

    private readonly ConcurrentDictionary<string, int> _topicPartitionCounts = new();

    public EventStreamingMeshService(ILogger<EventStreamingMeshService>? logger = null)
    {
        _logger = logger;
    }

    public int GetPartitionCount(string topic)
    {
        return _topicPartitionCounts.GetOrAdd(topic.ToLowerInvariant(), _ => DefaultPartitionCount);
    }

    public void ConfigurePartitionCount(string topic, int partitions)
    {
        _topicPartitionCounts[topic.ToLowerInvariant()] = Math.Max(1, partitions);
    }

    public int ComputePartition(string topic, string partitionKey)
    {
        var partitionCount = GetPartitionCount(topic);
        if (string.IsNullOrEmpty(partitionKey)) return 0;

        // Deterministic Murmur-like 32-bit FNV-1a Hash
        uint hash = 2166136261;
        var bytes = Encoding.UTF8.GetBytes(partitionKey.Trim());
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= 16777619;
        }

        return (int)(hash % (uint)partitionCount);
    }

    public Task<PublishStreamResult> PublishEventAsync<T>(
        string topic,
        string partitionKey,
        T payload,
        CancellationToken ct = default)
    {
        var safeTopic = topic.Trim().ToLowerInvariant();
        var partitionId = ComputePartition(safeTopic, partitionKey);
        var partitions = _topicPartitions.GetOrAdd(safeTopic, _ => new ConcurrentDictionary<int, List<PartitionRecord>>());
        var recordList = partitions.GetOrAdd(partitionId, _ => new List<PartitionRecord>());

        var messageId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(payload);

        long assignedOffset;
        lock (recordList)
        {
            assignedOffset = recordList.Count;
            var record = new PartitionRecord(messageId, safeTopic, partitionId, assignedOffset, partitionKey, json, now);
            recordList.Add(record);
        }

        _logger?.LogDebug("Published stream message {MessageId} on {Topic}[{Partition}] at offset {Offset}",
            messageId, safeTopic, partitionId, assignedOffset);

        return Task.FromResult(new PublishStreamResult(messageId, safeTopic, partitionId, assignedOffset, now));
    }

    public Task<IReadOnlyList<StreamMessage<T>>> ConsumePartitionBatchAsync<T>(
        string topic,
        int partitionId,
        long fromOffset,
        int maxBatchSize = 100,
        CancellationToken ct = default)
    {
        var safeTopic = topic.Trim().ToLowerInvariant();
        var result = new List<StreamMessage<T>>();

        if (!_topicPartitions.TryGetValue(safeTopic, out var partitions) ||
            !partitions.TryGetValue(partitionId, out var recordList))
        {
            return Task.FromResult<IReadOnlyList<StreamMessage<T>>>(result);
        }

        List<PartitionRecord> slice;
        lock (recordList)
        {
            if (fromOffset < 0 || fromOffset >= recordList.Count)
            {
                return Task.FromResult<IReadOnlyList<StreamMessage<T>>>(result);
            }

            int count = (int)Math.Min(maxBatchSize, recordList.Count - fromOffset);
            slice = recordList.GetRange((int)fromOffset, count);
        }

        foreach (var r in slice)
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<T>(r.RawJson);
                if (deserialized != null)
                {
                    result.Add(new StreamMessage<T>(
                        MessageId: r.MessageId,
                        Topic: r.Topic,
                        PartitionId: r.PartitionId,
                        Offset: r.Offset,
                        PartitionKey: r.PartitionKey,
                        Payload: deserialized,
                        PublishedAtUtc: r.PublishedAtUtc
                    ));
                }
            }
            catch (Exception ex)
            {
                // Poison message -> Quarantine to Dead Letter Queue
                QuarantineDeadLetter(r, ex.Message);
            }
        }

        return Task.FromResult<IReadOnlyList<StreamMessage<T>>>(result);
    }

    public Task CommitConsumerOffsetAsync(
        string consumerGroup,
        string topic,
        int partitionId,
        long offset,
        CancellationToken ct = default)
    {
        var key = $"{consumerGroup.Trim().ToLowerInvariant()}:{topic.Trim().ToLowerInvariant()}:{partitionId}";
        _consumerOffsets.AddOrUpdate(key, offset, (_, old) => Math.Max(old, offset));
        return Task.CompletedTask;
    }

    public Task<long> GetConsumerOffsetAsync(
        string consumerGroup,
        string topic,
        int partitionId,
        CancellationToken ct = default)
    {
        var key = $"{consumerGroup.Trim().ToLowerInvariant()}:{topic.Trim().ToLowerInvariant()}:{partitionId}";
        _consumerOffsets.TryGetValue(key, out var offset);
        return Task.FromResult(offset);
    }

    public Task<IReadOnlyList<DeadLetterStreamMessage>> GetDeadLetterMessagesAsync(
        string topic,
        int maxCount = 50,
        CancellationToken ct = default)
    {
        var safeTopic = topic.Trim().ToLowerInvariant();
        if (!_deadLetterQueues.TryGetValue(safeTopic, out var dlq))
        {
            return Task.FromResult<IReadOnlyList<DeadLetterStreamMessage>>(Array.Empty<DeadLetterStreamMessage>());
        }

        lock (dlq)
        {
            return Task.FromResult<IReadOnlyList<DeadLetterStreamMessage>>(dlq.Take(maxCount).ToList());
        }
    }

    private void QuarantineDeadLetter(PartitionRecord r, string reason)
    {
        var dlq = _deadLetterQueues.GetOrAdd(r.Topic, _ => new List<DeadLetterStreamMessage>());
        lock (dlq)
        {
            dlq.Add(new DeadLetterStreamMessage(
                MessageId: r.MessageId,
                OriginalTopic: r.Topic,
                OriginalPartitionId: r.PartitionId,
                PartitionKey: r.PartitionKey,
                RawPayload: r.RawJson,
                FailureReason: reason,
                QuarantinedAtUtc: DateTime.UtcNow
            ));
        }

        _logger?.LogWarning("Quarantined poison message {MessageId} from {Topic}[{Partition}] offset {Offset}. Reason: {Reason}",
            r.MessageId, r.Topic, r.PartitionId, r.Offset, reason);
    }

    private record PartitionRecord(
        string MessageId,
        string Topic,
        int PartitionId,
        long Offset,
        string PartitionKey,
        string RawJson,
        DateTime PublishedAtUtc
    );
}
