namespace SGrowing.Api.Modules.Social.AutoTriage;

public record InboundSocialMessage(
    string MessageId,
    string Channel,
    string SenderHandle,
    string SenderName,
    string MessageText,
    DateTime TimestampUtc,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public record TriageAssessment(
    string MessageId,
    string Sentiment,
    double SentimentScore,
    string IntentCategory,
    int UrgencyScore,
    bool RequiresImmediateEscalation,
    double Confidence
);

public record TriageQueueItem(
    string QueueId,
    InboundSocialMessage Message,
    TriageAssessment Assessment,
    string AssignedAgentRole,
    DateTime SlaDeadlineUtc,
    string Status,
    string DraftReply
);

public record IngestSocialMessageRequest(
    string Channel,
    string SenderHandle,
    string SenderName,
    string MessageText,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public record ApproveReplyRequest(
    string? CustomizedReplyText = null
);

public record TriageAnalyticsSummary(
    int TotalTriaged,
    int EscalationHazardCount,
    int SalesInquiryCount,
    double SlaCompliancePercent,
    IReadOnlyDictionary<string, int> ChannelBreakdown
);
