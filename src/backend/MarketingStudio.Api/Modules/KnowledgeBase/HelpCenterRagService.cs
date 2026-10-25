using MarketingStudio.Api.Data;
using MarketingStudio.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarketingStudio.Api.Modules.KnowledgeBase;

public record RagDeflectionResult(
    bool Deflected,
    string Answer,
    string? ArticleTitle,
    string? ArticleSlug,
    int ConfidenceScore
);

public class HelpCenterRagService
{
    private readonly MarketingStudioDbContext _db;

    public HelpCenterRagService(MarketingStudioDbContext db)
    {
        _db = db;
    }

    public async Task<RagDeflectionResult> GenerateAiDeflectionReplyAsync(
        Guid tenantId,
        string customerQuestion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerQuestion))
        {
            return new RagDeflectionResult(false, "Please provide a question so I can assist you.", null, null, 0);
        }

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "what", "is", "how", "to", "the", "a", "an", "can", "i", "you", "my", "our", "do", "does", "with", "for", "in", "on", "please", "help"
        };

        var keywords = customerQuestion
            .Split(new[] { ' ', '?', '!', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (keywords.Count == 0)
        {
            return new RagDeflectionResult(false, "I'm connecting you with a human support specialist.", null, null, 20);
        }

        var publishedArticles = await _db.HelpArticles
            .Where(a => a.TenantId == tenantId && a.IsPublished)
            .ToListAsync(cancellationToken);

        HelpArticle? bestMatch = null;
        int maxScore = 0;

        foreach (var article in publishedArticles)
        {
            int score = 0;
            var titleLower = article.Title.ToLowerInvariant();
            var contentLower = article.ContentMarkdown.ToLowerInvariant();

            foreach (var kw in keywords)
            {
                if (titleLower.Contains(kw)) score += 3;
                if (contentLower.Contains(kw)) score += 1;
            }

            if (score > maxScore)
            {
                maxScore = score;
                bestMatch = article;
            }
        }

        if (bestMatch != null && maxScore >= 2)
        {
            bestMatch.IncrementViewCount();
            await _db.SaveChangesAsync(cancellationToken);

            var summary = bestMatch.ContentMarkdown.Length > 200
                ? bestMatch.ContentMarkdown.Substring(0, 200) + "..."
                : bestMatch.ContentMarkdown;

            var answer = $"Based on our documentation '{bestMatch.Title}':\n\n{summary}\n\nRead the full guide here: /help/{bestMatch.Slug}";
            return new RagDeflectionResult(true, answer, bestMatch.Title, bestMatch.Slug, 90);
        }

        return new RagDeflectionResult(
            false,
            "I couldn't find a direct article addressing this in our Help Center. I have notified our support team to step in.",
            null,
            null,
            35
        );
    }
}
