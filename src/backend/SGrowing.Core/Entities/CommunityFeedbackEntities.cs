namespace SGrowing.Core.Entities;

public class CommunityFeedbackPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string DescriptionMarkdown { get; set; } = string.Empty;
    public int UpvoteCount { get; set; } = 1;
    public string RoadmapStatus { get; set; } = "UnderReview"; // "UnderReview", "Planned", "InProgress", "Completed", "Declined"
    public Guid? CanonicalPostId { get; set; } // If marked as duplicate, points to canonical idea
    public string AuthorEmail { get; set; } = string.Empty;
    public string OfficialResponse { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static CommunityFeedbackPost Create(
        Guid tenantId,
        string title,
        string description,
        string authorEmail,
        string status = "UnderReview")
    {
        var slug = title.Trim().ToLowerInvariant().Replace(" ", "-").Replace("/", "-");
        return new CommunityFeedbackPost
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            Slug = slug,
            DescriptionMarkdown = description.Trim(),
            AuthorEmail = authorEmail.Trim(),
            UpvoteCount = 1,
            RoadmapStatus = status.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class FeedbackPostVote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PostId { get; set; }
    public string VoterEmail { get; set; } = string.Empty;
    public DateTime VotedAtUtc { get; set; } = DateTime.UtcNow;

    public static FeedbackPostVote Create(Guid tenantId, Guid postId, string voterEmail)
    {
        return new FeedbackPostVote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PostId = postId,
            VoterEmail = voterEmail.Trim().ToLowerInvariant(),
            VotedAtUtc = DateTime.UtcNow
        };
    }
}
