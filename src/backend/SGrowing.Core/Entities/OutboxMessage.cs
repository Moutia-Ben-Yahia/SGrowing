namespace SGrowing.Core.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime? ProcessedOnUtc { get; private set; }
    public int RetryCount { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(Guid tenantId, string eventType, string payload)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("EventType cannot be empty.", nameof(eventType));

        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OccurredOnUtc = DateTime.UtcNow,
            EventType = eventType,
            Payload = payload,
            RetryCount = 0
        };
    }

    public void MarkProcessed()
    {
        ProcessedOnUtc = DateTime.UtcNow;
        Error = null;
    }

    public void MarkFailed(string error)
    {
        RetryCount++;
        Error = error;
    }
}
