using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SGrowing.Api.Modules.Feedback.MicroSurveys;

public record CreateMicroSurveyRequest(
    string Title,
    string TriggerAction,
    string StepsJson,
    int? RecontactDays = 14,
    int? DisplayLimitPercent = 100
);

public record IngestAnswerRequest(
    Guid SurveyId,
    string RespondentUserId,
    string CurrentAnswersJson,
    int StepIndex,
    bool IsFinished
);

public static class InAppMicroSurveyEndpoints
{
    public static IEndpointRouteBuilder MapInAppMicroSurveyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/feedback/micro-surveys")
            .WithTags("InAppMicroSurveys");

        group.MapPost("/", async (
            Guid tenantId,
            CreateMicroSurveyRequest req,
            IInAppMicroSurveyService service) =>
        {
            var survey = await service.CreateSurveyAsync(
                tenantId,
                req.Title,
                req.TriggerAction,
                req.StepsJson,
                req.RecontactDays ?? 14,
                req.DisplayLimitPercent ?? 100);

            return Results.Ok(survey);
        });

        group.MapGet("/evaluate", async (
            Guid tenantId,
            string respondentUserId,
            string actionName,
            IInAppMicroSurveyService service) =>
        {
            var result = await service.EvaluateClientTriggerAsync(tenantId, respondentUserId, actionName);
            return Results.Ok(result);
        });

        group.MapPost("/responses/partial", async (
            Guid tenantId,
            IngestAnswerRequest req,
            IInAppMicroSurveyService service) =>
        {
            var response = await service.IngestPartialAnswerAsync(
                tenantId,
                req.SurveyId,
                req.RespondentUserId,
                req.CurrentAnswersJson,
                req.StepIndex,
                req.IsFinished);

            return Results.Ok(response);
        });

        group.MapGet("/{surveyId:guid}/analytics", async (
            Guid tenantId,
            Guid surveyId,
            IInAppMicroSurveyService service) =>
        {
            var analytics = await service.GetSurveyAnalyticsAsync(tenantId, surveyId);
            return Results.Ok(analytics);
        });

        return app;
    }
}
