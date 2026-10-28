namespace SGrowing.Core.Entities;

public class WhatsAppNewsletterChannelRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string ChannelJid { get; set; } = string.Empty; // e.g. 120363025123456789@newsletter
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public int SubscriberCount { get; set; } = 0;
    public bool IsMuted { get; set; } = false;
    public bool IsVerified { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}

public class WhatsAppNewsletterPostRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ChannelId { get; set; }
    public string ServerId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string ReactionCountsJson { get; set; } = "{}"; // JSON map e.g. {"👍": 12, "❤️": 5}
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
}

public record CreateNewsletterChannelRequest(
    string Name,
    string Description,
    string? CustomJid = null
);

public record BroadcastNewsletterPostRequest(
    string Content,
    string? MediaUrl = null
);

public record ReactNewsletterPostRequest(
    string Emoji,
    string SenderJid
);

public record NewsletterChannelDetailDto(
    WhatsAppNewsletterChannelRecord Channel,
    List<WhatsAppNewsletterPostRecord> RecentPosts
);
