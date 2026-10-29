namespace SGrowing.Core.Entities;

public enum MessageSenderType
{
    Contact = 1,
    Agent = 2,
    Bot = 3,
    System = 4,
    InternalNote = 5
}

public enum MessageContentType
{
    Text = 1,
    Image = 2,
    Document = 3,
    Audio = 4,
    Location = 5
}

public enum DeliveryStatus
{
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    Read = 4,
    Failed = 5
}

public class Message
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public MessageSenderType SenderType { get; set; }
    public Guid? SenderId { get; set; }
    public MessageContentType ContentType { get; set; } = MessageContentType.Text;
    public string Content { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? ExternalMessageId { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Queued;
    public bool IsPrivateNote { get; set; } = false;
    public DateTime CreatedAtUtc { get; private set; }

    private Message() { }

    public static Message Create(
        Guid tenantId,
        Guid conversationId,
        MessageSenderType senderType,
        string content,
        Guid? senderId = null,
        MessageContentType contentType = MessageContentType.Text,
        string? mediaUrl = null,
        string? externalMessageId = null,
        DeliveryStatus deliveryStatus = DeliveryStatus.Queued,
        bool isPrivateNote = false)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (conversationId == Guid.Empty) throw new ArgumentException("ConversationId is required.", nameof(conversationId));

        return new Message
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            SenderType = isPrivateNote ? MessageSenderType.InternalNote : senderType,
            SenderId = senderId,
            ContentType = contentType,
            Content = content,
            MediaUrl = mediaUrl,
            ExternalMessageId = externalMessageId,
            DeliveryStatus = deliveryStatus,
            IsPrivateNote = isPrivateNote,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
