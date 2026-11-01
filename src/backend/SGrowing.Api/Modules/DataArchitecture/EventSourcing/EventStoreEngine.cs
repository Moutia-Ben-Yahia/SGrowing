using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.DataArchitecture.EventSourcing;

public interface IEventStoreEngine
{
    Task AppendEventsAsync(string aggregateId, long expectedVersion, IEnumerable<DomainEvent> events, CancellationToken ct = default);
    Task<IReadOnlyList<DomainEvent>> GetEventStreamAsync(string aggregateId, long fromVersion = 0, CancellationToken ct = default);
    Task SaveSnapshotAsync(SnapshotRecord snapshot, CancellationToken ct = default);
    Task<SnapshotRecord?> GetLatestSnapshotAsync(string aggregateId, CancellationToken ct = default);
    Task<TAggregate?> ReconstructAggregateAtTimeAsync<TAggregate>(string aggregateId, DateTimeOffset pointInTime, CancellationToken ct = default) where TAggregate : AggregateRoot, new();
    Task<TAggregate?> LoadAggregateAsync<TAggregate>(string aggregateId, CancellationToken ct = default) where TAggregate : AggregateRoot, new();
    Task<IReadOnlyList<DomainEvent>> GetAllEventsAsync(CancellationToken ct = default);
    int ActiveStreamsCount { get; }
    int TotalSnapshotsCount { get; }
    long TotalEventsCount { get; }
}

/// <summary>
/// Thread-safe in-memory event store engine using modern .NET 10 System.Threading.Lock.
/// Enforces optimistic concurrency, manages aggregate event streams, point-in-time time travel,
/// snapshots, and optional automatic dispatch to CQRS projection and CDC outbox streamer.
/// </summary>
public sealed class EventStoreEngine : IEventStoreEngine
{
    private readonly System.Threading.Lock _lock = new();
    private readonly Dictionary<string, List<DomainEvent>> _streams = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SnapshotRecord> _snapshots = new(StringComparer.Ordinal);
    private readonly List<DomainEvent> _globalTimeline = [];

    private readonly ICqrsProjectionEngine? _projectionEngine;
    private readonly IOutboxCdcStreamingService? _cdcStreamingService;

    public EventStoreEngine(
        ICqrsProjectionEngine? projectionEngine = null,
        IOutboxCdcStreamingService? cdcStreamingService = null)
    {
        _projectionEngine = projectionEngine;
        _cdcStreamingService = cdcStreamingService;
    }

    public int ActiveStreamsCount
    {
        get
        {
            lock (_lock)
            {
                return _streams.Count;
            }
        }
    }

    public int TotalSnapshotsCount
    {
        get
        {
            lock (_lock)
            {
                return _snapshots.Count;
            }
        }
    }

    public long TotalEventsCount
    {
        get
        {
            lock (_lock)
            {
                return _globalTimeline.Count;
            }
        }
    }

    public async Task AppendEventsAsync(
        string aggregateId,
        long expectedVersion,
        IEnumerable<DomainEvent> events,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentNullException.ThrowIfNull(events);

        var eventList = events.ToList();
        if (eventList.Count == 0)
            return;

        List<DomainEvent> committedEvents = [];

        lock (_lock)
        {
            if (!_streams.TryGetValue(aggregateId, out var stream))
            {
                stream = [];
                _streams[aggregateId] = stream;
            }

            var currentVersion = stream.Count > 0 ? stream[^1].Version : 0;
            if (expectedVersion != currentVersion)
            {
                throw new OptimisticConcurrencyException(aggregateId, expectedVersion, currentVersion);
            }

            var nextVersion = currentVersion;
            foreach (var @event in eventList)
            {
                nextVersion++;
                var versionedEvent = @event with
                {
                    Version = nextVersion,
                    AggregateId = aggregateId
                };

                stream.Add(versionedEvent);
                _globalTimeline.Add(versionedEvent);
                committedEvents.Add(versionedEvent);
            }
        }

        // Forward to CQRS projections and CDC streaming service
        foreach (var commEvent in committedEvents)
        {
            if (_projectionEngine != null)
            {
                await _projectionEngine.ProjectEventAsync(commEvent, ct);
            }

            if (_cdcStreamingService != null)
            {
                await _cdcStreamingService.PublishEventAsync(commEvent, null, ct);
            }
        }
    }

    public Task<IReadOnlyList<DomainEvent>> GetEventStreamAsync(
        string aggregateId,
        long fromVersion = 0,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        lock (_lock)
        {
            if (!_streams.TryGetValue(aggregateId, out var stream))
            {
                return Task.FromResult<IReadOnlyList<DomainEvent>>([]);
            }

            IReadOnlyList<DomainEvent> result = stream
                .Where(e => e.Version >= fromVersion)
                .OrderBy(e => e.Version)
                .ToList();

            return Task.FromResult(result);
        }
    }

    public Task SaveSnapshotAsync(SnapshotRecord snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshot.AggregateId);

        lock (_lock)
        {
            _snapshots[snapshot.AggregateId] = snapshot;
        }

        return Task.CompletedTask;
    }

    public Task<SnapshotRecord?> GetLatestSnapshotAsync(string aggregateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        lock (_lock)
        {
            _snapshots.TryGetValue(aggregateId, out var snapshot);
            return Task.FromResult(snapshot);
        }
    }

    public Task<TAggregate?> ReconstructAggregateAtTimeAsync<TAggregate>(
        string aggregateId,
        DateTimeOffset pointInTime,
        CancellationToken ct = default) where TAggregate : AggregateRoot, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        List<DomainEvent> eventsBeforeTimestamp;
        lock (_lock)
        {
            if (!_streams.TryGetValue(aggregateId, out var stream))
            {
                return Task.FromResult<TAggregate?>(null);
            }

            eventsBeforeTimestamp = stream
                .Where(e => e.TimestampUtc <= pointInTime)
                .OrderBy(e => e.Version)
                .ToList();
        }

        if (eventsBeforeTimestamp.Count == 0)
        {
            return Task.FromResult<TAggregate?>(null);
        }

        var aggregate = new TAggregate();
        aggregate.LoadFromHistory(eventsBeforeTimestamp);
        return Task.FromResult<TAggregate?>(aggregate);
    }

    public async Task<TAggregate?> LoadAggregateAsync<TAggregate>(
        string aggregateId,
        CancellationToken ct = default) where TAggregate : AggregateRoot, new()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);

        var snapshot = await GetLatestSnapshotAsync(aggregateId, ct);
        long fromVersion = 0;
        var aggregate = new TAggregate();

        if (snapshot != null)
        {
            aggregate.RestoreFromSnapshot(snapshot);
            fromVersion = snapshot.Version + 1;
        }

        var subsequentEvents = await GetEventStreamAsync(aggregateId, fromVersion, ct);
        if (snapshot == null && subsequentEvents.Count == 0)
        {
            return null;
        }

        if (subsequentEvents.Count > 0)
        {
            aggregate.LoadFromHistory(subsequentEvents);
        }

        return aggregate;
    }

    public Task<IReadOnlyList<DomainEvent>> GetAllEventsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            IReadOnlyList<DomainEvent> copy = [.. _globalTimeline];
            return Task.FromResult(copy);
        }
    }
}
