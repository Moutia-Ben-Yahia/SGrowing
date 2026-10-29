using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Feedback.MicroSurveys;

public record SurveyEvaluationResultDto(
    bool ShouldDisplay,
    Guid? SurveyId,
    string? Title,
    string? TriggerAction,
    string? StepsJson,
    string Reason
);

public record SurveyFunnelStepMetricDto(
    int StepIndex,
    string StepName,
    int Impressions,
    int Completions,
    double DropoffRatePercent
);

public record MicroSurveyAnalyticsDto(
    Guid SurveyId,
    string Title,
    int TotalStarts,
    int TotalCompletions,
    double OverallCompletionRatePercent,
    List<SurveyFunnelStepMetricDto> FunnelSteps
);

public interface IInAppMicroSurveyService
{
    Task<InAppMicroSurveyRecord> CreateSurveyAsync(
        Guid tenantId,
        string title,
        string triggerAction,
        string stepsJson,
        int recontactDays = 14,
        int displayLimitPercent = 100);

    Task<SurveyEvaluationResultDto> EvaluateClientTriggerAsync(
        Guid tenantId,
        string respondentUserId,
        string actionName);

    Task<MicroSurveyResponsePartial> IngestPartialAnswerAsync(
        Guid tenantId,
        Guid surveyId,
        string respondentUserId,
        string currentAnswersJson,
        int stepIndex,
        bool isFinished);

    Task<MicroSurveyAnalyticsDto> GetSurveyAnalyticsAsync(
        Guid tenantId,
        Guid surveyId);
}
