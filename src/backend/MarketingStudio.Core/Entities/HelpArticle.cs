namespace MarketingStudio.Core.Entities;

public class HelpArticle
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
    public bool IsPublished { get; set; } = true;
    public int ViewCount { get; private set; } = 0;
    public DateTime CreatedAtUtc { get; private set; }

    private HelpArticle() { }

    public static HelpArticle Create(
        Guid tenantId,
        Guid categoryId,
        string title,
        string slug,
        string contentMarkdown,
        bool isPublished = true)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (categoryId == Guid.Empty) throw new ArgumentException("CategoryId is required.", nameof(categoryId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));

        return new HelpArticle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = categoryId,
            Title = title,
            Slug = slug.ToLowerInvariant().Trim(),
            ContentMarkdown = contentMarkdown,
            IsPublished = isPublished,
            ViewCount = 0,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void IncrementViewCount()
    {
        ViewCount++;
    }
}
