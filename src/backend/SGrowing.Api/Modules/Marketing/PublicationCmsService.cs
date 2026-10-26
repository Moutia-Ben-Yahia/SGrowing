using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.Marketing;

public record RenderedArticleDto(
    Guid Id,
    string Title,
    string Slug,
    string Excerpt,
    string RenderedHtml,
    string AuthorName,
    PublicationVisibility Visibility,
    bool IsPaywalled,
    string? PaywallCtaMessage,
    int EstimatedReadMinutes,
    int ViewCount,
    DateTime? PublishedAtUtc
);

public interface IPublicationCmsService
{
    Task<PublicationArticle> CreateArticleAsync(Guid tenantId, string title, string slug, string excerpt, string htmlContent, string authorName = "Editorial Team", PublicationVisibility visibility = PublicationVisibility.Public, string lexicalAstJson = "{}", string featuredImageUrl = "");
    Task<PublicationArticle> PublishArticleAsync(Guid tenantId, Guid articleId);
    Task<RenderedArticleDto?> RenderArticleForReaderAsync(Guid tenantId, string slug, bool isReaderMember = false, bool isReaderPaid = false);
    Task<NewsletterBroadcast> BroadcastArticleNewsletterAsync(Guid tenantId, Guid articleId, string subject, string recipientSegment = "AllSubscribers", int estimatedAudience = 1500);
    Task<List<PublicationArticle>> GetArticlesAsync(Guid tenantId, PublicationArticleStatus? status = null);
}

public class PublicationCmsService : IPublicationCmsService
{
    private readonly SGrowingDbContext _db;
    private readonly IOutbox _outbox;

    public PublicationCmsService(SGrowingDbContext db, IOutbox outbox)
    {
        _db = db;
        _outbox = outbox;
    }

    public async Task<PublicationArticle> CreateArticleAsync(
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
        var existing = await _db.PublicationArticles.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Slug == slug.ToLower().Trim());
        if (existing != null) return existing;

        var article = PublicationArticle.Create(tenantId, title, slug, excerpt, htmlContent, authorName, visibility, lexicalAstJson, featuredImageUrl);
        _db.PublicationArticles.Add(article);
        await _db.SaveChangesAsync();
        return article;
    }

    public async Task<PublicationArticle> PublishArticleAsync(Guid tenantId, Guid articleId)
    {
        var article = await _db.PublicationArticles.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == articleId);
        if (article == null)
            throw new KeyNotFoundException($"Article {articleId} not found.");

        article.Status = PublicationArticleStatus.Published;
        article.PublishedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _outbox.EnqueueAsync(tenantId, "Publication.ArticlePublished", new
        {
            ArticleId = article.Id,
            article.Title,
            article.Slug,
            article.PublishedAtUtc
        });

        return article;
    }

    public async Task<RenderedArticleDto?> RenderArticleForReaderAsync(Guid tenantId, string slug, bool isReaderMember = false, bool isReaderPaid = false)
    {
        var article = await _db.PublicationArticles.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Slug == slug.ToLower().Trim());
        if (article == null) return null;

        article.ViewCount++;
        await _db.SaveChangesAsync();

        bool isPaywalled = false;
        string? paywallCta = null;
        string renderedHtml = article.HtmlContent;

        // Paywall Evaluation (Ghost-style visibility tiers)
        if (article.Visibility == PublicationVisibility.MembersOnly && !isReaderMember && !isReaderPaid)
        {
            isPaywalled = true;
            paywallCta = "This publication is reserved for free registered members. Subscribe to continue reading.";
            renderedHtml = TruncateTeaser(article.Excerpt, article.HtmlContent);
        }
        else if (article.Visibility == PublicationVisibility.PaidTier && !isReaderPaid)
        {
            isPaywalled = true;
            paywallCta = "Exclusive Premium Insight. Upgrade to our Premium Membership to unlock full access.";
            renderedHtml = TruncateTeaser(article.Excerpt, article.HtmlContent);
        }

        return new RenderedArticleDto(
            Id: article.Id,
            Title: article.Title,
            Slug: article.Slug,
            Excerpt: article.Excerpt,
            RenderedHtml: renderedHtml,
            AuthorName: article.AuthorName,
            Visibility: article.Visibility,
            IsPaywalled: isPaywalled,
            PaywallCtaMessage: paywallCta,
            EstimatedReadMinutes: article.EstimatedReadMinutes,
            ViewCount: article.ViewCount,
            PublishedAtUtc: article.PublishedAtUtc
        );
    }

    public async Task<NewsletterBroadcast> BroadcastArticleNewsletterAsync(
        Guid tenantId,
        Guid articleId,
        string subject,
        string recipientSegment = "AllSubscribers",
        int estimatedAudience = 1500)
    {
        var article = await _db.PublicationArticles.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == articleId);
        if (article == null)
            throw new KeyNotFoundException($"Article {articleId} not found.");

        var broadcast = NewsletterBroadcast.Create(tenantId, articleId, subject, recipientSegment, estimatedAudience);
        broadcast.Status = NewsletterBroadcastStatus.Completed;
        broadcast.DeliveredCount = (int)(estimatedAudience * 0.98);
        broadcast.SentAtUtc = DateTime.UtcNow;

        _db.NewsletterBroadcasts.Add(broadcast);
        await _db.SaveChangesAsync();

        await _outbox.EnqueueAsync(tenantId, "Publication.NewsletterDispatched", new
        {
            BroadcastId = broadcast.Id,
            ArticleId = article.Id,
            Subject = broadcast.Subject,
            RecipientsCount = broadcast.DeliveredCount,
            DispatchedAtUtc = broadcast.SentAtUtc
        });

        return broadcast;
    }

    public async Task<List<PublicationArticle>> GetArticlesAsync(Guid tenantId, PublicationArticleStatus? status = null)
    {
        var query = _db.PublicationArticles.Where(a => a.TenantId == tenantId);
        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }
        return await query.OrderByDescending(a => a.CreatedAtUtc).ToListAsync();
    }

    private static string TruncateTeaser(string excerpt, string fullHtml)
    {
        if (!string.IsNullOrWhiteSpace(excerpt))
            return $"<p class=\"lead-excerpt\">{excerpt}</p><div class=\"paywall-blur paywall-lock-banner\">...</div>";

        var preview = fullHtml.Length > 300 ? fullHtml[..300] : fullHtml;
        return $"{preview}<div class=\"paywall-divider paywall-lock-banner\">[Locked Content Hidden]</div>";
    }
}
