using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Feedback.MicroSurveys;

public class InAppMicroSurveyService : IInAppMicroSurveyService
{
    private readonly SGrowingDbContext _db;

    public InAppMicroSurveyService(SGrowingDbContext db)
    {
        _db = db;
    }

    public async Task<InAppMicroSurveyRecord> CreateSurveyAsync(
        Guid tenantId,
        string title,
        string triggerAction,
        string stepsJson,
        int recontactDays = 14,
        int displayLimitPercent = 100)
    {
        var survey = InAppMicroSurveyRecord.Create(
            tenantId,
            title,
            triggerAction,
            stepsJson,
            recontactDays,
            displayLimitPercent);

        _db.InAppMicroSurveyRecords.Add(survey);
        await _db.SaveChangesAsync();

        return survey;
    }

    public async Task<SurveyEvaluationResultDto> EvaluateClientTriggerAsync(
        Guid tenantId,
        string respondentUserId,
        string actionName)
    {
        var cleanAction = actionName.Trim().ToLowerInvariant();

        // 1. Find active survey for this trigger
        var survey = await _db.InAppMicroSurveyRecords
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive && s.TriggerAction == cleanAction)
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();

        if (survey == null)
        {
            return new SurveyEvaluationResultDto(false, null, null, cleanAction, null, "No active survey for this action");
        }

        // 2. Check Recontact Guard: check if respondent had any response recently
        var cutoff = DateTime.UtcNow.AddDays(-survey.RecontactDays);
        var recentResponse = await _db.MicroSurveyResponsePartials
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.RespondentUserId == respondentUserId.Trim() && r.LastUpdatedAtUtc >= cutoff)
            .FirstOrDefaultAsync();

        if (recentResponse != null)
        {
            return new SurveyEvaluationResultDto(
                false,
                survey.Id,
                survey.Title,
                cleanAction,
                null,
                $"Recontact frequency limit active ({survey.RecontactDays} days guard)");
        }

        // 3. Traffic sampling limit check
        if (survey.DisplayLimitPercent < 100)
        {
            var hash = Math.Abs(respondentUserId.GetHashCode()) % 100;
            if (hash >= survey.DisplayLimitPercent)
            {
                return new SurveyEvaluationResultDto(
                    false,
                    survey.Id,
                    survey.Title,
                    cleanAction,
                    null,
                    "Excluded by traffic sampling rate");
            }
        }

        return new SurveyEvaluationResultDto(
            true,
            survey.Id,
            survey.Title,
            cleanAction,
            survey.StepsJson,
            "Trigger evaluated and eligible");
    }

    public async Task<MicroSurveyResponsePartial> IngestPartialAnswerAsync(
        Guid tenantId,
        Guid surveyId,
        string respondentUserId,
        string currentAnswersJson,
        int stepIndex,
        bool isFinished)
    {
        var existing = await _db.MicroSurveyResponsePartials
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.SurveyId == surveyId && r.RespondentUserId == respondentUserId.Trim() && !r.IsFinished);

        if (existing == null)
        {
            existing = MicroSurveyResponsePartial.Create(
                tenantId,
                surveyId,
                respondentUserId,
                currentAnswersJson,
                stepIndex);
            existing.IsFinished = isFinished;
            _db.MicroSurveyResponsePartials.Add(existing);
        }
        else
        {
            existing.UpdateAnswer(currentAnswersJson, stepIndex, isFinished);
        }

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<MicroSurveyAnalyticsDto> GetSurveyAnalyticsAsync(Guid tenantId, Guid surveyId)
    {
        var survey = await _db.InAppMicroSurveyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == surveyId);

        var title = survey?.Title ?? "Micro Survey";
        var responses = await _db.MicroSurveyResponsePartials
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.SurveyId == surveyId)
            .ToListAsync();

        int starts = responses.Count;
        int completions = responses.Count(r => r.IsFinished);
        double overallCompletionPct = starts > 0 ? Math.Round(((double)completions / starts) * 100.0, 1) : 0.0;

        // Build 3-step default funnel
        var funnelSteps = new List<SurveyFunnelStepMetricDto>();
        for (int i = 1; i <= 3; i++)
        {
            int stepReached = responses.Count(r => r.LastCompletedStepIndex >= i || r.IsFinished);
            int stepCompleted = responses.Count(r => (r.LastCompletedStepIndex > i) || r.IsFinished);
            double dropoff = stepReached > 0 ? Math.Round(((double)(stepReached - stepCompleted) / stepReached) * 100.0, 1) : 0.0;

            funnelSteps.Add(new SurveyFunnelStepMetricDto(
                i,
                $"Question {i}",
                stepReached,
                stepCompleted,
                dropoff));
        }

        return new MicroSurveyAnalyticsDto(
            surveyId,
            title,
            starts,
            completions,
            overallCompletionPct,
            funnelSteps);
    }
}
