using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Conversations.PresenceAndCsat;

public record CsatMetricsSummaryDto(
    Guid TenantId,
    int TotalResponses,
    double AverageScore,
    double SatisfactionPercent,
    double NetPromoterScore,
    Dictionary<string, int> ChannelBreakdown
);

public interface ICsatSurveyService
{
    Task<CsatSurveyResult> RecordCsatResponseAsync(
        Guid tenantId,
        Guid conversationId,
        Guid customerContactId,
        string customerName,
        int score,
        string channel = "WhatsApp",
        string feedbackText = "");

    Task<CsatSurveyResult?> GetConversationCsatAsync(
        Guid tenantId,
        Guid conversationId);

    Task<CsatMetricsSummaryDto> GetTenantCsatMetricsAsync(
        Guid tenantId);
}
