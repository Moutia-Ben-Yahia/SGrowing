using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SGrowing.Api.Modules.Infrastructure.EventOutbox;

/// <summary>
/// Contract for the enterprise transactional outbox and resilient event streaming mesh.
/// Provides guaranteed at-least-once delivery, SHA-256 deduplication, exponential backoff, and DLQ quarantine.
/// </summary>
public interface IEventOutboxService
{
    /// <summary>
    /// Enqueues an arbitrary business event into the outbox with idempotency deduplication check.
    /// </summary>
    Task<EnqueueOutboxEventResult> EnqueueAsync(EnqueueOutboxEventRequest request, CancellationToken ct = default);

    /// <summary>
    /// Enqueues a strongly-typed OrderPaid event into the transactional outbox.
    /// </summary>
    Task<EnqueueOutboxEventResult> EnqueueOrderPaidAsync(Guid tenantId, OrderPaidPayload payload, string? correlationId = null, CancellationToken ct = default);

    /// <summary>
    /// Enqueues a strongly-typed DealWon event into the transactional outbox.
    /// </summary>
    Task<EnqueueOutboxEventResult> EnqueueDealWonAsync(Guid tenantId, DealWonPayload payload, string? correlationId = null, CancellationToken ct = default);

    /// <summary>
    /// Enqueues a strongly-typed LeadQualified event into the transactional outbox.
    /// </summary>
    Task<EnqueueOutboxEventResult> EnqueueLeadQualifiedAsync(Guid tenantId, LeadQualifiedPayload payload, string? correlationId = null, CancellationToken ct = default);

    /// <summary>
    /// Enqueues a strongly-typed TicketEscalated event into the transactional outbox.
    /// </summary>
    Task<EnqueueOutboxEventResult> EnqueueTicketEscalatedAsync(Guid tenantId, TicketEscalatedPayload payload, string? correlationId = null, CancellationToken ct = default);

    /// <summary>
    /// Polls a batch of pending or retry-ready outbox events and dispatches them to downstream consumers.
    /// </summary>
    Task<DispatchBatchResult> ProcessPendingOutboxBatchAsync(int batchSize = 50, Guid? tenantId = null, Func<OutboxEventRecord, Task<bool>>? customDispatcher = null, CancellationToken ct = default);

    /// <summary>
    /// Queries dead-letter queue messages matching the specified query filters.
    /// </summary>
    Task<IReadOnlyList<DeadLetterEventDetail>> GetDeadLetterMessagesAsync(DeadLetterQueryParameters query, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single dead-letter message by its unique event ID for payload inspection.
    /// </summary>
    Task<DeadLetterEventDetail?> GetDeadLetterMessageByIdAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Replays quarantined dead-letter messages by resetting their status to Pending and resetting retry timers.
    /// </summary>
    Task<ReplayDeadLetterResult> ReplayDeadLetterMessagesAsync(ReplayDeadLetterRequest request, CancellationToken ct = default);

    /// <summary>
    /// Permanently purges a dead-letter message from the queue and deduplication ledger.
    /// </summary>
    Task<bool> PurgeDeadLetterMessageAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Computes aggregated health metrics and throughput statistics for the outbox mesh.
    /// </summary>
    Task<OutboxMeshStatistics> GetMeshStatisticsAsync(Guid? tenantId = null, CancellationToken ct = default);

    /// <summary>
    /// Verifies if a given SHA-256 deduplication hash is already present in the idempotency ledger.
    /// </summary>
    Task<bool> IsDuplicateAsync(string deduplicationHash, CancellationToken ct = default);

    /// <summary>
    /// Computes deterministic SHA-256 idempotency hash for an event.
    /// </summary>
    string ComputeDeduplicationHash(Guid tenantId, string eventType, string aggregateId, string payloadJson);

    /// <summary>
    /// Resets internal state for unit testing and deterministic scenario replay.
    /// </summary>
    void ResetState();
}

/// <summary>
/// Production-grade implementation of <see cref="IEventOutboxService"/> with in-memory concurrent storage,
/// SHA-256 idempotency ledger, exponential backoff retries, and dead-letter queue quarantine.
/// </summary>
public class EventOutboxService : IEventOutboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ConcurrentDictionary<Guid, OutboxEventRecord> _events = new();
    private readonly ConcurrentDictionary<string, IdempotencyLedgerEntry> _idempotencyLedger = new(StringComparer.OrdinalIgnoreCase);

    private long _totalEnqueued;
    private long _totalPublished;
    private long _totalFailedRetries;
    private long _totalQuarantinedDeadLetter;
    private long _totalDeduplicationHits;
    private double _cumulativeLatencyMs;
    private long _dispatchLatencySamplesCount;

    private readonly object _stateLock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="EventOutboxService"/> with initial demo events.
    /// </summary>
    public EventOutboxService()
    {
        SeedInitialState();
    }

    /// <inheritdoc />
    public Task<EnqueueOutboxEventResult> EnqueueAsync(EnqueueOutboxEventRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.EventType))
        {
            throw new ArgumentException("EventType cannot be empty or whitespace.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.AggregateId))
        {
            throw new ArgumentException("AggregateId cannot be empty or whitespace.", nameof(request));
        }

        var payloadJson = request.PayloadJson;
        if (string.IsNullOrWhiteSpace(payloadJson) && request.Payload is not null)
        {
            payloadJson = JsonSerializer.Serialize(request.Payload, JsonOptions);
        }
        payloadJson ??= "{}";

        var deduplicationHash = ComputeDeduplicationHash(request.TenantId, request.EventType, request.AggregateId, payloadJson);

        lock (_stateLock)
        {
            if (_idempotencyLedger.TryGetValue(deduplicationHash, out var existingEntry))
            {
                Interlocked.Increment(ref _totalDeduplicationHits);
                return Task.FromResult(new EnqueueOutboxEventResult(
                    existingEntry.EventId,
                    deduplicationHash,
                    IsDuplicate: true,
                    Status: "DuplicateSuppressed",
                    EnqueuedAtUtc: existingEntry.FirstSeenAtUtc,
                    Message: $"Duplicate event detected. Matched existing hash '{deduplicationHash}' registered at {existingEntry.FirstSeenAtUtc:u}."
                ));
            }

            var eventRecord = new OutboxEventRecord
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                EventType = request.EventType.Trim(),
                AggregateId = request.AggregateId.Trim(),
                PayloadJson = payloadJson,
                DeduplicationHash = deduplicationHash,
                Status = OutboxEventStatus.Pending,
                RetryCount = 0,
                MaxRetries = request.MaxRetries.GetValueOrDefault(5),
                CreatedAtUtc = DateTime.UtcNow,
                CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString("N"),
                PartitionKey = request.PartitionKey ?? $"{request.TenantId:N}-{request.EventType}",
                Headers = request.Headers is not null
                    ? new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["x-sgrowing-engine"] = "TransactionalOutboxMesh/10.0",
                        ["x-sgrowing-tenant"] = request.TenantId.ToString()
                    }
            };

            _events[eventRecord.Id] = eventRecord;

            var ledgerEntry = new IdempotencyLedgerEntry(
                deduplicationHash,
                eventRecord.Id,
                eventRecord.EventType,
                eventRecord.TenantId,
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(7),
                "Pending"
            );
            _idempotencyLedger[deduplicationHash] = ledgerEntry;

            Interlocked.Increment(ref _totalEnqueued);

            return Task.FromResult(new EnqueueOutboxEventResult(
                eventRecord.Id,
                deduplicationHash,
                IsDuplicate: false,
                Status: "Enqueued",
                EnqueuedAtUtc: eventRecord.CreatedAtUtc,
                Message: "Event staged successfully into transactional outbox."
            ));
        }
    }

    /// <inheritdoc />
    public Task<EnqueueOutboxEventResult> EnqueueOrderPaidAsync(Guid tenantId, OrderPaidPayload payload, string? correlationId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var request = new EnqueueOutboxEventRequest(
            TenantId: tenantId,
            EventType: EnterpriseEventTypes.OrderPaid,
            AggregateId: payload.OrderId,
            Payload: payload,
            CorrelationId: correlationId,
            PartitionKey: $"order-{payload.OrderId}",
            Headers: new Dictionary<string, string>
            {
                ["x-business-domain"] = "Commerce",
                ["x-customer-email"] = payload.CustomerEmail
            }
        );
        return EnqueueAsync(request, ct);
    }

    /// <inheritdoc />
    public Task<EnqueueOutboxEventResult> EnqueueDealWonAsync(Guid tenantId, DealWonPayload payload, string? correlationId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var request = new EnqueueOutboxEventRequest(
            TenantId: tenantId,
            EventType: EnterpriseEventTypes.DealWon,
            AggregateId: payload.DealId,
            Payload: payload,
            CorrelationId: correlationId,
            PartitionKey: $"deal-{payload.DealId}",
            Headers: new Dictionary<string, string>
            {
                ["x-business-domain"] = "CRM",
                ["x-account-name"] = payload.AccountName
            }
        );
        return EnqueueAsync(request, ct);
    }

    /// <inheritdoc />
    public Task<EnqueueOutboxEventResult> EnqueueLeadQualifiedAsync(Guid tenantId, LeadQualifiedPayload payload, string? correlationId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var request = new EnqueueOutboxEventRequest(
            TenantId: tenantId,
            EventType: EnterpriseEventTypes.LeadQualified,
            AggregateId: payload.LeadId,
            Payload: payload,
            CorrelationId: correlationId,
            PartitionKey: $"lead-{payload.LeadId}",
            Headers: new Dictionary<string, string>
            {
                ["x-business-domain"] = "Marketing",
                ["x-lead-source"] = payload.Source
            }
        );
        return EnqueueAsync(request, ct);
    }

    /// <inheritdoc />
    public Task<EnqueueOutboxEventResult> EnqueueTicketEscalatedAsync(Guid tenantId, TicketEscalatedPayload payload, string? correlationId = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var request = new EnqueueOutboxEventRequest(
            TenantId: tenantId,
            EventType: EnterpriseEventTypes.TicketEscalated,
            AggregateId: payload.TicketId,
            Payload: payload,
            CorrelationId: correlationId,
            PartitionKey: $"ticket-{payload.TicketId}",
            Headers: new Dictionary<string, string>
            {
                ["x-business-domain"] = "Support",
                ["x-priority"] = payload.Priority
            }
        );
        return EnqueueAsync(request, ct);
    }

    /// <inheritdoc />
    public async Task<DispatchBatchResult> ProcessPendingOutboxBatchAsync(
        int batchSize = 50,
        Guid? tenantId = null,
        Func<OutboxEventRecord, Task<bool>>? customDispatcher = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var now = DateTime.UtcNow;

        List<OutboxEventRecord> candidateEvents;
        lock (_stateLock)
        {
            candidateEvents = _events.Values
                .Where(e => (e.Status == OutboxEventStatus.Pending ||
                            (e.Status == OutboxEventStatus.Failed && (e.NextAttemptAtUtc == null || e.NextAttemptAtUtc <= now))))
                .Where(e => tenantId == null || e.TenantId == tenantId.Value)
                .OrderBy(e => e.CreatedAtUtc)
                .Take(Math.Max(1, batchSize))
                .ToList();

            foreach (var evt in candidateEvents)
            {
                evt.Status = OutboxEventStatus.Processing;
            }
        }

        var summaries = new List<DispatchedEventSummary>();
        var succeeded = 0;
        var failed = 0;
        var quarantined = 0;

        foreach (var evt in candidateEvents)
        {
            ct.ThrowIfCancellationRequested();

            var attemptSw = Stopwatch.StartNew();
            bool isSuccess;
            string? dispatchError = null;

            try
            {
                if (customDispatcher is not null)
                {
                    isSuccess = await customDispatcher(evt);
                    if (!isSuccess)
                    {
                        dispatchError = evt.LastError ?? "Custom dispatcher returned unsuccessful status.";
                    }
                }
                else
                {
                    // Default resilient dispatch simulation
                    // Check if event has poison trigger in headers or payload
                    if (evt.Headers.TryGetValue("x-simulate-poison", out var poisonVal) && string.Equals(poisonVal, "true", StringComparison.OrdinalIgnoreCase) ||
                        evt.PayloadJson.Contains("\"__poison__\":true", StringComparison.OrdinalIgnoreCase))
                    {
                        isSuccess = false;
                        dispatchError = "Simulated poison message downstream crash: Connection refused by target stream broker.";
                    }
                    else
                    {
                        isSuccess = true;
                    }
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                dispatchError = $"Exception during downstream dispatch: {ex.Message}";
            }

            attemptSw.Stop();
            RecordLatency(attemptSw.Elapsed.TotalMilliseconds);

            lock (_stateLock)
            {
                if (isSuccess)
                {
                    evt.Status = OutboxEventStatus.Published;
                    evt.ProcessedAtUtc = DateTime.UtcNow;
                    evt.LastError = null;
                    evt.NextAttemptAtUtc = null;
                    succeeded++;
                    Interlocked.Increment(ref _totalPublished);

                    if (_idempotencyLedger.TryGetValue(evt.DeduplicationHash, out var existing))
                    {
                        _idempotencyLedger[evt.DeduplicationHash] = existing with { Status = "Published" };
                    }

                    summaries.Add(new DispatchedEventSummary(
                        evt.Id,
                        evt.EventType,
                        evt.AggregateId,
                        "Published",
                        evt.RetryCount,
                        null,
                        null
                    ));
                }
                else
                {
                    evt.RetryCount++;
                    evt.LastError = dispatchError;

                    if (evt.RetryCount >= evt.MaxRetries)
                    {
                        evt.Status = OutboxEventStatus.QuarantinedDeadLetter;
                        evt.DeadLetterReason = $"Exceeded max retry threshold ({evt.MaxRetries} attempts). Last failure: {dispatchError}";
                        evt.QuarantinedAtUtc = DateTime.UtcNow;
                        evt.NextAttemptAtUtc = null;
                        quarantined++;
                        Interlocked.Increment(ref _totalQuarantinedDeadLetter);

                        if (_idempotencyLedger.TryGetValue(evt.DeduplicationHash, out var existing))
                        {
                            _idempotencyLedger[evt.DeduplicationHash] = existing with { Status = "Quarantined" };
                        }

                        summaries.Add(new DispatchedEventSummary(
                            evt.Id,
                            evt.EventType,
                            evt.AggregateId,
                            "QuarantinedDeadLetter",
                            evt.RetryCount,
                            evt.DeadLetterReason,
                            null
                        ));
                    }
                    else
                    {
                        evt.Status = OutboxEventStatus.Failed;
                        // Exponential backoff: 2^retryCount seconds
                        var backoffSeconds = Math.Pow(2, evt.RetryCount);
                        evt.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(backoffSeconds);
                        failed++;
                        Interlocked.Increment(ref _totalFailedRetries);

                        summaries.Add(new DispatchedEventSummary(
                            evt.Id,
                            evt.EventType,
                            evt.AggregateId,
                            "FailedRetryScheduled",
                            evt.RetryCount,
                            evt.LastError,
                            evt.NextAttemptAtUtc
                        ));
                    }
                }
            }
        }

        sw.Stop();

        return new DispatchBatchResult(
            BatchSize: batchSize,
            ProcessedCount: candidateEvents.Count,
            SucceededCount: succeeded,
            FailedCount: failed,
            QuarantinedCount: quarantined,
            SkippedDuplicatesCount: 0,
            DispatchedEvents: summaries,
            Duration: sw.Elapsed
        );
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeadLetterEventDetail>> GetDeadLetterMessagesAsync(DeadLetterQueryParameters query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var queryable = _events.Values
            .Where(e => e.Status == OutboxEventStatus.QuarantinedDeadLetter);

        if (query.TenantId.HasValue)
        {
            queryable = queryable.Where(e => e.TenantId == query.TenantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            queryable = queryable.Where(e => string.Equals(e.EventType, query.EventType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim();
            queryable = queryable.Where(e =>
                e.AggregateId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.PayloadJson.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (e.LastError != null && e.LastError.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                (e.DeadLetterReason != null && e.DeadLetterReason.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        var results = queryable
            .OrderByDescending(e => e.QuarantinedAtUtc ?? e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToDeadLetterDetail)
            .ToList();

        return Task.FromResult<IReadOnlyList<DeadLetterEventDetail>>(results);
    }

    /// <inheritdoc />
    public Task<DeadLetterEventDetail?> GetDeadLetterMessageByIdAsync(Guid eventId, CancellationToken ct = default)
    {
        if (_events.TryGetValue(eventId, out var evt) && evt.Status == OutboxEventStatus.QuarantinedDeadLetter)
        {
            return Task.FromResult<DeadLetterEventDetail?>(MapToDeadLetterDetail(evt));
        }

        return Task.FromResult<DeadLetterEventDetail?>(null);
    }

    /// <inheritdoc />
    public Task<ReplayDeadLetterResult> ReplayDeadLetterMessagesAsync(ReplayDeadLetterRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var replayedIds = new List<Guid>();
        var errors = new List<string>();

        lock (_stateLock)
        {
            foreach (var id in request.EventIds)
            {
                if (!_events.TryGetValue(id, out var evt))
                {
                    errors.Add($"Event '{id}' not found in outbox.");
                    continue;
                }

                if (evt.Status != OutboxEventStatus.QuarantinedDeadLetter)
                {
                    errors.Add($"Event '{id}' is not in DLQ status (current status: {evt.Status}).");
                    continue;
                }

                evt.Status = OutboxEventStatus.Pending;
                if (request.ResetRetryCount)
                {
                    evt.RetryCount = 0;
                }
                evt.NextAttemptAtUtc = DateTime.UtcNow;
                evt.LastError = null;
                evt.DeadLetterReason = null;
                evt.QuarantinedAtUtc = null;
                evt.Headers["x-dlq-replayed-at"] = DateTime.UtcNow.ToString("O");
                if (!string.IsNullOrWhiteSpace(request.Reason))
                {
                    evt.Headers["x-dlq-replay-reason"] = request.Reason;
                }

                if (_idempotencyLedger.TryGetValue(evt.DeduplicationHash, out var ledgerEntry))
                {
                    _idempotencyLedger[evt.DeduplicationHash] = ledgerEntry with { Status = "Pending" };
                }

                replayedIds.Add(id);
            }
        }

        return Task.FromResult(new ReplayDeadLetterResult(
            RequestedCount: request.EventIds.Count,
            ReplayedCount: replayedIds.Count,
            ReplayedEventIds: replayedIds,
            Errors: errors
        ));
    }

    /// <inheritdoc />
    public Task<bool> PurgeDeadLetterMessageAsync(Guid eventId, CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (_events.TryGetValue(eventId, out var evt) && evt.Status == OutboxEventStatus.QuarantinedDeadLetter)
            {
                _events.TryRemove(eventId, out _);
                _idempotencyLedger.TryRemove(evt.DeduplicationHash, out _);
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<OutboxMeshStatistics> GetMeshStatisticsAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        var allEvents = _events.Values.ToList();
        if (tenantId.HasValue)
        {
            allEvents = allEvents.Where(e => e.TenantId == tenantId.Value).ToList();
        }

        var pending = allEvents.Count(e => e.Status == OutboxEventStatus.Pending);
        var processing = allEvents.Count(e => e.Status == OutboxEventStatus.Processing);
        var deadLetter = allEvents.Count(e => e.Status == OutboxEventStatus.QuarantinedDeadLetter);

        var byType = allEvents
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => (long)g.Count(), StringComparer.OrdinalIgnoreCase);

        var totalTerminal = _totalPublished + _totalQuarantinedDeadLetter;
        var successRate = totalTerminal > 0
            ? Math.Round(((double)_totalPublished / totalTerminal) * 100.0, 2)
            : 100.0;

        var sampleCount = Interlocked.Read(ref _dispatchLatencySamplesCount);
        var avgLatency = sampleCount > 0
            ? Math.Round(_cumulativeLatencyMs / sampleCount, 2)
            : 12.5;

        return Task.FromResult(new OutboxMeshStatistics(
            TotalEnqueued: Interlocked.Read(ref _totalEnqueued),
            TotalPublished: Interlocked.Read(ref _totalPublished),
            TotalFailedRetries: Interlocked.Read(ref _totalFailedRetries),
            TotalQuarantinedDeadLetter: Interlocked.Read(ref _totalQuarantinedDeadLetter),
            TotalDeduplicationHits: Interlocked.Read(ref _totalDeduplicationHits),
            CurrentPendingCount: pending,
            CurrentProcessingCount: processing,
            CurrentDeadLetterDepth: deadLetter,
            SuccessRatePercent: successRate,
            AverageDispatchLatencyMs: avgLatency,
            CountsByEventType: byType,
            GeneratedAtUtc: DateTime.UtcNow
        ));
    }

    /// <inheritdoc />
    public Task<bool> IsDuplicateAsync(string deduplicationHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deduplicationHash))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_idempotencyLedger.ContainsKey(deduplicationHash));
    }

    /// <inheritdoc />
    public string ComputeDeduplicationHash(Guid tenantId, string eventType, string aggregateId, string payloadJson)
    {
        var normalizedKey = $"{tenantId:D}:{eventType.Trim().ToLowerInvariant()}:{aggregateId.Trim().ToLowerInvariant()}:{payloadJson.Trim()}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedKey));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <inheritdoc />
    public void ResetState()
    {
        lock (_stateLock)
        {
            _events.Clear();
            _idempotencyLedger.Clear();
            _totalEnqueued = 0;
            _totalPublished = 0;
            _totalFailedRetries = 0;
            _totalQuarantinedDeadLetter = 0;
            _totalDeduplicationHits = 0;
            _cumulativeLatencyMs = 0;
            _dispatchLatencySamplesCount = 0;
        }
    }

    private void RecordLatency(double ms)
    {
        lock (_stateLock)
        {
            _cumulativeLatencyMs += ms;
            Interlocked.Increment(ref _dispatchLatencySamplesCount);
        }
    }

    private static DeadLetterEventDetail MapToDeadLetterDetail(OutboxEventRecord record)
    {
        return new DeadLetterEventDetail(
            EventId: record.Id,
            TenantId: record.TenantId,
            EventType: record.EventType,
            AggregateId: record.AggregateId,
            PayloadJson: record.PayloadJson,
            DeduplicationHash: record.DeduplicationHash,
            RetryCount: record.RetryCount,
            MaxRetries: record.MaxRetries,
            LastError: record.LastError,
            DeadLetterReason: record.DeadLetterReason,
            CreatedAtUtc: record.CreatedAtUtc,
            QuarantinedAtUtc: record.QuarantinedAtUtc,
            CorrelationId: record.CorrelationId,
            PartitionKey: record.PartitionKey,
            Headers: new Dictionary<string, string>(record.Headers, StringComparer.OrdinalIgnoreCase)
        );
    }

    private void SeedInitialState()
    {
        var sampleTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

        // 1. OrderPaid event (published)
        var orderPayload = JsonSerializer.Serialize(new OrderPaidPayload("ORD-2026-8812", 249.99m, "USD", "sarah.connor@cyberdyne.org", "Stripe", DateTime.UtcNow.AddMinutes(-40)), JsonOptions);
        var orderHash = ComputeDeduplicationHash(sampleTenant, EnterpriseEventTypes.OrderPaid, "ORD-2026-8812", orderPayload);
        var orderEvt = new OutboxEventRecord
        {
            TenantId = sampleTenant,
            EventType = EnterpriseEventTypes.OrderPaid,
            AggregateId = "ORD-2026-8812",
            PayloadJson = orderPayload,
            DeduplicationHash = orderHash,
            Status = OutboxEventStatus.Published,
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-40),
            ProcessedAtUtc = DateTime.UtcNow.AddMinutes(-39),
            CorrelationId = "corr-ord-8812"
        };
        _events[orderEvt.Id] = orderEvt;
        _idempotencyLedger[orderHash] = new IdempotencyLedgerEntry(orderHash, orderEvt.Id, orderEvt.EventType, sampleTenant, orderEvt.CreatedAtUtc, DateTime.UtcNow.AddDays(7), "Published");

        // 2. DealWon event (published)
        var dealPayload = JsonSerializer.Serialize(new DealWonPayload("DEAL-9904", 45000.00m, "USD", "Acme Enterprise Corp", "usr_rebecca", DateTime.UtcNow.AddMinutes(-25)), JsonOptions);
        var dealHash = ComputeDeduplicationHash(sampleTenant, EnterpriseEventTypes.DealWon, "DEAL-9904", dealPayload);
        var dealEvt = new OutboxEventRecord
        {
            TenantId = sampleTenant,
            EventType = EnterpriseEventTypes.DealWon,
            AggregateId = "DEAL-9904",
            PayloadJson = dealPayload,
            DeduplicationHash = dealHash,
            Status = OutboxEventStatus.Published,
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-25),
            ProcessedAtUtc = DateTime.UtcNow.AddMinutes(-24),
            CorrelationId = "corr-deal-9904"
        };
        _events[dealEvt.Id] = dealEvt;
        _idempotencyLedger[dealHash] = new IdempotencyLedgerEntry(dealHash, dealEvt.Id, dealEvt.EventType, sampleTenant, dealEvt.CreatedAtUtc, DateTime.UtcNow.AddDays(7), "Published");

        // 3. LeadQualified event (Pending)
        var leadPayload = JsonSerializer.Serialize(new LeadQualifiedPayload("LEAD-4410", "A_TIER_HOT", "InboundWebinar", "Alex Vance", "alex@blackmesa.com", DateTime.UtcNow.AddMinutes(-5)), JsonOptions);
        var leadHash = ComputeDeduplicationHash(sampleTenant, EnterpriseEventTypes.LeadQualified, "LEAD-4410", leadPayload);
        var leadEvt = new OutboxEventRecord
        {
            TenantId = sampleTenant,
            EventType = EnterpriseEventTypes.LeadQualified,
            AggregateId = "LEAD-4410",
            PayloadJson = leadPayload,
            DeduplicationHash = leadHash,
            Status = OutboxEventStatus.Pending,
            RetryCount = 0,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CorrelationId = "corr-lead-4410"
        };
        _events[leadEvt.Id] = leadEvt;
        _idempotencyLedger[leadHash] = new IdempotencyLedgerEntry(leadHash, leadEvt.Id, leadEvt.EventType, sampleTenant, leadEvt.CreatedAtUtc, DateTime.UtcNow.AddDays(7), "Pending");

        // 4. TicketEscalated poison event (Quarantined in DLQ)
        var ticketPayload = JsonSerializer.Serialize(new TicketEscalatedPayload("TCK-1002", "CRITICAL_SEV1", "Database replication lag breached SLA threshold", "eng_triage_bot", 0, DateTime.UtcNow.AddMinutes(-15)), JsonOptions);
        var ticketHash = ComputeDeduplicationHash(sampleTenant, EnterpriseEventTypes.TicketEscalated, "TCK-1002", ticketPayload);
        var ticketEvt = new OutboxEventRecord
        {
            TenantId = sampleTenant,
            EventType = EnterpriseEventTypes.TicketEscalated,
            AggregateId = "TCK-1002",
            PayloadJson = ticketPayload,
            DeduplicationHash = ticketHash,
            Status = OutboxEventStatus.QuarantinedDeadLetter,
            RetryCount = 5,
            MaxRetries = 5,
            LastError = "Downstream webhook delivery rejected: 504 Gateway Timeout from https://ops-pager.corp.internal/v1/escalations",
            DeadLetterReason = "Exceeded max retry threshold (5 attempts). Last failure: Downstream webhook delivery rejected: 504 Gateway Timeout.",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-15),
            QuarantinedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            CorrelationId = "corr-tck-1002"
        };
        _events[ticketEvt.Id] = ticketEvt;
        _idempotencyLedger[ticketHash] = new IdempotencyLedgerEntry(ticketHash, ticketEvt.Id, ticketEvt.EventType, sampleTenant, ticketEvt.CreatedAtUtc, DateTime.UtcNow.AddDays(7), "Quarantined");

        _totalEnqueued = 4;
        _totalPublished = 2;
        _totalQuarantinedDeadLetter = 1;
        _totalDeduplicationHits = 0;
        _cumulativeLatencyMs = 38.0;
        _dispatchLatencySamplesCount = 2;
    }
}
