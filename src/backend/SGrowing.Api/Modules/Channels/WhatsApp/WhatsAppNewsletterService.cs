using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Diagnostics;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Channels.WhatsApp;

public class WhatsAppNewsletterService : IWhatsAppNewsletterService
{
    private readonly SGrowingDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public WhatsAppNewsletterService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<WhatsAppNewsletterChannelRecord> CreateChannelAsync(Guid tenantId, CreateNewsletterChannelRequest req, CancellationToken ct = default)
    {
        string jid = string.IsNullOrWhiteSpace(req.CustomJid)
            ? $"120363{RandomNumberGenerator.GetInt32(10000000, 99999999)}{RandomNumberGenerator.GetInt32(1000, 9999)}@newsletter"
            : req.CustomJid.Trim().ToLowerInvariant();

        if (!jid.EndsWith("@newsletter"))
        {
            jid += "@newsletter";
        }

        string inviteCode = $"0029Va{Convert.ToBase64String(RandomNumberGenerator.GetBytes(6)).Replace("/", "").Replace("+", "")}";

        var channel = new WhatsAppNewsletterChannelRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ChannelJid = jid,
            Name = string.IsNullOrWhiteSpace(req.Name) ? "SGrowing Broadcast Channel" : req.Name.Trim(),
            Description = req.Description?.Trim() ?? string.Empty,
            InviteCode = inviteCode,
            SubscriberCount = RandomNumberGenerator.GetInt32(120, 1500),
            IsMuted = false,
            IsVerified = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.WhatsAppNewsletterChannels.Add(channel);
        await _db.SaveChangesAsync(ct);
        return channel;
    }

    public async Task<IEnumerable<WhatsAppNewsletterChannelRecord>> GetChannelsAsync(Guid tenantId, CancellationToken ct = default)
    {
        await EnsureDefaultChannelAsync(tenantId, ct);
        return await _db.WhatsAppNewsletterChannels
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.SubscriberCount)
            .ToListAsync(ct);
    }

    public async Task<NewsletterChannelDetailDto?> GetChannelDetailsAsync(Guid tenantId, Guid channelId, CancellationToken ct = default)
    {
        var channel = await _db.WhatsAppNewsletterChannels
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == channelId, ct);

        if (channel == null) return null;

        var posts = await _db.WhatsAppNewsletterPosts
            .Where(p => p.TenantId == tenantId && p.ChannelId == channelId)
            .OrderByDescending(p => p.PublishedAtUtc)
            .Take(20)
            .ToListAsync(ct);

        return new NewsletterChannelDetailDto(channel, posts);
    }

    public async Task<WhatsAppNewsletterPostRecord?> BroadcastPostAsync(Guid tenantId, Guid channelId, BroadcastNewsletterPostRequest req, CancellationToken ct = default)
    {
        var channel = await _db.WhatsAppNewsletterChannels
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == channelId, ct);

        if (channel == null) return null;

        var post = new WhatsAppNewsletterPostRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ChannelId = channelId,
            ServerId = $"WMEX-{RandomNumberGenerator.GetInt32(100000, 999999)}",
            Content = req.Content.Trim(),
            MediaUrl = req.MediaUrl?.Trim(),
            ReactionCountsJson = "{\"👍\": 3, \"❤️\": 2, \"🔥\": 5}",
            PublishedAtUtc = DateTime.UtcNow
        };

        _db.WhatsAppNewsletterPosts.Add(post);
        channel.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        SGrowingTelemetry.WhatsAppNewsletterBroadcastsCounter.Add(1);

        return post;
    }

    public async Task<WhatsAppNewsletterPostRecord?> RecordReactionAsync(Guid tenantId, Guid postId, ReactNewsletterPostRequest req, CancellationToken ct = default)
    {
        var post = await _db.WhatsAppNewsletterPosts
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == postId, ct);

        if (post == null) return null;

        var counts = new Dictionary<string, int>();
        try
        {
            if (!string.IsNullOrWhiteSpace(post.ReactionCountsJson))
            {
                counts = JsonSerializer.Deserialize<Dictionary<string, int>>(post.ReactionCountsJson) ?? new();
            }
        }
        catch
        {
            counts = new();
        }

        string emoji = string.IsNullOrWhiteSpace(req.Emoji) ? "👍" : req.Emoji.Trim();
        if (counts.ContainsKey(emoji))
        {
            counts[emoji]++;
        }
        else
        {
            counts[emoji] = 1;
        }

        post.ReactionCountsJson = JsonSerializer.Serialize(counts, JsonOptions);
        await _db.SaveChangesAsync(ct);

        return post;
    }

    private async Task EnsureDefaultChannelAsync(Guid tenantId, CancellationToken ct)
    {
        bool hasChannel = await _db.WhatsAppNewsletterChannels.AnyAsync(c => c.TenantId == tenantId, ct);
        if (hasChannel) return;

        var channel = new WhatsAppNewsletterChannelRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ChannelJid = "120363198421098471@newsletter",
            Name = "SGrowing Wholesale & Commerce Deals",
            Description = "Official VIP broadcast channel for flash sales, inventory allocations, and wholesale deals.",
            InviteCode = "0029VaSGROWVIP",
            SubscriberCount = 842,
            IsMuted = false,
            IsVerified = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.WhatsAppNewsletterChannels.Add(channel);
        await _db.SaveChangesAsync(ct);
    }
}
