namespace SGrowing.Core.Entities;

public enum JourneyStepType
{
    SendEmail = 1,
    SendWhatsApp = 2,
    Delay = 3
}

public class JourneyStep
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid JourneyId { get; private set; }
    public int StepOrder { get; set; }
    public JourneyStepType StepType { get; set; } = JourneyStepType.SendEmail;
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public int DelaySeconds { get; set; } = 0;
    public DateTime CreatedAtUtc { get; private set; }

    private JourneyStep() { }

    public static JourneyStep Create(
        Guid tenantId,
        Guid journeyId,
        int stepOrder,
        JourneyStepType stepType,
        string bodyTemplate,
        string? subjectTemplate = null,
        int delaySeconds = 0)
    {
        return new JourneyStep
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JourneyId = journeyId,
            StepOrder = stepOrder,
            StepType = stepType,
            BodyTemplate = bodyTemplate,
            SubjectTemplate = subjectTemplate,
            DelaySeconds = delaySeconds,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
