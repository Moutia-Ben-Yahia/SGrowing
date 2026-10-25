namespace MarketingStudio.Core.Entities;

public enum ChannelType
{
    WhatsAppCloud = 1,
    WhatsAppEvolution = 2,
    Email = 3,
    WebChat = 4
}

public class Inbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChannelType ChannelType { get; set; }
    public string ConfigJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
