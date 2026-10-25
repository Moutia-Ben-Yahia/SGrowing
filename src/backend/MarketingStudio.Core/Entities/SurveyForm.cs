namespace MarketingStudio.Core.Entities;

public enum SurveyType
{
    Csat = 1,
    Nps = 2,
    LeadCapture = 3
}

public class SurveyForm
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Title { get; set; } = string.Empty;
    public SurveyType Type { get; set; } = SurveyType.Csat;
    public string QuestionPrompt { get; set; } = string.Empty;
    public int ScaleMax { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; private set; }

    private SurveyForm() { }

    public static SurveyForm Create(Guid tenantId, string title, SurveyType type, string questionPrompt, int scaleMax = 5)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));

        return new SurveyForm
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = title,
            Type = type,
            QuestionPrompt = questionPrompt,
            ScaleMax = scaleMax,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
