namespace SGrowing.Api.Modules.Channels.WhatsAppBroadcast;

public enum WhatsAppTemplateStatus
{
    Pending,
    Approved,
    Rejected,
    Paused
}

public enum BroadcastCampaignStatus
{
    Draft,
    Scheduled,
    Broadcasting,
    Completed,
    Paused
}

public enum MessageDeliveryState
{
    Queued,
    Sent,
    Delivered,
    Read,
    Failed
}

public record WhatsAppTemplate(
    string TemplateId,
    string Name,
    string Category, // "MARKETING", "UTILITY", "AUTHENTICATION"
    string Language, // e.g. "en_US"
    string HeaderText,
    string BodyText,
    List<string> Buttons,
    WhatsAppTemplateStatus Status
);

public record BroadcastRecipient(
    string RecipientId,
    string PhoneNumber,
    Dictionary<string, string> Variables,
    MessageDeliveryState State,
    DateTime UpdatedAtUtc
);

public record BroadcastCampaign(
    string CampaignId,
    string TenantId,
    string Name,
    string TemplateId,
    int TotalRecipients,
    int SentCount,
    int DeliveredCount,
    int ReadCount,
    int FailedCount,
    BroadcastCampaignStatus Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc
);

public record CreateCampaignRequest(
    string TenantId,
    string Name,
    string TemplateId,
    List<string> PhoneNumbers
);

public record SyncTemplateRequest(
    string Name,
    string Category,
    string Language,
    string HeaderText,
    string BodyText,
    List<string> Buttons
);
