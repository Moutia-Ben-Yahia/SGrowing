using System.Collections.Concurrent;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.Marketing;

public interface ISocialPostRepository
{
    Task<SocialPost> AddAsync(SocialPost post, CancellationToken cancellationToken = default);
    Task<List<SocialPost>> GetDueScheduledPostsAsync(Guid tenantId, DateTime asOfUtc, CancellationToken cancellationToken = default);
    Task UpdateAsync(SocialPost post, CancellationToken cancellationToken = default);
    Task<SocialPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<SocialPost>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public class InMemorySocialPostRepository : ISocialPostRepository
{
    private readonly ConcurrentDictionary<Guid, SocialPost> _posts = new();

    public Task<SocialPost> AddAsync(SocialPost post, CancellationToken cancellationToken = default)
    {
        _posts[post.Id] = post;
        return Task.FromResult(post);
    }

    public Task<List<SocialPost>> GetDueScheduledPostsAsync(Guid tenantId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var due = _posts.Values
            .Where(p => p.TenantId == tenantId &&
                        p.Status == SocialPostStatus.Scheduled &&
                        p.ScheduledAtUtc.HasValue &&
                        p.ScheduledAtUtc.Value <= asOfUtc)
            .OrderBy(p => p.ScheduledAtUtc)
            .ToList();
        return Task.FromResult(due);
    }

    public Task UpdateAsync(SocialPost post, CancellationToken cancellationToken = default)
    {
        _posts[post.Id] = post;
        return Task.CompletedTask;
    }

    public Task<SocialPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _posts.TryGetValue(id, out var post);
        return Task.FromResult(post);
    }

    public Task<List<SocialPost>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var list = _posts.Values.Where(p => p.TenantId == tenantId).ToList();
        return Task.FromResult(list);
    }
}

public record SocialPostPublishedPayload(
    Guid PostId,
    Guid TenantId,
    SocialPlatform Platform,
    string PlatformName,
    string Content,
    string CustomizedText,
    List<string> MediaUrls,
    DateTime PublishedAtUtc
);

public class SocialPublishingService
{
    private readonly IOutbox _outbox;
    private readonly ISocialPostRepository _repository;

    public SocialPublishingService(IOutbox outbox, ISocialPostRepository? repository = null)
    {
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        _repository = repository ?? new InMemorySocialPostRepository();
    }

    public SocialPublishingService(SGrowingDbContext db, IOutbox outbox, ISocialPostRepository? repository = null)
        : this(outbox, repository)
    {
    }

    public ISocialPostRepository Repository => _repository;

    public async Task<SocialPost> CreatePostAsync(
        Guid tenantId,
        string content,
        List<SocialPlatform> platforms,
        DateTime? scheduledAtUtc,
        List<string>? mediaUrls = null,
        CancellationToken cancellationToken = default)
    {
        var post = SocialPost.Create(tenantId, content, platforms, scheduledAtUtc, mediaUrls);
        await _repository.AddAsync(post, cancellationToken);
        return post;
    }

    public async Task<List<SocialPost>> PublishDuePostsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var duePosts = await _repository.GetDueScheduledPostsAsync(tenantId, DateTime.UtcNow, cancellationToken);

        foreach (var post in duePosts)
        {
            var publishedAtUtc = DateTime.UtcNow;
            post.MarkPublished(publishedAtUtc);

            foreach (var platform in post.TargetPlatforms)
            {
                var customizedText = CustomizeContentForPlatform(post.Content, platform);

                var payload = new SocialPostPublishedPayload(
                    PostId: post.Id,
                    TenantId: tenantId,
                    Platform: platform,
                    PlatformName: platform.ToString(),
                    Content: customizedText,
                    CustomizedText: customizedText,
                    MediaUrls: post.MediaUrls,
                    PublishedAtUtc: publishedAtUtc
                );

                await _outbox.EnqueueAsync(tenantId, "SocialPost.Published", payload, cancellationToken);
            }

            await _repository.UpdateAsync(post, cancellationToken);
        }

        return duePosts;
    }

    public static string CustomizeContentForPlatform(string content, SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.X => content.Length > 280 ? content[..277] + "..." : content,
            SocialPlatform.LinkedIn => $"{content}\n\n#Business #Growth #Marketing",
            SocialPlatform.Instagram => $"{content}\n\n#marketing #growth #socialmedia",
            SocialPlatform.Telegram => $"📢 {content}",
            SocialPlatform.Threads => content,
            SocialPlatform.Facebook => content,
            _ => content
        };
    }
}
