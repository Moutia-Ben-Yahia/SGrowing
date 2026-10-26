namespace SGrowing.Core.Entities;

public enum FeedbackStatus
{
    Open = 1,
    UnderReview = 2,
    Planned = 3,
    InProgress = 4,
    Completed = 5,
    Closed = 6
}

public class FeatureFeedbackPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Feature"; // Feature, Integration, UI, Performance, Mobile
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;
    public int UpvoteCount { get; set; } = 1;
    public string AuthorEmail { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static FeatureFeedbackPost Create(
        Guid tenantId,
        string title,
        string description,
        string category = "Feature",
        string authorEmail = "",
        string authorName = "")
    {
        return new FeatureFeedbackPost
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            Description = description.Trim(),
            Category = category.Trim(),
            Status = FeedbackStatus.Open,
            UpvoteCount = 1,
            AuthorEmail = authorEmail.Trim().ToLowerInvariant(),
            AuthorName = authorName.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class FeatureFeedbackVote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PostId { get; set; }
    public string VoterIdentifier { get; set; } = string.Empty; // User ID or Email
    public DateTime VotedAtUtc { get; set; } = DateTime.UtcNow;

    public static FeatureFeedbackVote Create(
        Guid tenantId,
        Guid postId,
        string voterIdentifier)
    {
        return new FeatureFeedbackVote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PostId = postId,
            VoterIdentifier = voterIdentifier.Trim().ToLowerInvariant(),
            VotedAtUtc = DateTime.UtcNow
        };
    }
}
