using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Marketing;

public enum DurablePublishStatus
{
    Published = 1,
    PublishedWithError = 2,
    RefreshTokenRequired = 3,
    Failed = 4
}

public record DurablePublishResultDto(
    Guid PostId,
    SocialPlatform Platform,
    DurablePublishStatus Status,
    string? RemotePostId,
    string? ErrorMessage,
    DateTime CompletedAtUtc
);

public record ViralHookSuggestionDto(
    string HookText,
    string Angle, // "Contrarian", "Statistical", "Question", "Story"
    double EstimatedViralityScore
);

public record PlatformAdaptedPostDto(
    SocialPlatform Platform,
    string FormattedContent,
    List<string> RecommendedHashtags,
    int CharacterCount,
    int MaxCharacterLimit = 280,
    bool IsWithinLimit = true,
    string RecommendedAspectRatio = "1:1 Square",
    string? ValidationWarning = null
);

public record AccountTokenHealthDto(
    Guid AccountId,
    Guid TenantId,
    SocialPlatform Platform,
    string AccountName,
    DateTime? TokenExpiresAtUtc,
    bool RefreshTokenRequired,
    bool IsExpired,
    int DaysUntilExpiration,
    string StatusSummary
);

public record TokenHealthAuditReportDto(
    Guid TenantId,
    DateTime AuditedAtUtc,
    int TotalAccountsAudited,
    int HealthyAccountsCount,
    int ExpiringOrRequiredRefreshCount,
    List<AccountTokenHealthDto> Accounts
);

public record CharacterLimitValidationResultDto(
    SocialPlatform Platform,
    int MaxLimit,
    int ActualLength,
    bool IsValid,
    int RemainingCharacters
);

public interface ISocialPublishingEngineService
{
    Task<List<DurablePublishResultDto>> PublishPostDurablyAsync(Guid tenantId, Guid postId, List<SocialPlatform> platforms);
    Task<List<ViralHookSuggestionDto>> GenerateViralHooksAsync(Guid tenantId, string coreTopic);
    Task<List<PlatformAdaptedPostDto>> RewriteForPlatformsAsync(Guid tenantId, string masterDraft, List<SocialPlatform> targetPlatforms, string? preferredAspectRatio = null);
    Task<TokenHealthAuditReportDto> AuditConnectedAccountsTokenHealthAsync(Guid tenantId);
    CharacterLimitValidationResultDto ValidatePostContent(SocialPlatform platform, string content);
}

public class SocialPublishingEngineService : ISocialPublishingEngineService
{
    private readonly SGrowingDbContext _db;

    public SocialPublishingEngineService(SGrowingDbContext db)
    {
        _db = db;
    }

    public static int GetMaxCharacterLimit(SocialPlatform platform) => platform switch
    {
        SocialPlatform.X => 280,
        SocialPlatform.LinkedIn => 3000,
        SocialPlatform.Instagram => 2200,
        SocialPlatform.Facebook => 2200,
        SocialPlatform.TikTok => 2200,
        SocialPlatform.Threads => 500,
        SocialPlatform.Telegram => 4096,
        _ => 280
    };

    public CharacterLimitValidationResultDto ValidatePostContent(SocialPlatform platform, string content)
    {
        var maxLimit = GetMaxCharacterLimit(platform);
        var actualLength = content?.Length ?? 0;
        var isValid = actualLength <= maxLimit;
        var remaining = maxLimit - actualLength;
        return new CharacterLimitValidationResultDto(platform, maxLimit, actualLength, isValid, remaining);
    }

    public async Task<List<DurablePublishResultDto>> PublishPostDurablyAsync(Guid tenantId, Guid postId, List<SocialPlatform> platforms)
    {
        var post = await _db.SocialPosts.FirstOrDefaultAsync(p => p.Id == postId);
        var results = new List<DurablePublishResultDto>();

        foreach (var platform in platforms)
        {
            // Simulate Postiz two-phase publishing & partial failure guarantees
            if (platform == SocialPlatform.X)
            {
                results.Add(new DurablePublishResultDto(
                    postId,
                    platform,
                    DurablePublishStatus.Published,
                    $"x-{Guid.NewGuid():N}",
                    null,
                    DateTime.UtcNow
                ));
            }
            else if (platform == SocialPlatform.LinkedIn)
            {
                // Simulate PublishedWithError (e.g. text published successfully, but rich link preview had a temporary 206)
                results.Add(new DurablePublishResultDto(
                    postId,
                    platform,
                    DurablePublishStatus.PublishedWithError,
                    $"urn:li:share:{Guid.NewGuid():N}",
                    "Post body succeeded; secondary link preview metadata warning.",
                    DateTime.UtcNow
                ));
            }
            else
            {
                results.Add(new DurablePublishResultDto(
                    postId,
                    platform,
                    DurablePublishStatus.Published,
                    $"{platform.ToString().ToLower()}-{Guid.NewGuid():N}",
                    null,
                    DateTime.UtcNow
                ));
            }
        }

        if (post != null)
        {
            post.Status = SocialPostStatus.Published;
            post.PublishedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return results;
    }

    public Task<List<ViralHookSuggestionDto>> GenerateViralHooksAsync(Guid tenantId, string coreTopic)
    {
        var suggestions = new List<ViralHookSuggestionDto>
        {
            new($"90% of companies get {coreTopic} completely wrong. Here's why:", "Contrarian", 94.5),
            new($"We scaled revenue by 340% using this single {coreTopic} framework:", "Statistical", 91.2),
            new($"If you had to restart {coreTopic} from scratch in 2026, what would you do differently?", "Question", 88.0),
            new($"Last year, our team made a critical mistake with {coreTopic}. Here is what it taught us:", "Story", 92.8)
        };

        return Task.FromResult(suggestions);
    }

    public Task<List<PlatformAdaptedPostDto>> RewriteForPlatformsAsync(
        Guid tenantId,
        string masterDraft,
        List<SocialPlatform> targetPlatforms,
        string? preferredAspectRatio = null)
    {
        var adapted = new List<PlatformAdaptedPostDto>();

        foreach (var platform in targetPlatforms)
        {
            int maxLimit = GetMaxCharacterLimit(platform);
            string formattedContent;
            List<string> hashtags;
            string aspectRatio;

            switch (platform)
            {
                case SocialPlatform.X:
                    // 1-2 hashtags
                    hashtags = new List<string> { "#Growth", "#Tech" };
                    aspectRatio = "1:1 Square";

                    // Truncate cleanly if masterDraft exceeds 270 chars so total fits in 280
                    var xBody = masterDraft.Length > 270 ? masterDraft[..267] + "..." : masterDraft;
                    formattedContent = xBody;
                    break;

                case SocialPlatform.LinkedIn:
                    // 3-5 hashtags
                    hashtags = new List<string> { "#Leadership", "#B2B", "#SalesOps", "#Innovation" };
                    aspectRatio = "1:1 Square";
                    formattedContent = $"💡 Executive Takeaway:\n\n{masterDraft}\n\nWhat are your thoughts on this approach?";
                    break;

                case SocialPlatform.TikTok:
                    // 3-5 hashtags, 9:16 Vertical Reel/Story
                    hashtags = new List<string> { "#TikTokGrowth", "#TechTok", "#BusinessTips", "#Viral" };
                    aspectRatio = "9:16 Vertical Reel/Story";
                    formattedContent = $"🚀 POV: {masterDraft}\n\nDouble tap if you agree! 🎯";
                    break;

                case SocialPlatform.Instagram:
                    // 3-5 hashtags, 1:1 Square or 9:16 Vertical Reel
                    hashtags = new List<string> { "#marketing", "#growth", "#business", "#strategy" };
                    if (!string.IsNullOrEmpty(preferredAspectRatio) && preferredAspectRatio.Contains("9:16"))
                    {
                        aspectRatio = "9:16 Vertical Reel/Story";
                        formattedContent = $"🔥 REEL HOOK: {masterDraft}\n.\n.\nSave this for later! 📌";
                    }
                    else
                    {
                        aspectRatio = "1:1 Square";
                        formattedContent = $"{masterDraft}\n.\n.\nDrop a comment below! 👇";
                    }
                    break;

                case SocialPlatform.Facebook:
                    // 2-3 hashtags
                    hashtags = new List<string> { "#BusinessGrowth", "#Enterprise" };
                    aspectRatio = "1:1 Square";
                    formattedContent = $"📢 Update:\n\n{masterDraft}\n\nLearn more at our link in bio.";
                    break;

                case SocialPlatform.Threads:
                    hashtags = new List<string> { "#tech", "#builder" };
                    aspectRatio = "1:1 Square";
                    var threadsBody = masterDraft.Length > 490 ? masterDraft[..487] + "..." : masterDraft;
                    formattedContent = threadsBody;
                    break;

                case SocialPlatform.Telegram:
                default:
                    hashtags = new List<string> { "#announcement", "#sgrowing" };
                    aspectRatio = "1:1 Square";
                    formattedContent = $"📢 {masterDraft}";
                    break;
            }

            int charCount = formattedContent.Length;
            bool isWithinLimit = charCount <= maxLimit;
            string? warning = isWithinLimit
                ? null
                : $"Formatted content length ({charCount}) exceeds maximum limit ({maxLimit}) for {platform}.";

            adapted.Add(new PlatformAdaptedPostDto(
                platform,
                formattedContent,
                hashtags,
                charCount,
                maxLimit,
                isWithinLimit,
                aspectRatio,
                warning
            ));
        }

        return Task.FromResult(adapted);
    }

    public async Task<TokenHealthAuditReportDto> AuditConnectedAccountsTokenHealthAsync(Guid tenantId)
    {
        var accounts = await _db.SocialAccounts
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .ToListAsync();

        var auditedUtc = DateTime.UtcNow;
        var expirationThreshold = auditedUtc.AddDays(7);
        var healthList = new List<AccountTokenHealthDto>();
        int expiringCount = 0;

        foreach (var acc in accounts)
        {
            bool isExpired = acc.TokenExpiresAtUtc.HasValue && acc.TokenExpiresAtUtc.Value <= auditedUtc;
            bool isExpiringSoon = acc.TokenExpiresAtUtc.HasValue &&
                                  acc.TokenExpiresAtUtc.Value > auditedUtc &&
                                  acc.TokenExpiresAtUtc.Value <= expirationThreshold;

            int daysRemaining = acc.TokenExpiresAtUtc.HasValue
                ? (int)Math.Max(0, Math.Ceiling((acc.TokenExpiresAtUtc.Value - auditedUtc).TotalDays))
                : -1;

            string status;
            if (isExpired)
            {
                status = "Expired";
                acc.RefreshTokenRequired = true;
                expiringCount++;
            }
            else if (isExpiringSoon)
            {
                status = "ExpiringSoon";
                acc.RefreshTokenRequired = true;
                expiringCount++;
            }
            else if (acc.RefreshTokenRequired)
            {
                status = "RefreshTokenRequired";
                expiringCount++;
            }
            else
            {
                status = "Healthy";
            }

            healthList.Add(new AccountTokenHealthDto(
                acc.Id,
                acc.TenantId,
                acc.Platform,
                acc.AccountName,
                acc.TokenExpiresAtUtc,
                acc.RefreshTokenRequired,
                isExpired,
                daysRemaining,
                status
            ));
        }

        if (expiringCount > 0)
        {
            await _db.SaveChangesAsync();
        }

        return new TokenHealthAuditReportDto(
            tenantId,
            auditedUtc,
            accounts.Count,
            accounts.Count - expiringCount,
            expiringCount,
            healthList
        );
    }
}
