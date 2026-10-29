namespace SGrowing.Core.Entities;

public class VoiceCallAmdRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CallSessionId { get; set; }
    public string DestinationPhone { get; set; } = string.Empty;
    public string DetectedClassification { get; set; } = "HumanPickup"; // "HumanPickup", "AnsweringMachineBeep", "InteractiveIvr", "Silence"
    public decimal AmdConfidence { get; set; } = 0.94m;
    public int DetectionDurationMs { get; set; } = 1450;
    public bool VoicemailDropped { get; set; } = false;
    public string AudioDropFileUrl { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;

    public static VoiceCallAmdRecord Create(
        Guid tenantId,
        string destinationPhone,
        string classification,
        decimal confidence,
        int durationMs,
        bool voicemailDropped = false,
        string dropAudioUrl = "",
        Guid? callSessionId = null)
    {
        return new VoiceCallAmdRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CallSessionId = callSessionId,
            DestinationPhone = destinationPhone.Trim(),
            DetectedClassification = classification.Trim(),
            AmdConfidence = Math.Clamp(confidence, 0.1m, 1.0m),
            DetectionDurationMs = durationMs,
            VoicemailDropped = voicemailDropped,
            AudioDropFileUrl = dropAudioUrl.Trim(),
            EvaluatedAtUtc = DateTime.UtcNow
        };
    }
}

public class VoicemailDropTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string AudioFileUrl { get; set; } = string.Empty;
    public string DynamicTextPrompt { get; set; } = string.Empty;
    public bool UseTtsSynthesis { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public static VoicemailDropTemplate Create(
        Guid tenantId,
        string templateName,
        string audioFileUrl,
        string textPrompt = "",
        bool useTts = false)
    {
        return new VoicemailDropTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateName = templateName.Trim(),
            AudioFileUrl = audioFileUrl.Trim(),
            DynamicTextPrompt = textPrompt.Trim(),
            UseTtsSynthesis = useTts,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
