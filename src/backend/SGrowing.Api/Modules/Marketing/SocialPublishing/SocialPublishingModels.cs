namespace SGrowing.Api.Modules.Marketing.SocialPublishing;

public enum SocialPlatform
{
    Facebook,
    Instagram,
    LinkedIn,
    X,
    TikTok
}

public enum SocialPostStatus
{
    Draft,
    PendingApproval,
    Approved,
    Scheduled,
    Published,
    Failed
}

public record SocialPost(
    Guid Id,
    string TenantId,
    List<SocialPlatform> Platforms,
    string ContentText,
    List<string> MediaAssetUrls,
    DateTime? ScheduledAtUtc,
    DateTime? PublishedAtUtc,
    SocialPostStatus Status,
    Dictionary<string, int> Metrics,
    string? ApproverId,
    DateTime CreatedAtUtc
);

public record SchedulePostRequest(
    string TenantId,
    List<SocialPlatform> Platforms,
    string ContentText,
    List<string> MediaAssetUrls,
    DateTime ScheduledAtUtc
);

public record ApprovePostRequest(
    Guid PostId,
    string TenantId,
    string ApproverId,
    bool IsApproved
);

public record PublishNowRequest(
    Guid PostId,
    string TenantId
);

public record SocialAnalyticsWebhookPayload(
    Guid PostId,
    string TenantId,
    SocialPlatform Platform,
    int Impressions,
    int Clicks,
    int Likes,
    int Shares
);
