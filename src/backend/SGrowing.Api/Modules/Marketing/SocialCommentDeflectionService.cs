using SGrowing.Api.Data;
using SGrowing.Core.Entities;
using SGrowing.Core.Outbox;

namespace SGrowing.Api.Modules.Marketing;

public record DeflectionResult(bool Deflected, string? ReplyText);

public record SocialDirectMessageTaskPayload(
    Guid TenantId,
    SocialPlatform Platform,
    string PlatformName,
    string PostExternalId,
    string CommenterUsername,
    string CommentText,
    string DirectMessageText,
    DateTime CreatedAtUtc
);

public class SocialCommentDeflectionService
{
    private static readonly string[] IntentKeywords =
    [
        "price", "pricing", "cost", "demo", "buy", "purchase", "help", "support", "info", "link"
    ];

    private readonly IOutbox _outbox;

    public SocialCommentDeflectionService(IOutbox outbox)
    {
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    }

    public SocialCommentDeflectionService(SGrowingDbContext db, IOutbox outbox)
        : this(outbox)
    {
    }

    public async Task<DeflectionResult> HandlePublicCommentAsync(
        Guid tenantId,
        SocialPlatform platform,
        string postExternalId,
        string commenterUsername,
        string commentText,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(commentText))
        {
            return new DeflectionResult(false, null);
        }

        var hasIntent = IntentKeywords.Any(k => commentText.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (!hasIntent)
        {
            return new DeflectionResult(false, null);
        }

        var dmPayload = new SocialDirectMessageTaskPayload(
            TenantId: tenantId,
            Platform: platform,
            PlatformName: platform.ToString(),
            PostExternalId: postExternalId,
            CommenterUsername: commenterUsername,
            CommentText: commentText,
            DirectMessageText: $"Hi @{commenterUsername}! We saw your question regarding our latest post. We're happy to share more details and help answer any questions you have.",
            CreatedAtUtc: DateTime.UtcNow
        );

        await _outbox.EnqueueAsync(tenantId, "Social.DirectMessage.Send", dmPayload, cancellationToken);

        return new DeflectionResult(true, "Sent you a DM with details!");
    }
}
