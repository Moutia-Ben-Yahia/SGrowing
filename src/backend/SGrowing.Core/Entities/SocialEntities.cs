namespace SGrowing.Core.Entities;

public enum SocialPlatform
{
    Facebook = 1,
    Instagram = 2,
    Threads = 3,
    X = 4,
    LinkedIn = 5,
    Telegram = 6,
    TikTok = 7,
    YouTube = 8
}

public enum SocialPostStatus
{
    Draft = 1,
    Scheduled = 2,
    Published = 3,
    Failed = 4
}

public class SocialAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public SocialPlatform Platform { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string ExternalAccountId { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAtUtc { get; set; }
    public bool RefreshTokenRequired { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public SocialAccount() { }

    public static SocialAccount Create(
        Guid tenantId,
        SocialPlatform platform,
        string accountName,
        string externalAccountId,
        string? accessToken = null,
        bool isActive = true,
        DateTime? tokenExpiresAtUtc = null,
        string? refreshToken = null,
        bool refreshTokenRequired = false)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(accountName)) throw new ArgumentException("AccountName is required.", nameof(accountName));
        if (string.IsNullOrWhiteSpace(externalAccountId)) throw new ArgumentException("ExternalAccountId is required.", nameof(externalAccountId));

        return new SocialAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Platform = platform,
            AccountName = accountName.Trim(),
            ExternalAccountId = externalAccountId.Trim(),
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenExpiresAtUtc = tokenExpiresAtUtc,
            RefreshTokenRequired = refreshTokenRequired,
            IsActive = isActive,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class SocialPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string> MediaUrls { get; set; } = new();
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public SocialPostStatus Status { get; set; } = SocialPostStatus.Draft;
    public List<SocialPlatform> TargetPlatforms { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public SocialPost() { }

    public static SocialPost Create(
        Guid tenantId,
        string content,
        List<SocialPlatform> targetPlatforms,
        DateTime? scheduledAtUtc = null,
        List<string>? mediaUrls = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Content is required.", nameof(content));
        if (targetPlatforms == null || targetPlatforms.Count == 0) throw new ArgumentException("At least one target platform is required.", nameof(targetPlatforms));

        return new SocialPost
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Content = content.Trim(),
            TargetPlatforms = targetPlatforms.Distinct().ToList(),
            ScheduledAtUtc = scheduledAtUtc,
            MediaUrls = mediaUrls ?? new List<string>(),
            Status = scheduledAtUtc.HasValue ? SocialPostStatus.Scheduled : SocialPostStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkPublished(DateTime? publishedAtUtc = null)
    {
        Status = SocialPostStatus.Published;
        PublishedAtUtc = publishedAtUtc ?? DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = SocialPostStatus.Failed;
    }
}
