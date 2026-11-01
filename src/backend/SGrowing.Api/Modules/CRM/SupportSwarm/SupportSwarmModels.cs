using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.CRM.SupportSwarm;

/// <summary>
/// Omnichannel customer touchpoint channels for incoming support tickets.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SupportChannel
{
    WhatsApp,
    WebChat,
    Telegram,
    Email,
    Sms
}

/// <summary>
/// Domain categories for customer support inquiries.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketCategory
{
    OrderDeliveryLogistics,
    BillingAndRefunds,
    TechnicalProduct,
    AccountAndSecurity,
    WholesaleB2B
}

/// <summary>
/// Ticket priority tiers determining queue velocity and escalation path.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketPriority
{
    Low,
    Medium,
    High,
    UrgentVip
}

/// <summary>
/// Specialized AI fleet personas and human supervisor fallback tiers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentPersona
{
    LogisticsDispatcherAi,
    BillingSpecialistAi,
    ProductAdvisorAi,
    SeniorHumanSupervisor
}

/// <summary>
/// Customer satisfaction risk classifications calculated by the real-time CSAT Radar.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CsatRiskLevel
{
    LowRisk,
    ModerateAttention,
    HighRiskChurnCandidate,
    CriticalInterventionNeeded
}

/// <summary>
/// Ticket lifecycle states.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TicketStatus
{
    Open,
    InProgress,
    Escalated,
    Resolved
}

/// <summary>
/// Represents an omnichannel customer support ticket in the agentic swarm routing matrix.
/// </summary>
public sealed record CustomerSupportTicket(
    string TicketId,
    string CustomerId,
    SupportChannel Channel,
    TicketCategory Category,
    TicketPriority Priority,
    decimal CustomerLtvDzd,
    double SentimentScore,
    AgentPersona AssignedPersona,
    TicketStatus Status,
    DateTime CreatedAtUtc,
    string? Subject = null,
    string? InitialMessage = null,
    string? EscalationReason = null,
    string? ReassignedReason = null,
    DateTime? UpdatedAtUtc = null,
    DateTime? ResolvedAtUtc = null,
    string? ResolutionNotes = null
);

/// <summary>
/// Represents an individual message within a support ticket conversation thread.
/// </summary>
public sealed record SupportConversationMessage(
    string MessageId,
    string TicketId,
    string SenderType,
    string Text,
    double SentimentScore,
    DateTime TimestampUtc
);

/// <summary>
/// Real-time alert emitted when the CSAT Radar detects satisfaction degradation or legal/fraud threats.
/// </summary>
public sealed record CsatDropAlert(
    string AlertId,
    string TicketId,
    string CustomerId,
    double PredictedCsatScore,
    CsatRiskLevel RiskLevel,
    string TriggerReason,
    decimal SuggestedDeEscalationDiscountPct,
    DateTime EmittedAtUtc,
    bool Acknowledged,
    DateTime? AcknowledgedAtUtc = null,
    string? AcknowledgedBy = null
);

/// <summary>
/// Canned action and synthesized resolution macro extracted from high-satisfaction interactions.
/// </summary>
public sealed record AutoSynthesizedMacro(
    string MacroId,
    TicketCategory Category,
    string Title,
    string TemplateText,
    double ConfidenceScore,
    double ConversionSuccessRate,
    int UsageCount,
    DateTime CreatedAtUtc,
    int SuccessfulResolutionsCount = 0,
    DateTime? LastUsedAtUtc = null
);

/// <summary>
/// Real-time load and capacity metrics for an AI or human agent persona.
/// </summary>
public sealed record AgentWorkload(
    AgentPersona Persona,
    int ActiveTicketsCount,
    int MaxCapacity,
    bool IsAvailable
);

/// <summary>
/// Request payload to route an incoming support ticket.
/// </summary>
public sealed record RouteTicketRequest(
    string? TicketId,
    string CustomerId,
    SupportChannel Channel,
    TicketCategory Category,
    TicketPriority Priority,
    decimal CustomerLtvDzd,
    double SentimentScore,
    string? Subject = null,
    string? InitialMessage = null
);

/// <summary>
/// Response payload after routing a ticket through the swarm matrix.
/// </summary>
public sealed record RouteTicketResponse(
    string TicketId,
    AgentPersona AssignedPersona,
    TicketPriority FinalPriority,
    TicketStatus Status,
    string RoutingReason,
    DateTime AssignedAtUtc,
    CustomerSupportTicket Ticket
);

/// <summary>
/// Request payload to reassign an active ticket to a new persona.
/// </summary>
public sealed record ReassignTicketRequest(
    AgentPersona NewPersona,
    string Reason
);

/// <summary>
/// Request payload to scan an ongoing conversation for CSAT degradation and escalation triggers.
/// </summary>
public sealed record ScanConversationRequest(
    string TicketId,
    string CustomerId,
    IReadOnlyList<SupportConversationMessage> Messages
);

/// <summary>
/// Result of in-conversation CSAT Radar analysis.
/// </summary>
public sealed record CsatAnalysisResult(
    string TicketId,
    string CustomerId,
    double PredictedCsatScore,
    CsatRiskLevel RiskLevel,
    double SentimentDelta,
    bool TriggerKeywordsDetected,
    IReadOnlyList<string> MatchedKeywords,
    bool FrustrationLoopDetected,
    decimal SuggestedDeEscalationDiscountPct,
    CsatDropAlert? Alert
);

/// <summary>
/// Request payload to synthesize a new macro from a successful ticket resolution.
/// </summary>
public sealed record SynthesizeMacroRequest(
    TicketCategory Category,
    string Title,
    string ResolutionText,
    double Confidence = 0.90
);

/// <summary>
/// Request payload to record usage outcome feedback for a synthesized macro.
/// </summary>
public sealed record RecordMacroUsageRequest(
    bool ResolvedIssue
);

/// <summary>
/// Overall summary of the support swarm fleet status.
/// </summary>
public sealed record SupportSwarmSummaryResponse(
    int TotalActiveTickets,
    int TotalEscalatedTickets,
    int ActiveAlertsCount,
    int TotalMacrosCount,
    IReadOnlyList<AgentWorkload> AgentWorkloads
);
