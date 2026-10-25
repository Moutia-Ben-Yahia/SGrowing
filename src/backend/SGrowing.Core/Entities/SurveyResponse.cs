namespace SGrowing.Core.Entities;

public enum FeedbackSentiment
{
    Positive = 1,
    Neutral = 2,
    Negative = 3
}

public class SurveyResponse
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid SurveyFormId { get; private set; }
    public Guid ContactId { get; private set; }
    public int Score { get; private set; }
    public string? FeedbackText { get; private set; }
    public FeedbackSentiment Sentiment { get; private set; } = FeedbackSentiment.Neutral;
    public DateTime CreatedAtUtc { get; private set; }

    private SurveyResponse() { }

    public static SurveyResponse Create(
        Guid tenantId,
        Guid surveyFormId,
        Guid contactId,
        int score,
        string? feedbackText,
        FeedbackSentiment sentiment)
    {
        return new SurveyResponse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SurveyFormId = surveyFormId,
            ContactId = contactId,
            Score = score,
            FeedbackText = feedbackText,
            Sentiment = sentiment,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
