namespace SGrowing.Api.Modules.Support.TicketEscalation;

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Urgent
}

public enum SupportTier
{
    Tier1_Frontline,
    Tier2_TechnicalSpecialist,
    Tier3_EngineeringOps,
    ExecutiveOnCall
}

public enum EscalationState
{
    Normal,
    WarningSlaDueSoon,
    EscalatedTier2,
    EscalatedTier3,
    Resolved
}

public enum CustomerSentiment
{
    Positive,
    Neutral,
    Frustrated,
    CriticalChurnRisk
}

public record TicketEscalationLogRecord(
    Guid LogId,
    Guid TicketId,
    SupportTier FromTier,
    SupportTier ToTier,
    string Reason,
    string TriggeredBy,
    DateTime TimestampUtc
);

public record SupportTicketRecord(
    Guid TicketId,
    Guid TenantId,
    string TicketNumber,
    string Subject,
    string CustomerName,
    string CustomerEmail,
    string CompanyName,
    TicketPriority Priority,
    SupportTier CurrentTier,
    EscalationState State,
    CustomerSentiment Sentiment,
    string AssignedAgent,
    string Category,
    DateTime CreatedAtUtc,
    DateTime SlaDueAtUtc,
    DateTime? FirstResponseAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionNotes,
    List<TicketEscalationLogRecord> EscalationLogs,
    List<string> Tags
);

public record CreateTicketRequest(
    Guid TenantId,
    string Subject,
    string CustomerName,
    string CustomerEmail,
    string CompanyName,
    TicketPriority Priority,
    string Category,
    CustomerSentiment Sentiment,
    string InitialNotes
);

public record EscalateTicketRequest(
    Guid TenantId,
    Guid TicketId,
    SupportTier TargetTier,
    string Reason,
    string EscalatedBy
);

public record ResolveTicketRequest(
    Guid TenantId,
    Guid TicketId,
    string ResolutionNotes,
    string ResolvedBy
);

public record SuggestCannedMacroResponse(
    Guid TicketId,
    string MacroTitle,
    string RecommendedResponse,
    CustomerSentiment TargetSentiment,
    List<string> ActionSteps,
    double ConfidenceScore
);

public record TicketEscalationOverviewReport(
    Guid TenantId,
    int TotalActiveTickets,
    int WarningSlaCount,
    int EscalatedTier2Count,
    int EscalatedTier3Count,
    int ResolvedTodayCount,
    double SlaCompliancePercentage,
    List<SupportTicketRecord> Tickets
);
