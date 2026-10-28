using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public interface IWhatsAppNewsletterService
{
    Task<WhatsAppNewsletterChannelRecord> CreateChannelAsync(Guid tenantId, CreateNewsletterChannelRequest req, CancellationToken ct = default);
    Task<IEnumerable<WhatsAppNewsletterChannelRecord>> GetChannelsAsync(Guid tenantId, CancellationToken ct = default);
    Task<NewsletterChannelDetailDto?> GetChannelDetailsAsync(Guid tenantId, Guid channelId, CancellationToken ct = default);
    Task<WhatsAppNewsletterPostRecord?> BroadcastPostAsync(Guid tenantId, Guid channelId, BroadcastNewsletterPostRequest req, CancellationToken ct = default);
    Task<WhatsAppNewsletterPostRecord?> RecordReactionAsync(Guid tenantId, Guid postId, ReactNewsletterPostRequest req, CancellationToken ct = default);
}
