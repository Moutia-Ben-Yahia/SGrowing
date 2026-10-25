namespace SGrowing.Core.Entities;

public class CustomerEvent
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ContactId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = "{}";
    public DateTime OccurredAtUtc { get; private set; }

    private CustomerEvent() { }

    public static CustomerEvent Create(Guid tenantId, Guid contactId, string eventName, string payloadJson = "{}")
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (contactId == Guid.Empty) throw new ArgumentException("ContactId is required.", nameof(contactId));
        if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentException("EventName is required.", nameof(eventName));

        return new CustomerEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contactId,
            EventName = eventName,
            PayloadJson = payloadJson,
            OccurredAtUtc = DateTime.UtcNow
        };
    }
}
