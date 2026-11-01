using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.DataArchitecture.EventSourcing;

public sealed record CdcOutboxMessage(
    long Offset,
    int Partition,
    string Topic,
    DomainEvent Event,
    DateTimeOffset EnqueuedUtc
);

public sealed record PartitionOffsetInfo(
    int Partition,
    long HighWatermarkOffset,
    int MessageCount
);

public sealed record ConsumerGroupLagInfo(
    string ConsumerGroup,
    int Partition,
    long CommittedOffset,
    long HighWatermarkOffset,
    long Lag
);

public sealed record DlqRecord(
    Guid DlqId,
    string ConsumerGroup,
    CdcOutboxMessage Message,
    string ErrorReason,
    int RetryCount,
    DateTimeOffset FailedAtUtc
);

public sealed record CdcMetrics(
    long TotalMessagesProduced,
    long TotalMessagesCommitted,
    long TotalMessagesInDlq,
    IReadOnlyList<PartitionOffsetInfo> Partitions,
    IReadOnlyList<ConsumerGroupLagInfo> ConsumerGroupLags
);

public interface IOutboxCdcStreamingService
{
    Task<CdcOutboxMessage> PublishEventAsync(DomainEvent @event, string? topic = null, CancellationToken ct = default);
    Task RegisterConsumerGroupAsync(string consumerGroup, CancellationToken ct = default);
    Task CommitOffsetAsync(string consumerGroup, int partition, long offset, CancellationToken ct = default);
    Task<IReadOnlyList<CdcOutboxMessage>> FetchBatchAsync(string consumerGroup, int partition, int maxBatchSize = 100, CancellationToken ct = default);
    Task<int> ConsumeAndProcessBatchAsync(string consumerGroup, int partition, int maxBatchSize, Func<CdcOutboxMessage, Task<bool>> handler, int maxRetries = 3, CancellationToken ct = default);
    Task<bool> RouteToDlqAsync(string consumerGroup, CdcOutboxMessage message, string errorReason, int retryCount = 1, CancellationToken ct = default);
    Task<IReadOnlyList<DlqRecord>> GetDlqMessagesAsync(string? consumerGroup = null, CancellationToken ct = default);
    Task<bool> ReplayDlqMessageAsync(Guid dlqId, Func<CdcOutboxMessage, Task<bool>> consumerHandler, CancellationToken ct = default);
    Task<CdcMetrics> GetMetricsAsync(CancellationToken ct = default);
    int PartitionCount { get; }
}

/// <summary>
/// Simulates a high-throughput Change Data Capture (CDC) streaming pipeline to Kafka / Redis Streams / Webhooks.
/// Manages partitioned offsets, consumer subscription groups, stream lag, and Dead Letter Queue (DLQ).
/// </summary>
public sealed class OutboxCdcStreamingService : IOutboxCdcStreamingService
{
    private readonly System.Threading.Lock _lock = new();

    public int PartitionCount { get; }

    // Partition streams: partitionId -> List of messages
    private readonly Dictionary<int, List<CdcOutboxMessage>> _partitionStreams = new();
    private readonly Dictionary<int, long> _partitionWatermarks = new();

    // Consumer Groups: consumerGroup -> (partition -> committed offset)
    private readonly Dictionary<string, Dictionary<int, long>> _consumerOffsets = new(StringComparer.OrdinalIgnoreCase);

    // Dead Letter Queue
    private readonly List<DlqRecord> _dlqRecords = [];

    private long _totalMessagesProduced;
    private long _totalMessagesCommitted;

    public OutboxCdcStreamingService(int partitionCount = 4)
    {
        if (partitionCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(partitionCount), "Partition count must be at least 1.");

        PartitionCount = partitionCount;

        for (var i = 0; i < PartitionCount; i++)
        {
            _partitionStreams[i] = [];
            _partitionWatermarks[i] = 0;
        }
    }

    public int GetPartitionForAggregate(string aggregateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        // Stable deterministic hash code across runtimes
        uint hash = 2166136261;
        foreach (var c in aggregateId)
        {
            hash = (hash ^ c) * 16777619;
        }
        return (int)(hash % (uint)PartitionCount);
    }

    public Task<CdcOutboxMessage> PublishEventAsync(
        DomainEvent @event,
        string? topic = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var resolvedTopic = string.IsNullOrWhiteSpace(topic)
            ? $"sgrowing.events.{@event.AggregateType.ToLowerInvariant()}"
            : topic;

        var partition = GetPartitionForAggregate(@event.AggregateId);

        lock (_lock)
        {
            _partitionWatermarks[partition]++;
            var offset = _partitionWatermarks[partition];

            var message = new CdcOutboxMessage(
                Offset: offset,
                Partition: partition,
                Topic: resolvedTopic,
                Event: @event,
                EnqueuedUtc: DateTimeOffset.UtcNow
            );

            _partitionStreams[partition].Add(message);
            _totalMessagesProduced++;

            return Task.FromResult(message);
        }
    }

    public Task RegisterConsumerGroupAsync(string consumerGroup, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);

        lock (_lock)
        {
            if (!_consumerOffsets.ContainsKey(consumerGroup))
            {
                var dict = new Dictionary<int, long>();
                for (var i = 0; i < PartitionCount; i++)
                {
                    dict[i] = 0;
                }
                _consumerOffsets[consumerGroup] = dict;
            }
        }

        return Task.CompletedTask;
    }

    public Task CommitOffsetAsync(
        string consumerGroup,
        int partition,
        long offset,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);
        if (partition < 0 || partition >= PartitionCount)
            throw new ArgumentOutOfRangeException(nameof(partition));

        lock (_lock)
        {
            if (!_consumerOffsets.TryGetValue(consumerGroup, out var partitions))
            {
                partitions = [];
                for (var i = 0; i < PartitionCount; i++)
                {
                    partitions[i] = 0;
                }
                _consumerOffsets[consumerGroup] = partitions;
            }

            if (offset > partitions[partition])
            {
                partitions[partition] = offset;
                _totalMessagesCommitted++;
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CdcOutboxMessage>> FetchBatchAsync(
        string consumerGroup,
        int partition,
        int maxBatchSize = 100,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);
        if (partition < 0 || partition >= PartitionCount)
            throw new ArgumentOutOfRangeException(nameof(partition));
        if (maxBatchSize <= 0)
            return Task.FromResult<IReadOnlyList<CdcOutboxMessage>>([]);

        lock (_lock)
        {
            long committedOffset = 0;
            if (_consumerOffsets.TryGetValue(consumerGroup, out var partitions) &&
                partitions.TryGetValue(partition, out var comm))
            {
                committedOffset = comm;
            }

            var stream = _partitionStreams[partition];
            IReadOnlyList<CdcOutboxMessage> batch = stream
                .Where(m => m.Offset > committedOffset)
                .OrderBy(m => m.Offset)
                .Take(maxBatchSize)
                .ToList();

            return Task.FromResult(batch);
        }
    }

    public async Task<int> ConsumeAndProcessBatchAsync(
        string consumerGroup,
        int partition,
        int maxBatchSize,
        Func<CdcOutboxMessage, Task<bool>> handler,
        int maxRetries = 3,
        CancellationToken ct = default)
    {
        var messages = await FetchBatchAsync(consumerGroup, partition, maxBatchSize, ct);
        if (messages.Count == 0)
            return 0;

        var processedCount = 0;

        foreach (var message in messages)
        {
            var success = false;
            var attempts = 0;
            string? lastError = null;

            while (!success && attempts < maxRetries)
            {
                attempts++;
                try
                {
                    success = await handler(message);
                    if (!success)
                    {
                        lastError = $"Consumer handler returned false on attempt {attempts}";
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }

            if (success)
            {
                await CommitOffsetAsync(consumerGroup, partition, message.Offset, ct);
                processedCount++;
            }
            else
            {
                // Route unprocessable message to DLQ and advance offset to avoid partition head-of-line stall
                await RouteToDlqAsync(consumerGroup, message, lastError ?? "Unknown consumer error", attempts, ct);
                await CommitOffsetAsync(consumerGroup, partition, message.Offset, ct);
            }
        }

        return processedCount;
    }

    public Task<bool> RouteToDlqAsync(
        string consumerGroup,
        CdcOutboxMessage message,
        string errorReason,
        int retryCount = 1,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerGroup);
        ArgumentNullException.ThrowIfNull(message);

        var dlqRecord = new DlqRecord(
            DlqId: Guid.NewGuid(),
            ConsumerGroup: consumerGroup,
            Message: message,
            ErrorReason: errorReason,
            RetryCount: retryCount,
            FailedAtUtc: DateTimeOffset.UtcNow
        );

        lock (_lock)
        {
            _dlqRecords.Add(dlqRecord);
        }

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<DlqRecord>> GetDlqMessagesAsync(
        string? consumerGroup = null,
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            IEnumerable<DlqRecord> query = _dlqRecords;
            if (!string.IsNullOrWhiteSpace(consumerGroup))
            {
                query = query.Where(d => string.Equals(d.ConsumerGroup, consumerGroup, StringComparison.OrdinalIgnoreCase));
            }

            IReadOnlyList<DlqRecord> result = [.. query];
            return Task.FromResult(result);
        }
    }

    public async Task<bool> ReplayDlqMessageAsync(
        Guid dlqId,
        Func<CdcOutboxMessage, Task<bool>> consumerHandler,
        CancellationToken ct = default)
    {
        DlqRecord? target;
        lock (_lock)
        {
            target = _dlqRecords.FirstOrDefault(d => d.DlqId == dlqId);
        }

        if (target == null)
            return false;

        bool success;
        try
        {
            success = await consumerHandler(target.Message);
        }
        catch
        {
            success = false;
        }

        if (success)
        {
            lock (_lock)
            {
                _dlqRecords.RemoveAll(d => d.DlqId == dlqId);
            }
            return true;
        }

        return false;
    }

    public Task<CdcMetrics> GetMetricsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            var partitionInfos = new List<PartitionOffsetInfo>(PartitionCount);
            for (var p = 0; p < PartitionCount; p++)
            {
                partitionInfos.Add(new PartitionOffsetInfo(
                    Partition: p,
                    HighWatermarkOffset: _partitionWatermarks[p],
                    MessageCount: _partitionStreams[p].Count
                ));
            }

            var lagInfos = new List<ConsumerGroupLagInfo>();
            foreach (var (group, pOffsets) in _consumerOffsets)
            {
                for (var p = 0; p < PartitionCount; p++)
                {
                    pOffsets.TryGetValue(p, out var comm);
                    var hwm = _partitionWatermarks[p];
                    var lag = Math.Max(0, hwm - comm);

                    lagInfos.Add(new ConsumerGroupLagInfo(
                        ConsumerGroup: group,
                        Partition: p,
                        CommittedOffset: comm,
                        HighWatermarkOffset: hwm,
                        Lag: lag
                    ));
                }
            }

            var metrics = new CdcMetrics(
                TotalMessagesProduced: _totalMessagesProduced,
                TotalMessagesCommitted: _totalMessagesCommitted,
                TotalMessagesInDlq: _dlqRecords.Count,
                Partitions: partitionInfos,
                ConsumerGroupLags: lagInfos
            );

            return Task.FromResult(metrics);
        }
    }
}
