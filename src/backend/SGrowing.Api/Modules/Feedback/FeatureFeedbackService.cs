using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.Feedback;

public record RoadmapBoardDto(
    List<FeatureFeedbackPost> OpenIdeas,
    List<FeatureFeedbackPost> PlannedFeatures,
    List<FeatureFeedbackPost> InProgressFeatures,
    List<FeatureFeedbackPost> CompletedFeatures
);

public record VoteResultDto(
    Guid PostId,
    bool IsUpvoted,
    int TotalUpvotes
);

public interface IFeatureFeedbackService
{
    Task<FeatureFeedbackPost> CreatePostAsync(Guid tenantId, string title, string description, string category = "Feature", string authorEmail = "", string authorName = "");
    Task<VoteResultDto> ToggleVoteAsync(Guid tenantId, Guid postId, string voterIdentifier);
    Task<FeatureFeedbackPost> UpdatePostStatusAsync(Guid tenantId, Guid postId, FeedbackStatus newStatus);
    Task<RoadmapBoardDto> GetRoadmapBoardAsync(Guid tenantId);
    Task<List<FeatureFeedbackPost>> GetPostsAsync(Guid tenantId, FeedbackStatus? status = null, string? category = null);
}

public class FeatureFeedbackService : IFeatureFeedbackService
{
    private readonly SGrowingDbContext _db;
    private readonly IOutbox _outbox;

    public FeatureFeedbackService(SGrowingDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<FeatureFeedbackPost> CreatePostAsync(Guid tenantId, string title, string description, string category = "Feature", string authorEmail = "", string authorName = "")
    {
        var post = FeatureFeedbackPost.Create(tenantId, title, description, category, authorEmail, authorName);
        _db.FeatureFeedbackPosts.Add(post);

        // Auto-vote by author if identifier available
        if (!string.IsNullOrWhiteSpace(authorEmail))
        {
            var initialVote = FeatureFeedbackVote.Create(tenantId, post.Id, authorEmail);
            _db.FeatureFeedbackVotes.Add(initialVote);
        }

        await _db.SaveChangesAsync();

        await _outbox.EnqueueAsync(tenantId, "Feedback.PostCreated", new
        {
            PostId = post.Id,
            post.Title,
            post.Category,
            post.CreatedAtUtc
        });

        return post;
    }

    public async Task<VoteResultDto> ToggleVoteAsync(Guid tenantId, Guid postId, string voterIdentifier)
    {
        var post = await _db.FeatureFeedbackPosts.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == postId);
        if (post == null)
            throw new KeyNotFoundException($"Feedback post {postId} not found.");

        var normalizedVoter = voterIdentifier.Trim().ToLowerInvariant();
        var existingVote = await _db.FeatureFeedbackVotes
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.PostId == postId && v.VoterIdentifier == normalizedVoter);

        bool isUpvoted;
        if (existingVote != null)
        {
            // Remove vote
            _db.FeatureFeedbackVotes.Remove(existingVote);
            post.UpvoteCount = Math.Max(0, post.UpvoteCount - 1);
            isUpvoted = false;
        }
        else
        {
            // Add vote
            var vote = FeatureFeedbackVote.Create(tenantId, postId, normalizedVoter);
            _db.FeatureFeedbackVotes.Add(vote);
            post.UpvoteCount++;
            isUpvoted = true;
        }

        await _db.SaveChangesAsync();

        return new VoteResultDto(
            PostId: post.Id,
            IsUpvoted: isUpvoted,
            TotalUpvotes: post.UpvoteCount
        );
    }

    public async Task<FeatureFeedbackPost> UpdatePostStatusAsync(Guid tenantId, Guid postId, FeedbackStatus newStatus)
    {
        var post = await _db.FeatureFeedbackPosts.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == postId);
        if (post == null)
            throw new KeyNotFoundException($"Feedback post {postId} not found.");

        post.Status = newStatus;
        await _db.SaveChangesAsync();

        await _outbox.EnqueueAsync(tenantId, "Feedback.StatusChanged", new
        {
            PostId = post.Id,
            NewStatus = newStatus.ToString(),
            post.Title
        });

        return post;
    }

    public async Task<RoadmapBoardDto> GetRoadmapBoardAsync(Guid tenantId)
    {
        var allPosts = await _db.FeatureFeedbackPosts
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.UpvoteCount)
            .ToListAsync();

        return new RoadmapBoardDto(
            OpenIdeas: allPosts.Where(p => p.Status == FeedbackStatus.Open || p.Status == FeedbackStatus.UnderReview).ToList(),
            PlannedFeatures: allPosts.Where(p => p.Status == FeedbackStatus.Planned).ToList(),
            InProgressFeatures: allPosts.Where(p => p.Status == FeedbackStatus.InProgress).ToList(),
            CompletedFeatures: allPosts.Where(p => p.Status == FeedbackStatus.Completed).ToList()
        );
    }

    public async Task<List<FeatureFeedbackPost>> GetPostsAsync(Guid tenantId, FeedbackStatus? status = null, string? category = null)
    {
        var query = _db.FeatureFeedbackPosts.Where(p => p.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category.ToLower() == category.ToLower().Trim());
        }

        return await query.OrderByDescending(p => p.UpvoteCount).ToListAsync();
    }
}
