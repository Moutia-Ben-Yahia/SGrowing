namespace SGrowing.Api.Modules.Journeys.OmnichannelSequence;

public enum ChannelType
{
    WhatsApp,
    Email,
    Sms,
    VoiceSdr
}

public record CadenceStepConfig(
    int StepOrder,
    ChannelType Channel,
    TimeSpan DelayBeforeStep,
    TimeSpan FallbackTimeout,
    string TemplateOrScriptId
);

public record CadenceRecipient(
    string RecipientId,
    string Name,
    string Email,
    string PhoneNumber,
    string TimeZoneId,
    int LeadScore,
    int WeeklyTouchpointsCount
);

public record OmnichannelCadenceDefinition(
    string CadenceId,
    string Name,
    List<CadenceStepConfig> Steps,
    int MaxWeeklyTouchpoints = 4
);

public record ExecuteCadenceStepRequest(
    string TenantId,
    string CadenceId,
    string RecipientId,
    int StepOrder,
    bool PriorStepEngaged,
    int ElapsedHoursSincePriorStep = 0
);

public record ExecuteCadenceStepResult(
    bool ActionDispatched,
    ChannelType ChannelUsed,
    string RecipientId,
    bool EscalatedToFallback,
    bool SuppressedDueToFatigue,
    string StatusMessage,
    DateTime DispatchedAtUtc
);

public record CadenceDispatchHistory(
    string TenantId,
    string CadenceId,
    string RecipientId,
    int StepOrder,
    ChannelType ChannelUsed,
    bool EscalatedToFallback,
    DateTime DispatchedAtUtc,
    string StatusMessage
);
