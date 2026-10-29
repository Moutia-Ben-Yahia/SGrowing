using SGrowing.Core.Entities;

namespace SGrowing.Api.Modules.Voice;

public record AmdClassificationResultDto(
    Guid RecordId,
    string DestinationPhone,
    string Classification,
    decimal Confidence,
    int DurationMs,
    bool VoicemailDropped,
    string? AudioDropFileUrl,
    string ActionTaken
);

public interface IVoiceCallAmdService
{
    Task<AmdClassificationResultDto> ClassifyAudioStreamAmdAsync(
        Guid tenantId,
        string destinationPhone,
        int audioDurationMs = 2100,
        string? forcedClassification = null,
        Guid? callSessionId = null);

    Task<VoicemailDropTemplate> ConfigureVoicemailTemplateAsync(
        Guid tenantId,
        string templateName,
        string audioUrl,
        string textPrompt = "",
        bool useTts = false);

    Task<List<VoiceCallAmdRecord>> GetAmdRecordsAsync(
        Guid tenantId,
        int limit = 50);
}
