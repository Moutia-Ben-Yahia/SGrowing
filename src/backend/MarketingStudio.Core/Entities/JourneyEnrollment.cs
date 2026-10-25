namespace MarketingStudio.Core.Entities;

public enum JourneyEnrollmentStatus
{
    Active = 1,
    Completed = 2,
    Cancelled = 3
}

public class JourneyEnrollment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid JourneyId { get; private set; }
    public Guid ContactId { get; private set; }
    public int CurrentStepOrder { get; set; } = 1;
    public JourneyEnrollmentStatus Status { get; set; } = JourneyEnrollmentStatus.Active;
    public DateTime? NextExecutionAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; private set; }

    private JourneyEnrollment() { }

    public static JourneyEnrollment Create(Guid tenantId, Guid journeyId, Guid contactId)
    {
        return new JourneyEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JourneyId = journeyId,
            ContactId = contactId,
            CurrentStepOrder = 1,
            Status = JourneyEnrollmentStatus.Active,
            NextExecutionAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void AdvanceToStep(int nextStepOrder, int delaySeconds)
    {
        CurrentStepOrder = nextStepOrder;
        NextExecutionAtUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
    }

    public void MarkCompleted()
    {
        Status = JourneyEnrollmentStatus.Completed;
        NextExecutionAtUtc = null;
    }
}
