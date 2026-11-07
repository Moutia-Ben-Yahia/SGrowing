using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.EventOutbox;

/// <summary>
/// Execution and lifecycle states of an event in the transactional outbox mesh.
/// </summary>
public enum OutboxEventStatus
{
    /// <summary>
    /// Event has been committed to the outbox and is pending worker dispatch.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Event is actively being processed and dispatched by an outbox worker.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Event has been successfully delivered and acknowledged downstream.
    /// </summary>
    Published = 2,

    /// <summary>
    /// Temporary delivery failure; scheduled for retry using exponential backoff.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Message exceeded maximum retries or was identified as poison; quarantined to Dead Letter Queue (DLQ).
    /// </summary>
    QuarantinedDeadLetter = 4,

    /// <summary>
    /// Manually or automatically replayed from DLQ back into the active processing queue.
    /// </summary>
    Replayed = 5
}

/// <summary>
/// Well-known enterprise domain event types across the SGrowing ecosystem.
/// </summary>
public static class EnterpriseEventTypes
{
    /// <summary>
    /// Triggered when customer order payment is confirmed.
    /// </summary>
    public const string OrderPaid = "OrderPaid";

    /// <summary>
    /// Triggered when a sales deal transitions to won status.
    /// </summary>
    public const string DealWon = "DealWon";

    /// <summary>
    /// Triggered when inbound lead exceeds qualification threshold.
    /// </summary>
    public const string LeadQualified = "LeadQualified";

    /// <summary>
    /// Triggered when an omnichannel support ticket breaches SLA and escalates.
    /// </summary>
    public const string TicketEscalated = "TicketEscalated";
}

/// <summary>
/// Record representing an outbox event tracked within the transactional outbox ledger.
/// </summary>
public class OutboxEventRecord
{
    /// <summary>
    /// Unique identifier for the event.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Multi-tenant identifier owner of this event.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Domain event type name (e.g., OrderPaid, DealWon).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Aggregate root entity identifier that emitted this event.
    /// </summary>
    public string AggregateId { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON payload of the event body.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 idempotency hash calculated from TenantId, EventType, AggregateId, and PayloadJson.
    /// </summary>
    public string DeduplicationHash { get; set; } = string.Empty;

    /// <summary>
    /// Current lifecycle state of the outbox event.
    /// </summary>
    public OutboxEventStatus Status { get; set; } = OutboxEventStatus.Pending;

    /// <summary>
    /// Number of dispatch attempts made.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Maximum retry attempts before routing to Dead Letter Queue.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Diagnostic message or stack trace of the last failure.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Timestamp when event was originally staged in UTC.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when event was successfully published in UTC.
    /// </summary>
    public DateTime? ProcessedAtUtc { get; set; }

    /// <summary>
    /// Timestamp when the next dispatch attempt is allowed (governed by exponential backoff).
    /// </summary>
    public DateTime? NextAttemptAtUtc { get; set; }

    /// <summary>
    /// Distributed tracing correlation identifier.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Partition key for ordered streaming mesh distribution.
    /// </summary>
    public string? PartitionKey { get; set; }

    /// <summary>
    /// Custom metadata and transport headers.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Reason explaining why the event was moved to the Dead Letter Queue.
    /// </summary>
    public string? DeadLetterReason { get; set; }

    /// <summary>
    /// Timestamp when the event was quarantined in UTC.
    /// </summary>
    public DateTime? QuarantinedAtUtc { get; set; }
}

/// <summary>
/// Entry within the SHA-256 idempotency deduplication ledger.
/// </summary>
public record IdempotencyLedgerEntry(
    string DeduplicationHash,
    Guid EventId,
    string EventType,
    Guid TenantId,
    DateTime FirstSeenAtUtc,
    DateTime ExpiresAtUtc,
    string Status
);

/// <summary>
/// Request payload to stage an event in the transactional outbox.
/// </summary>
public record EnqueueOutboxEventRequest(
    Guid TenantId,
    string EventType,
    string AggregateId,
    object? Payload = null,
    string? PayloadJson = null,
    string? CorrelationId = null,
    string? PartitionKey = null,
    int? MaxRetries = null,
    Dictionary<string, string>? Headers = null
);

/// <summary>
/// Result returned after attempting to enqueue an outbox event.
/// </summary>
public record EnqueueOutboxEventResult(
    Guid EventId,
    string DeduplicationHash,
    bool IsDuplicate,
    string Status,
    DateTime EnqueuedAtUtc,
    string Message
);

/// <summary>
/// Parameters for batch polling and dispatching outbox events.
/// </summary>
public record DispatchBatchRequest(
    int BatchSize = 50,
    Guid? TenantId = null,
    string? EventType = null
);

/// <summary>
/// Summary of an individual event dispatch outcome.
/// </summary>
public record DispatchedEventSummary(
    Guid EventId,
    string EventType,
    string AggregateId,
    string Status,
    int RetryCount,
    string? Error,
    DateTime? NextAttemptAtUtc
);

/// <summary>
/// Aggregate result of an outbox batch dispatch execution.
/// </summary>
public record DispatchBatchResult(
    int BatchSize,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount,
    int QuarantinedCount,
    int SkippedDuplicatesCount,
    List<DispatchedEventSummary> DispatchedEvents,
    TimeSpan Duration
);

/// <summary>
/// Query filters for inspecting Dead Letter Queue messages.
/// </summary>
public record DeadLetterQueryParameters(
    Guid? TenantId = null,
    string? EventType = null,
    string? SearchTerm = null,
    int Page = 1,
    int PageSize = 50
);

/// <summary>
/// Detailed inspection view of a Dead Letter Queue quarantined message.
/// </summary>
public record DeadLetterEventDetail(
    Guid EventId,
    Guid TenantId,
    string EventType,
    string AggregateId,
    string PayloadJson,
    string DeduplicationHash,
    int RetryCount,
    int MaxRetries,
    string? LastError,
    string? DeadLetterReason,
    DateTime CreatedAtUtc,
    DateTime? QuarantinedAtUtc,
    string? CorrelationId,
    string? PartitionKey,
    Dictionary<string, string> Headers
);

/// <summary>
/// Request to replay quarantined messages from the Dead Letter Queue.
/// </summary>
public record ReplayDeadLetterRequest(
    List<Guid> EventIds,
    bool ResetRetryCount = true,
    string? Reason = null
);

/// <summary>
/// Result of replaying Dead Letter Queue messages.
/// </summary>
public record ReplayDeadLetterResult(
    int RequestedCount,
    int ReplayedCount,
    List<Guid> ReplayedEventIds,
    List<string> Errors
);

/// <summary>
/// Live operational statistics of the transactional outbox event mesh.
/// </summary>
public record OutboxMeshStatistics(
    long TotalEnqueued,
    long TotalPublished,
    long TotalFailedRetries,
    long TotalQuarantinedDeadLetter,
    long TotalDeduplicationHits,
    long CurrentPendingCount,
    long CurrentProcessingCount,
    long CurrentDeadLetterDepth,
    double SuccessRatePercent,
    double AverageDispatchLatencyMs,
    Dictionary<string, long> CountsByEventType,
    DateTime GeneratedAtUtc
);

/// <summary>
/// Strongly-typed payload for OrderPaid business events.
/// </summary>
public record OrderPaidPayload(
    string OrderId,
    decimal Amount,
    string Currency,
    string CustomerEmail,
    string PaymentMethod,
    DateTime PaidAtUtc
);

/// <summary>
/// Strongly-typed payload for DealWon business events.
/// </summary>
public record DealWonPayload(
    string DealId,
    decimal DealValue,
    string Currency,
    string AccountName,
    string OwnerId,
    DateTime WonAtUtc
);

/// <summary>
/// Strongly-typed payload for LeadQualified business events.
/// </summary>
public record LeadQualifiedPayload(
    string LeadId,
    string LeadScore,
    string Source,
    string ContactName,
    string ContactEmail,
    DateTime QualifiedAtUtc
);

/// <summary>
/// Strongly-typed payload for TicketEscalated business events.
/// </summary>
public record TicketEscalatedPayload(
    string TicketId,
    string Priority,
    string EscalationReason,
    string AssigneeId,
    int BreachMinutesRemaining,
    DateTime EscalatedAtUtc
);
