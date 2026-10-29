using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Feedback.Community;

public class CommunityFeedbackService : ICommunityFeedbackService
{
    private readonly SGrowingDbContext _db;

    public CommunityFeedbackService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<CommunityFeedbackPost> SubmitPostAsync(
        Guid tenantId,
        string title,
        string description,
        string authorEmail)
    {
        var cleanTitle = title.Trim();
        var post = CommunityFeedbackPost.Create(tenantId, cleanTitle, description, authorEmail);

        // Check for potential duplicate by title similarity
        var existingSimilar = await _db.CommunityFeedbackPosts
            .Where(p => p.TenantId == tenantId && p.Title.ToLower() == cleanTitle.ToLower())
            .FirstOrDefaultAsync();

        if (existingSimilar != null)
        {
            post.CanonicalPostId = existingSimilar.Id;
        }

        _db.CommunityFeedbackPosts.Add(post);

        // Add author's initial vote
        var initialVote = FeedbackPostVote.Create(tenantId, post.Id, authorEmail);
        _db.FeedbackPostVotes.Add(initialVote);

        await _db.SaveChangesAsync();
        return post;
    }

    public async Task<bool> VotePostAsync(Guid tenantId, Guid postId, string voterEmail)
    {
        var cleanEmail = voterEmail.Trim().ToLowerInvariant();

        var existingVote = await _db.FeedbackPostVotes
            .FirstOrDefaultAsync(v => v.TenantId == tenantId && v.PostId == postId && v.VoterEmail == cleanEmail);

        if (existingVote != null)
        {
            // Already voted
            return false;
        }

        var post = await _db.CommunityFeedbackPosts
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == postId);

        if (post == null) return false;

        var vote = FeedbackPostVote.Create(tenantId, postId, cleanEmail);
        _db.FeedbackPostVotes.Add(vote);

        post.UpvoteCount++;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<CommunityFeedbackPost> UpdateRoadmapStatusAsync(
        Guid tenantId,
        Guid postId,
        string newStatus,
        string officialResponse = "")
    {
        var post = await _db.CommunityFeedbackPosts
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == postId);

        if (post == null)
            throw new KeyNotFoundException($"Feedback post '{postId}' not found.");

        post.RoadmapStatus = newStatus.Trim();
        if (!string.IsNullOrWhiteSpace(officialResponse))
        {
            post.OfficialResponse = officialResponse.Trim();
        }

        await _db.SaveChangesAsync();
        return post;
    }

    public async Task<CommunityRoadmapDto> GetPublicRoadmapAsync(Guid tenantId)
    {
        var posts = await _db.CommunityFeedbackPosts
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.UpvoteCount)
            .ToListAsync();

        var planned = posts.Where(p => p.RoadmapStatus.Equals("Planned", StringComparison.OrdinalIgnoreCase)).ToList();
        var inProgress = posts.Where(p => p.RoadmapStatus.Equals("InProgress", StringComparison.OrdinalIgnoreCase)).ToList();
        var completed = posts.Where(p => p.RoadmapStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)).ToList();

        return new CommunityRoadmapDto(tenantId, planned, inProgress, completed);
    }

    public async Task<List<CommunityFeedbackPost>> SearchPostsAsync(Guid tenantId, string query)
    {
        var cleanQuery = query.Trim().ToLowerInvariant();

        return await _db.CommunityFeedbackPosts
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && (p.Title.ToLower().Contains(cleanQuery) || p.DescriptionMarkdown.ToLower().Contains(cleanQuery)))
            .OrderByDescending(p => p.UpvoteCount)
            .Take(20)
            .ToListAsync();
    }
}
