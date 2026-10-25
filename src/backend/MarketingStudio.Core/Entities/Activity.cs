namespace MarketingStudio.Core.Entities;

public enum ActivityType
{
    Message = 1,
    Note = 2,
    Call = 3,
    StageChange = 4
}

public class Activity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public ActivityType Type { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
