using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations.Sla;

public enum SlaEscalationTier
{
    Normal,
    Warning, // 75% elapsed
    Breached, // 100% elapsed
    Critical // 150% elapsed
}

public record SlaEscalationItemDto(
    Guid ConversationId,
    ConversationPriority Priority,
    string PolicyName,
    int ElapsedMinutes,
    int TargetMinutes,
    SlaEscalationTier EscalationTier,
    bool PriorityElevated,
    string RecommendedAction,
    DateTime DetectedAtUtc
);

public record SlaEscalationEvaluationResult(
    Guid TenantId,
    DateTime EvaluatedAtUtc,
    int TotalActiveEvaluated,
    int TotalWarning,
    int TotalBreached,
    int TotalCritical,
    List<SlaEscalationItemDto> Escalations
);

public record CsatSurveyDispatchResult(
    Guid SurveyId,
    Guid ConversationId,
    string Channel,
    string SurveyUrl,
    string FormattedMessage,
    DateTime DispatchedAtUtc
);

public interface ISlaEscalationEngineService
{
    Task<CsatSurveyDispatchResult> TriggerCsatSurveyAsync(
        Guid tenantId,
        Guid conversationId,
        string channel = "email",
        string baseUrl = "https://feedback.sgrowing.io",
        CancellationToken ct = default);

    Task<SlaEscalationEvaluationResult> EvaluateEscalationPoliciesAsync(
        Guid tenantId,
        CancellationToken ct = default);
}
