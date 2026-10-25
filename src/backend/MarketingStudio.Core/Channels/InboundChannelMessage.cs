namespace MarketingStudio.Core.Channels;

public record InboundChannelMessage(
    Guid TenantId,
    string ChannelType,
    string ExternalSenderId,
    string? SenderName,
    string ExternalMessageId,
    string ContentType,
    string Content,
    string? MediaUrl = null
);
