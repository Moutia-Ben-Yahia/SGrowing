namespace SGrowing.Core.Entities;

public enum PublicationVisibility
{
    Public = 1,
    MembersOnly = 2,
    PaidTier = 3
}

public enum PublicationArticleStatus
{
    Draft = 1,
    Scheduled = 2,
    Published = 3,
    Archived = 4
}

public class PublicationArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string ContentLexicalAstJson { get; set; } = "{}";
    public string HtmlContent { get; set; } = string.Empty;
    public string FeaturedImageUrl { get; set; } = string.Empty;
    public string AuthorName { get; set; } = "Editorial Team";
    public PublicationVisibility Visibility { get; set; } = PublicationVisibility.Public;
    public PublicationArticleStatus Status { get; set; } = PublicationArticleStatus.Draft;
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public int ViewCount { get; set; } = 0;
    public int EstimatedReadMinutes { get; set; } = 3;
    public string MetaTitle { get; set; } = string.Empty;
    public string MetaDescription { get; set; } = string.Empty;
    public string CanonicalUrl { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static PublicationArticle Create(
        Guid tenantId,
        string title,
        string slug,
        string excerpt,
        string htmlContent,
        string authorName = "Editorial Team",
        PublicationVisibility visibility = PublicationVisibility.Public,
        string lexicalAstJson = "{}",
        string featuredImageUrl = "")
    {
        int wordCount = htmlContent.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        int readTime = Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));

        return new PublicationArticle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Excerpt = excerpt.Trim(),
            HtmlContent = htmlContent,
            ContentLexicalAstJson = lexicalAstJson,
            FeaturedImageUrl = featuredImageUrl.Trim(),
            AuthorName = authorName.Trim(),
            Visibility = visibility,
            Status = PublicationArticleStatus.Draft,
            EstimatedReadMinutes = readTime,
            MetaTitle = title.Trim(),
            MetaDescription = excerpt.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public enum NewsletterBroadcastStatus
{
    Queued = 1,
    Sending = 2,
    Completed = 3,
    Failed = 4
}

public class NewsletterBroadcast
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ArticleId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string RecipientSegment { get; set; } = "AllSubscribers"; // AllSubscribers, PaidMembers, FreeMembers
    public int TotalRecipientsCount { get; set; } = 0;
    public int DeliveredCount { get; set; } = 0;
    public int OpenedCount { get; set; } = 0;
    public int ClickedCount { get; set; } = 0;
    public NewsletterBroadcastStatus Status { get; set; } = NewsletterBroadcastStatus.Queued;
    public DateTime? SentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static NewsletterBroadcast Create(
        Guid tenantId,
        Guid articleId,
        string subject,
        string recipientSegment = "AllSubscribers",
        int totalRecipients = 0)
    {
        return new NewsletterBroadcast
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArticleId = articleId,
            Subject = subject.Trim(),
            RecipientSegment = recipientSegment,
            TotalRecipientsCount = totalRecipients,
            Status = NewsletterBroadcastStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
