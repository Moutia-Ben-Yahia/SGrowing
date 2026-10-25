namespace SGrowing.Core.Entities;

public enum TouchpointChannel
{
    Email = 1,
    LinkedInConnect = 2,
    LinkedInMessage = 3,
    WhatsApp = 4,
    VoiceCall = 5,
    Sms = 6
}

public class CadenceTouchpoint
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CampaignId { get; set; }
    public int StepNumber { get; set; }
    public TouchpointChannel Channel { get; set; }
    public int DelayDays { get; set; }
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public CadenceTouchpoint() { }

    public static CadenceTouchpoint Create(
        Guid tenantId,
        Guid campaignId,
        int stepNumber,
        TouchpointChannel channel,
        int delayDays,
        string? subjectTemplate,
        string bodyTemplate)
    {
        return new CadenceTouchpoint
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CampaignId = campaignId,
            StepNumber = stepNumber,
            Channel = channel,
            DelayDays = delayDays,
            SubjectTemplate = subjectTemplate,
            BodyTemplate = bodyTemplate,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class TouchpointExecution
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TouchpointId { get; set; }
    public Guid ContactId { get; set; }
    public TouchpointChannel Channel { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public string Status { get; set; } = "Pending";

    public TouchpointExecution() { }

    public static TouchpointExecution Create(
        Guid tenantId,
        Guid touchpointId,
        Guid contactId,
        TouchpointChannel channel,
        DateTime scheduledAtUtc,
        string status = "Pending")
    {
        return new TouchpointExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TouchpointId = touchpointId,
            ContactId = contactId,
            Channel = channel,
            ScheduledAtUtc = scheduledAtUtc,
            ExecutedAtUtc = null,
            Status = status
        };
    }

    public void MarkSent(DateTime? executedAtUtc = null)
    {
        Status = "Sent";
        ExecutedAtUtc = executedAtUtc ?? DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        Status = "Delivered";
    }

    public void MarkReplied()
    {
        Status = "Replied";
    }
}
