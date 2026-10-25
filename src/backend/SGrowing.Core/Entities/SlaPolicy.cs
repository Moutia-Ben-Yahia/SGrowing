namespace SGrowing.Core.Entities;

public class SlaPolicy
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public ConversationPriority Priority { get; set; } = ConversationPriority.Normal;
    public int TargetFirstResponseMinutes { get; set; } = 30;
    public int TargetResolutionMinutes { get; set; } = 240;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private SlaPolicy() { }

    public static SlaPolicy Create(
        Guid tenantId,
        string name,
        ConversationPriority priority,
        int targetFirstResponseMinutes,
        int targetResolutionMinutes)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        return new SlaPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Priority = priority,
            TargetFirstResponseMinutes = targetFirstResponseMinutes,
            TargetResolutionMinutes = targetResolutionMinutes,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
