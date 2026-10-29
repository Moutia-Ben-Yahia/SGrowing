namespace SGrowing.Core.Entities;

public class InAppMicroSurveyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TriggerAction { get; set; } = "checkout_completed"; // e.g. "checkout_abandoned", "pricing_viewed_3x"
    public int RecontactDays { get; set; } = 14;
    public int DisplayLimitPercent { get; set; } = 100;
    public string StepsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static InAppMicroSurveyRecord Create(
        Guid tenantId,
        string title,
        string triggerAction,
        string stepsJson,
        int recontactDays = 14,
        int displayLimitPercent = 100)
    {
        return new InAppMicroSurveyRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title.Trim(),
            TriggerAction = triggerAction.Trim().ToLowerInvariant(),
            StepsJson = stepsJson,
            RecontactDays = Math.Max(1, recontactDays),
            DisplayLimitPercent = Math.Clamp(displayLimitPercent, 1, 100),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class MicroSurveyResponsePartial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid SurveyId { get; set; }
    public string RespondentUserId { get; set; } = string.Empty;
    public string AnswersJson { get; set; } = "{}";
    public int LastCompletedStepIndex { get; set; } = 1;
    public bool IsFinished { get; set; } = false;
    public string DropoffStep { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public static MicroSurveyResponsePartial Create(
        Guid tenantId,
        Guid surveyId,
        string respondentUserId,
        string initialAnswerJson,
        int stepIndex = 1)
    {
        return new MicroSurveyResponsePartial
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SurveyId = surveyId,
            RespondentUserId = respondentUserId.Trim(),
            AnswersJson = initialAnswerJson,
            LastCompletedStepIndex = stepIndex,
            IsFinished = false,
            DropoffStep = $"Step_{stepIndex}",
            StartedAtUtc = DateTime.UtcNow,
            LastUpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateAnswer(string updatedAnswersJson, int stepIndex, bool isFinished)
    {
        AnswersJson = updatedAnswersJson;
        LastCompletedStepIndex = stepIndex;
        IsFinished = isFinished;
        DropoffStep = isFinished ? "Completed" : $"Step_{stepIndex}";
        LastUpdatedAtUtc = DateTime.UtcNow;
    }
}
