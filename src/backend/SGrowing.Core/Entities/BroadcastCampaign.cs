namespace SGrowing.Core.Entities;

public enum BroadcastStatus
{
    Draft = 1,
    Scheduled = 2,
    Running = 3,
    Completed = 4,
    Failed = 5
}

public class BroadcastCampaign
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public string TargetTag { get; set; } = string.Empty;
    public BroadcastStatus Status { get; set; } = BroadcastStatus.Draft;
    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public DateTime CreatedAtUtc { get; private set; }

    private BroadcastCampaign() { }

    public static BroadcastCampaign Create(
        Guid tenantId,
        string name,
        Guid templateId,
        string targetTag)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));

        return new BroadcastCampaign
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            TemplateId = templateId,
            TargetTag = targetTag,
            Status = BroadcastStatus.Draft,
            TotalRecipients = 0,
            SentCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
