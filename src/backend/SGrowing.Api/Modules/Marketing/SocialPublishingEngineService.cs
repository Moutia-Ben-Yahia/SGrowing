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
    int CharacterCount
);

public interface ISocialPublishingEngineService
{
    Task<List<DurablePublishResultDto>> PublishPostDurablyAsync(Guid tenantId, Guid postId, List<SocialPlatform> platforms);
    Task<List<ViralHookSuggestionDto>> GenerateViralHooksAsync(Guid tenantId, string coreTopic);
    Task<List<PlatformAdaptedPostDto>> RewriteForPlatformsAsync(Guid tenantId, string masterDraft, List<SocialPlatform> targetPlatforms);
}

public class SocialPublishingEngineService : ISocialPublishingEngineService
{
    private readonly SGrowingDbContext _db;

    public SocialPublishingEngineService(SGrowingDbContext db)
    {
        _db = db;
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

    public Task<List<PlatformAdaptedPostDto>> RewriteForPlatformsAsync(Guid tenantId, string masterDraft, List<SocialPlatform> targetPlatforms)
    {
        var adapted = new List<PlatformAdaptedPostDto>();

        foreach (var platform in targetPlatforms)
        {
            switch (platform)
            {
                case SocialPlatform.X:
                    var xContent = masterDraft.Length > 270 ? masterDraft[..267] + "..." : masterDraft;
                    adapted.Add(new PlatformAdaptedPostDto(
                        platform,
                        xContent,
                        new List<string> { "#Growth", "#Tech" },
                        xContent.Length
                    ));
                    break;

                case SocialPlatform.LinkedIn:
                    var liContent = $"💡 Executive Takeaway:\n\n{masterDraft}\n\nWhat are your thoughts on this approach?";
                    adapted.Add(new PlatformAdaptedPostDto(
                        platform,
                        liContent,
                        new List<string> { "#Leadership", "#SalesOps", "#B2B" },
                        liContent.Length
                    ));
                    break;

                case SocialPlatform.Instagram:
                case SocialPlatform.Threads:
                default:
                    var igContent = $"{masterDraft}\n.\n.\nDrop a comment below! 👇";
                    adapted.Add(new PlatformAdaptedPostDto(
                        platform,
                        igContent,
                        new List<string> { "#business", "#growthhacks", "#strategy" },
                        igContent.Length
                    ));
                    break;
            }
        }

        return Task.FromResult(adapted);
    }
}
