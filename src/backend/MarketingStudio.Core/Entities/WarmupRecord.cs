namespace MarketingStudio.Core.Entities;

public enum WarmupDirection
{
    Sent = 1,
    Received = 2
}

public enum WarmupStatus
{
    Inbox = 1,
    SpamRescued = 2
}

public class WarmupRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SenderAccountId { get; set; }
    public string PeerEmail { get; set; } = string.Empty;
    public WarmupDirection Direction { get; set; } = WarmupDirection.Sent;
    public WarmupStatus Status { get; set; } = WarmupStatus.Inbox;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
