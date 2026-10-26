namespace SGrowing.Core.Entities;

public enum VoiceDialerCampaignStatus
{
    Draft = 1,
    Running = 2,
    Paused = 3,
    Completed = 4
}

public enum IvrCallState
{
    Queued = 1,
    Ringing = 2,
    InCallSpeaking = 3,
    AwaitingIntent = 4,
    Confirmed = 5,
    Postponed = 6,
    Cancelled = 7,
    NoAnswer = 8,
    Failed = 9
}

public enum IvrSpokenIntent
{
    None = 0,
    Confirmed = 1,
    Postponed = 2,
    Cancelled = 3,
    Unrecognized = 4
}

public class VoiceConfirmationCampaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "ar-TN"; // ar-TN, fr-FR, en-US
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelayMinutes { get; set; } = 60;
    public VoiceDialerCampaignStatus Status { get; set; } = VoiceDialerCampaignStatus.Running;
    public int TotalCallsQueued { get; set; }
    public int TotalCallsSucceeded { get; set; }
    public int TotalCallsFailed { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static VoiceConfirmationCampaign Create(
        Guid tenantId,
        string name,
        string languageCode = "ar-TN",
        int maxRetries = 3,
        int retryDelayMinutes = 60)
    {
        return new VoiceConfirmationCampaign
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            LanguageCode = languageCode,
            MaxRetryAttempts = maxRetries,
            RetryDelayMinutes = retryDelayMinutes,
            Status = VoiceDialerCampaignStatus.Running,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}

public class OutboundCallAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid OrderId { get; set; }
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int AttemptNumber { get; set; } = 1;
    public IvrCallState State { get; set; } = IvrCallState.Queued;
    public IvrSpokenIntent SpokenIntent { get; set; } = IvrSpokenIntent.None;
    public string? SpeechTranscript { get; set; }
    public string? DtmfDigit { get; set; }
    public int CallDurationSeconds { get; set; }
    public DateTime? ScheduledRetryUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public static OutboundCallAttempt Create(
        Guid tenantId,
        Guid orderId,
        string customerPhone,
        string customerName,
        Guid? campaignId = null,
        int attemptNumber = 1)
    {
        return new OutboundCallAttempt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = orderId,
            CustomerPhone = customerPhone.Trim(),
            CustomerName = customerName.Trim(),
            CampaignId = campaignId,
            AttemptNumber = attemptNumber,
            State = IvrCallState.Queued,
            SpokenIntent = IvrSpokenIntent.None,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
