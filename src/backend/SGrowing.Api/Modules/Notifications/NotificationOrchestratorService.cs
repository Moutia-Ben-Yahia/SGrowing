using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Notifications;

#region Enums

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannel
{
    InApp,
    WhatsApp,
    Sms,
    Email,
    Webhook
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationTopic
{
    SecurityAlert,
    BillingInvoice,
    TicketSla,
    LeadAssigned,
    MarketingNewsletter,
    SystemMaintenance
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationPriority
{
    Low,
    Normal,
    High,
    UrgentCritical
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeliveryStatus
{
    Pending,
    Delivered,
    Read,
    Failed,
    Throttled,
    OptedOut,
    FallenBack
}

#endregion

#region Models & DTOs

public record SubscriberPreference(
    Guid TenantId,
    Guid RecipientId,
    Dictionary<NotificationTopic, List<NotificationChannel>> TopicChannelSettings,
    int QuietHoursStartHourUtc,
    int QuietHoursEndHourUtc,
    bool IsQuietHoursEnabled,
    int MaxNotificationsPerHour
);

public record FallbackPolicy(
    Guid PolicyId,
    Guid TenantId,
    NotificationTopic Topic,
    List<NotificationChannel> ChannelsInOrder,
    int TimeoutMinutesBetweenChannels
);

public record NotificationDeliveryAttempt(
    DateTime TimestampUtc,
    NotificationChannel Channel,
    DeliveryStatus Status,
    string? Notes
);

public record NotificationRecord
{
    public Guid NotificationId { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid RecipientId { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientEmail { get; init; }
    public NotificationTopic Topic { get; init; }
    public NotificationPriority Priority { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? ActionUrl { get; init; }
    public string? IdempotencyKey { get; init; }
    public NotificationChannel CurrentChannel { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public DateTime DispatchedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? DeliveredAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime NextFallbackUtc { get; set; }
    public List<NotificationDeliveryAttempt> DeliveryAttempts { get; init; } = new();
}

public record InAppFeedItem(
    Guid FeedItemId,
    Guid NotificationId,
    Guid TenantId,
    Guid RecipientId,
    string Title,
    string Body,
    string? ActionUrl,
    NotificationPriority Priority,
    bool IsRead,
    DateTime CreatedAtUtc
);

public record DispatchNotificationRequest(
    Guid TenantId,
    Guid RecipientId,
    string? RecipientPhone,
    string? RecipientEmail,
    NotificationTopic Topic,
    NotificationPriority Priority,
    string Title,
    string Body,
    string? ActionUrl = null,
    string? IdempotencyKey = null
);

public record RecordDeliveryReceiptRequest(
    Guid TenantId,
    Guid NotificationId,
    NotificationChannel Channel,
    bool IsRead
);

public record SetSubscriberPreferencesRequest(
    Guid TenantId,
    Guid RecipientId,
    Dictionary<NotificationTopic, List<NotificationChannel>> TopicChannelSettings,
    int QuietHoursStartHourUtc = 22,
    int QuietHoursEndHourUtc = 7,
    bool IsQuietHoursEnabled = false,
    int MaxNotificationsPerHour = 20
);

public record SetFallbackPolicyRequest(
    Guid TenantId,
    NotificationTopic Topic,
    List<NotificationChannel> ChannelsInOrder,
    int TimeoutMinutesBetweenChannels = 3
);

public record NotificationOrchestratorMetrics(
    Guid TenantId,
    int TotalDispatched,
    int DeliveredCount,
    int ReadCount,
    int ThrottledCount,
    int OptedOutCount,
    int FallenBackCount
);

#endregion

public interface INotificationOrchestratorService
{
    Task<SubscriberPreference> SetSubscriberPreferencesAsync(SetSubscriberPreferencesRequest request, CancellationToken ct = default);
    Task<SubscriberPreference> GetSubscriberPreferencesAsync(Guid tenantId, Guid recipientId, CancellationToken ct = default);

    Task<FallbackPolicy> SetFallbackPolicyAsync(SetFallbackPolicyRequest request, CancellationToken ct = default);
    Task<List<FallbackPolicy>> GetFallbackPoliciesAsync(Guid tenantId, CancellationToken ct = default);

    Task<NotificationRecord> DispatchNotificationAsync(DispatchNotificationRequest request, DateTime? nowUtc = null, CancellationToken ct = default);
    Task<NotificationRecord> RecordDeliveryReceiptAsync(RecordDeliveryReceiptRequest request, CancellationToken ct = default);
    Task<List<NotificationRecord>> ProcessFallbackSweepAsync(Guid tenantId, DateTime? nowUtc = null, CancellationToken ct = default);

    Task<List<InAppFeedItem>> GetInAppFeedAsync(Guid tenantId, Guid recipientId, bool unreadOnly = false, CancellationToken ct = default);
    Task<bool> MarkFeedItemReadAsync(Guid tenantId, Guid feedItemId, CancellationToken ct = default);

    Task<NotificationOrchestratorMetrics> GetMetricsAsync(Guid tenantId, CancellationToken ct = default);
}

public class NotificationOrchestratorService : INotificationOrchestratorService
{
    private readonly ConcurrentDictionary<Guid, NotificationRecord> _notifications = new();
    private readonly ConcurrentDictionary<string, Guid> _idempotencyIndex = new();
    private readonly ConcurrentDictionary<string, SubscriberPreference> _preferences = new();
    private readonly ConcurrentDictionary<string, FallbackPolicy> _policies = new();
    private readonly ConcurrentDictionary<Guid, InAppFeedItem> _inAppFeed = new();

    private string PrefKey(Guid tenantId, Guid recipientId) => $"{tenantId}:{recipientId}";
    private string PolicyKey(Guid tenantId, NotificationTopic topic) => $"{tenantId}:{topic}";

    public Task<SubscriberPreference> SetSubscriberPreferencesAsync(SetSubscriberPreferencesRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (request.RecipientId == Guid.Empty) throw new ArgumentException("RecipientId is required.", nameof(request));

        var pref = new SubscriberPreference(
            request.TenantId,
            request.RecipientId,
            request.TopicChannelSettings ?? new Dictionary<NotificationTopic, List<NotificationChannel>>(),
            request.QuietHoursStartHourUtc,
            request.QuietHoursEndHourUtc,
            request.IsQuietHoursEnabled,
            request.MaxNotificationsPerHour > 0 ? request.MaxNotificationsPerHour : 20
        );

        _preferences[PrefKey(request.TenantId, request.RecipientId)] = pref;
        return Task.FromResult(pref);
    }

    public Task<SubscriberPreference> GetSubscriberPreferencesAsync(Guid tenantId, Guid recipientId, CancellationToken ct = default)
    {
        if (_preferences.TryGetValue(PrefKey(tenantId, recipientId), out var pref))
        {
            return Task.FromResult(pref);
        }

        // Default: all channels enabled for all topics
        var defaultChannels = new List<NotificationChannel>
        {
            NotificationChannel.InApp,
            NotificationChannel.WhatsApp,
            NotificationChannel.Sms,
            NotificationChannel.Email
        };

        var allTopics = Enum.GetValues<NotificationTopic>()
            .ToDictionary(t => t, _ => new List<NotificationChannel>(defaultChannels));

        var fallbackPref = new SubscriberPreference(tenantId, recipientId, allTopics, 22, 7, false, 20);
        return Task.FromResult(fallbackPref);
    }

    public Task<FallbackPolicy> SetFallbackPolicyAsync(SetFallbackPolicyRequest request, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (request.ChannelsInOrder == null || request.ChannelsInOrder.Count == 0)
        {
            throw new ArgumentException("At least one channel must be specified.", nameof(request));
        }

        var policy = new FallbackPolicy(
            Guid.NewGuid(),
            request.TenantId,
            request.Topic,
            request.ChannelsInOrder,
            request.TimeoutMinutesBetweenChannels > 0 ? request.TimeoutMinutesBetweenChannels : 3
        );

        _policies[PolicyKey(request.TenantId, request.Topic)] = policy;
        return Task.FromResult(policy);
    }

    public Task<List<FallbackPolicy>> GetFallbackPoliciesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _policies.Values.Where(p => p.TenantId == tenantId).ToList();
        return Task.FromResult(list);
    }

    public async Task<NotificationRecord> DispatchNotificationAsync(DispatchNotificationRequest request, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(request));
        if (request.RecipientId == Guid.Empty) throw new ArgumentException("RecipientId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Title)) throw new ArgumentException("Title is required.", nameof(request));

        var now = nowUtc ?? DateTime.UtcNow;

        // 1. Idempotency Check
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var idxKey = $"{request.TenantId}:{request.IdempotencyKey.Trim()}";
            if (_idempotencyIndex.TryGetValue(idxKey, out var existingId) &&
                _notifications.TryGetValue(existingId, out var existingRecord))
            {
                return existingRecord;
            }
        }

        var prefs = await GetSubscriberPreferencesAsync(request.TenantId, request.RecipientId, ct);

        // 2. Opt-out check
        if (prefs.TopicChannelSettings.TryGetValue(request.Topic, out var allowedChannels) && allowedChannels.Count == 0)
        {
            var optOutRecord = new NotificationRecord
            {
                TenantId = request.TenantId,
                RecipientId = request.RecipientId,
                RecipientPhone = request.RecipientPhone,
                RecipientEmail = request.RecipientEmail,
                Topic = request.Topic,
                Priority = request.Priority,
                Title = request.Title.Trim(),
                Body = request.Body?.Trim() ?? string.Empty,
                ActionUrl = request.ActionUrl,
                IdempotencyKey = request.IdempotencyKey,
                CurrentChannel = NotificationChannel.InApp,
                Status = DeliveryStatus.OptedOut,
                DispatchedAtUtc = now
            };
            optOutRecord.DeliveryAttempts.Add(new NotificationDeliveryAttempt(now, NotificationChannel.InApp, DeliveryStatus.OptedOut, "Recipient opted out of this topic"));
            _notifications[optOutRecord.NotificationId] = optOutRecord;
            return optOutRecord;
        }

        // 3. Quiet Hours Check (UrgentCritical bypasses quiet hours)
        if (prefs.IsQuietHoursEnabled && request.Priority != NotificationPriority.UrgentCritical)
        {
            var currentHour = now.Hour;
            bool inQuietHours = false;
            if (prefs.QuietHoursStartHourUtc > prefs.QuietHoursEndHourUtc)
            {
                inQuietHours = currentHour >= prefs.QuietHoursStartHourUtc || currentHour < prefs.QuietHoursEndHourUtc;
            }
            else
            {
                inQuietHours = currentHour >= prefs.QuietHoursStartHourUtc && currentHour < prefs.QuietHoursEndHourUtc;
            }

            if (inQuietHours)
            {
                var throttledRecord = new NotificationRecord
                {
                    TenantId = request.TenantId,
                    RecipientId = request.RecipientId,
                    RecipientPhone = request.RecipientPhone,
                    RecipientEmail = request.RecipientEmail,
                    Topic = request.Topic,
                    Priority = request.Priority,
                    Title = request.Title.Trim(),
                    Body = request.Body?.Trim() ?? string.Empty,
                    ActionUrl = request.ActionUrl,
                    IdempotencyKey = request.IdempotencyKey,
                    CurrentChannel = NotificationChannel.InApp,
                    Status = DeliveryStatus.Throttled,
                    DispatchedAtUtc = now
                };
                throttledRecord.DeliveryAttempts.Add(new NotificationDeliveryAttempt(now, NotificationChannel.InApp, DeliveryStatus.Throttled, "Suppressed by active quiet hours"));
                _notifications[throttledRecord.NotificationId] = throttledRecord;
                return throttledRecord;
            }
        }

        // 4. Rate Limiting Check
        var oneHourAgo = now.AddHours(-1);
        var recentCount = _notifications.Values.Count(n =>
            n.TenantId == request.TenantId &&
            n.RecipientId == request.RecipientId &&
            n.DispatchedAtUtc >= oneHourAgo &&
            n.Status != DeliveryStatus.OptedOut);

        if (recentCount >= prefs.MaxNotificationsPerHour && request.Priority != NotificationPriority.UrgentCritical)
        {
            var rateLimitedRecord = new NotificationRecord
            {
                TenantId = request.TenantId,
                RecipientId = request.RecipientId,
                RecipientPhone = request.RecipientPhone,
                RecipientEmail = request.RecipientEmail,
                Topic = request.Topic,
                Priority = request.Priority,
                Title = request.Title.Trim(),
                Body = request.Body?.Trim() ?? string.Empty,
                ActionUrl = request.ActionUrl,
                IdempotencyKey = request.IdempotencyKey,
                CurrentChannel = NotificationChannel.InApp,
                Status = DeliveryStatus.Throttled,
                DispatchedAtUtc = now
            };
            rateLimitedRecord.DeliveryAttempts.Add(new NotificationDeliveryAttempt(now, NotificationChannel.InApp, DeliveryStatus.Throttled, $"Hourly rate limit reached ({prefs.MaxNotificationsPerHour}/hr)"));
            _notifications[rateLimitedRecord.NotificationId] = rateLimitedRecord;
            return rateLimitedRecord;
        }

        // 5. Determine Fallback Policy
        _policies.TryGetValue(PolicyKey(request.TenantId, request.Topic), out var policy);
        var channelOrder = policy?.ChannelsInOrder ?? new List<NotificationChannel>
        {
            NotificationChannel.InApp,
            NotificationChannel.WhatsApp,
            NotificationChannel.Sms,
            NotificationChannel.Email
        };

        // Filter by user allowed channels if specified
        if (allowedChannels != null && allowedChannels.Count > 0)
        {
            channelOrder = channelOrder.Where(c => allowedChannels.Contains(c)).ToList();
            if (channelOrder.Count == 0)
            {
                channelOrder = new List<NotificationChannel> { NotificationChannel.InApp };
            }
        }

        var initialChannel = channelOrder.First();
        var timeoutMins = policy?.TimeoutMinutesBetweenChannels ?? 3;

        var record = new NotificationRecord
        {
            TenantId = request.TenantId,
            RecipientId = request.RecipientId,
            RecipientPhone = request.RecipientPhone,
            RecipientEmail = request.RecipientEmail,
            Topic = request.Topic,
            Priority = request.Priority,
            Title = request.Title.Trim(),
            Body = request.Body?.Trim() ?? string.Empty,
            ActionUrl = request.ActionUrl,
            IdempotencyKey = request.IdempotencyKey,
            CurrentChannel = initialChannel,
            Status = DeliveryStatus.Delivered,
            DispatchedAtUtc = now,
            DeliveredAtUtc = now,
            NextFallbackUtc = now.AddMinutes(timeoutMins)
        };

        record.DeliveryAttempts.Add(new NotificationDeliveryAttempt(
            now,
            initialChannel,
            DeliveryStatus.Delivered,
            $"Initial dispatch via {initialChannel}. Next fallback deadline: {record.NextFallbackUtc:O}"
        ));

        // Create In-App feed entry if applicable
        if (initialChannel == NotificationChannel.InApp)
        {
            var feedItem = new InAppFeedItem(
                Guid.NewGuid(),
                record.NotificationId,
                record.TenantId,
                record.RecipientId,
                record.Title,
                record.Body,
                record.ActionUrl,
                record.Priority,
                false,
                now
            );
            _inAppFeed[feedItem.FeedItemId] = feedItem;
        }

        _notifications[record.NotificationId] = record;

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var idxKey = $"{request.TenantId}:{request.IdempotencyKey.Trim()}";
            _idempotencyIndex[idxKey] = record.NotificationId;
        }

        return record;
    }

    public Task<NotificationRecord> RecordDeliveryReceiptAsync(RecordDeliveryReceiptRequest request, CancellationToken ct = default)
    {
        if (!_notifications.TryGetValue(request.NotificationId, out var record) || record.TenantId != request.TenantId)
        {
            throw new KeyNotFoundException($"Notification {request.NotificationId} not found.");
        }

        var now = DateTime.UtcNow;
        if (request.IsRead)
        {
            record.Status = DeliveryStatus.Read;
            record.ReadAtUtc = now;
            record.DeliveryAttempts.Add(new NotificationDeliveryAttempt(now, request.Channel, DeliveryStatus.Read, "Receipt: Read confirmed"));

            // Mark matching feed item read
            var matchingFeed = _inAppFeed.Values.FirstOrDefault(f => f.NotificationId == record.NotificationId);
            if (matchingFeed != null)
            {
                _inAppFeed[matchingFeed.FeedItemId] = matchingFeed with { IsRead = true };
            }
        }
        else
        {
            record.Status = DeliveryStatus.Delivered;
            record.DeliveredAtUtc = now;
            record.DeliveryAttempts.Add(new NotificationDeliveryAttempt(now, request.Channel, DeliveryStatus.Delivered, "Receipt: Delivered confirmed"));
        }

        return Task.FromResult(record);
    }

    public Task<List<NotificationRecord>> ProcessFallbackSweepAsync(Guid tenantId, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return Task.FromResult(new List<NotificationRecord>());
        var now = nowUtc ?? DateTime.UtcNow;
        var cascaded = new List<NotificationRecord>();

        var candidates = _notifications.Values.Where(n =>
            n.TenantId == tenantId &&
            n.Status != DeliveryStatus.Read &&
            n.Status != DeliveryStatus.OptedOut &&
            n.Status != DeliveryStatus.Throttled &&
            n.Status != DeliveryStatus.Failed &&
            n.NextFallbackUtc <= now
        ).ToList();

        foreach (var notif in candidates)
        {
            _policies.TryGetValue(PolicyKey(notif.TenantId, notif.Topic), out var policy);
            var channels = policy?.ChannelsInOrder ?? new List<NotificationChannel>
            {
                NotificationChannel.InApp,
                NotificationChannel.WhatsApp,
                NotificationChannel.Sms,
                NotificationChannel.Email
            };

            var currentIndex = channels.IndexOf(notif.CurrentChannel);
            if (currentIndex >= 0 && currentIndex < channels.Count - 1)
            {
                var nextChannel = channels[currentIndex + 1];
                notif.CurrentChannel = nextChannel;
                notif.Status = DeliveryStatus.FallenBack;
                var timeoutMins = policy?.TimeoutMinutesBetweenChannels ?? 3;
                notif.NextFallbackUtc = now.AddMinutes(timeoutMins);

                notif.DeliveryAttempts.Add(new NotificationDeliveryAttempt(
                    now,
                    nextChannel,
                    DeliveryStatus.FallenBack,
                    $"Fallback triggered: Unread on previous channel. Cascaded to {nextChannel}."
                ));

                cascaded.Add(notif);
            }
            else
            {
                notif.Status = DeliveryStatus.Failed;
                notif.DeliveryAttempts.Add(new NotificationDeliveryAttempt(
                    now,
                    notif.CurrentChannel,
                    DeliveryStatus.Failed,
                    "Exhausted all fallback channels without read receipt."
                ));
            }
        }

        return Task.FromResult(cascaded);
    }

    public Task<List<InAppFeedItem>> GetInAppFeedAsync(Guid tenantId, Guid recipientId, bool unreadOnly = false, CancellationToken ct = default)
    {
        var items = _inAppFeed.Values
            .Where(f => f.TenantId == tenantId && f.RecipientId == recipientId);

        if (unreadOnly)
        {
            items = items.Where(f => !f.IsRead);
        }

        var list = items.OrderByDescending(f => f.CreatedAtUtc).ToList();
        return Task.FromResult(list);
    }

    public Task<bool> MarkFeedItemReadAsync(Guid tenantId, Guid feedItemId, CancellationToken ct = default)
    {
        if (_inAppFeed.TryGetValue(feedItemId, out var item) && item.TenantId == tenantId)
        {
            _inAppFeed[feedItemId] = item with { IsRead = true };
            if (_notifications.TryGetValue(item.NotificationId, out var notif))
            {
                notif.Status = DeliveryStatus.Read;
                notif.ReadAtUtc = DateTime.UtcNow;
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<NotificationOrchestratorMetrics> GetMetricsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _notifications.Values.Where(n => n.TenantId == tenantId).ToList();

        var total = list.Count;
        var delivered = list.Count(n => n.Status == DeliveryStatus.Delivered || n.DeliveredAtUtc.HasValue);
        var read = list.Count(n => n.Status == DeliveryStatus.Read || n.ReadAtUtc.HasValue);
        var throttled = list.Count(n => n.Status == DeliveryStatus.Throttled);
        var optedOut = list.Count(n => n.Status == DeliveryStatus.OptedOut);
        var fallenBack = list.Count(n => n.Status == DeliveryStatus.FallenBack || n.DeliveryAttempts.Any(a => a.Status == DeliveryStatus.FallenBack));

        return Task.FromResult(new NotificationOrchestratorMetrics(
            tenantId,
            total,
            delivered,
            read,
            throttled,
            optedOut,
            fallenBack
        ));
    }
}
