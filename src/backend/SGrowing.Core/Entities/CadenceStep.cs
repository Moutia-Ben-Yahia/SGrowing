namespace SGrowing.Core.Entities;

public enum OutboundChannel
{
    Email = 1,
    WhatsApp = 2,
    LinkedInInvite = 3
}

public class CadenceStep
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CampaignId { get; private set; }
    public int StepNumber { get; set; }
    public int DelayDays { get; set; }
    public OutboundChannel Channel { get; set; } = OutboundChannel.Email;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;

    private CadenceStep() { }

    public static CadenceStep Create(
        Guid tenantId,
        Guid campaignId,
        int stepNumber,
        int delayDays,
        OutboundChannel channel,
        string subjectTemplate,
        string bodyTemplate)
    {
        return new CadenceStep
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CampaignId = campaignId,
            StepNumber = stepNumber,
            DelayDays = delayDays,
            Channel = channel,
            SubjectTemplate = subjectTemplate,
            BodyTemplate = bodyTemplate
        };
    }
}
