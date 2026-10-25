namespace SGrowing.Core.Entities;

public enum JourneyStatus
{
    Draft = 1,
    Active = 2,
    Paused = 3
}

public class CustomerJourney
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public JourneyStatus Status { get; set; } = JourneyStatus.Active;
    public DateTime CreatedAtUtc { get; private set; }

    public List<JourneyStep> Steps { get; set; } = new();

    private CustomerJourney() { }

    public static CustomerJourney Create(Guid tenantId, string name, string triggerEvent)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(triggerEvent)) throw new ArgumentException("TriggerEvent is required.", nameof(triggerEvent));

        return new CustomerJourney
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            TriggerEvent = triggerEvent,
            Status = JourneyStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
