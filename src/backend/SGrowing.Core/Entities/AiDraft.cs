namespace SGrowing.Core.Entities;

public enum AiDraftStatus
{
    Drafted = 1,
    Accepted = 2,
    Rejected = 3,
    AutoSent = 4
}

public class AiDraft
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ConversationId { get; private set; }
    public string SuggestedReply { get; set; } = string.Empty;
    public float ConfidenceScore { get; set; }
    public string SourcesJson { get; set; } = "[]";
    public AiDraftStatus Status { get; set; } = AiDraftStatus.Drafted;
    public DateTime CreatedAtUtc { get; private set; }

    private AiDraft() { }

    public static AiDraft Create(Guid tenantId, Guid conversationId, string reply, float confidence, string sourcesJson = "[]")
    {
        return new AiDraft
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ConversationId = conversationId,
            SuggestedReply = reply,
            ConfidenceScore = confidence,
            SourcesJson = sourcesJson,
            Status = AiDraftStatus.Drafted,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
