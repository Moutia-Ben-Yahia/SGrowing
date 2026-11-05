using System;

namespace SGrowing.Core.Entities;

public enum OutboxMessageStatus
{
    Pending = 0,
    Processing = 1,
    Published = 2,
    Failed = 3
}

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

public class OutboxMessageEntity : BaseEntity
{
    public Guid TenantId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string? DestinationTopic { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 5;
    public string? LastError { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? CorrelationId { get; set; }
}
