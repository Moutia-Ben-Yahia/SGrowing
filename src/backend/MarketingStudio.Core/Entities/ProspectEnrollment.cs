namespace MarketingStudio.Core.Entities;

public enum EnrollmentStatus
{
    Pending = 1,
    Active = 2,
    Replied = 3,
    Bounced = 4,
    Unsubscribed = 5,
    Completed = 6
}

public class ProspectEnrollment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid ContactId { get; private set; }
    public int CurrentStepNumber { get; set; } = 1;
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public string? PersonalizedHook { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
    public DateTime? LastDispatchedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; private set; }

    private ProspectEnrollment() { }

    public static ProspectEnrollment Create(Guid tenantId, Guid campaignId, Guid contactId, string? personalizedHook = null)
    {
        return new ProspectEnrollment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CampaignId = campaignId,
            ContactId = contactId,
            CurrentStepNumber = 1,
            Status = EnrollmentStatus.Active,
            PersonalizedHook = personalizedHook,
            NextRunAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void AdvanceStep(int nextStepNumber, int delayDays)
    {
        CurrentStepNumber = nextStepNumber;
        NextRunAtUtc = DateTime.UtcNow.AddDays(delayDays);
        LastDispatchedAtUtc = DateTime.UtcNow;
    }

    public void MarkReplied()
    {
        Status = EnrollmentStatus.Replied;
        NextRunAtUtc = null;
    }

    public void MarkUnsubscribed()
    {
        Status = EnrollmentStatus.Unsubscribed;
        NextRunAtUtc = null;
    }

    public void Complete()
    {
        Status = EnrollmentStatus.Completed;
        NextRunAtUtc = null;
    }
}
