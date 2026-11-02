namespace SGrowing.Api.Modules.Conversations.Unified;

public enum ConversationChannel
{
    Email,
    WhatsApp,
    Sms,
    WebChat
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Urgent
}

public enum TicketStatus
{
    Open,
    PendingCustomer,
    Resolved,
    Escalated
}

public enum CustomerSentiment
{
    Positive,
    Neutral,
    Negative,
    Frustrated
}

public record CustomerMessage(
    Guid Id,
    Guid ThreadId,
    string TenantId,
    ConversationChannel Channel,
    string SenderId,
    string SenderName,
    string Content,
    CustomerSentiment Sentiment,
    bool IsFromCustomer,
    DateTime TimestampUtc
);

public record UnifiedThread(
    Guid Id,
    string TenantId,
    string CustomerEmail,
    string CustomerPhone,
    List<ConversationChannel> ActiveChannels,
    List<CustomerMessage> Messages,
    TicketStatus Status,
    TicketPriority Priority,
    DateTime SlaDueAtUtc,
    bool SlaBreached,
    string? AssignedAgentId,
    bool AiDeflected,
    DateTime UpdatedAtUtc
);

public record IngestMessageRequest(
    string TenantId,
    ConversationChannel Channel,
    string CustomerEmail,
    string CustomerPhone,
    string SenderName,
    string Content,
    bool IsFromCustomer = true
);

public record AiDeflectionResult(
    Guid ThreadId,
    string SuggestedResponse,
    double ConfidenceScore,
    bool CanAutoSend,
    string SourceArticle
);

public record EscalateTicketRequest(
    Guid ThreadId,
    string TenantId,
    string Reason,
    TicketPriority NewPriority
);

public record AppendAgentReplyRequest(
    string TenantId,
    string AgentId,
    string AgentName,
    string Content,
    ConversationChannel Channel
);
