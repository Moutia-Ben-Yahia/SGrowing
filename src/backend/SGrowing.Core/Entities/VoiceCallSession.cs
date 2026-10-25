namespace SGrowing.Core.Entities;

public enum CallDirection
{
    Inbound = 1,
    Outbound = 2
}

public enum CallStatus
{
    Ringing = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4
}

public class VoiceCallSession
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public string CallSid { get; set; } = string.Empty;
    public CallDirection Direction { get; set; }
    public string FromNumber { get; set; } = string.Empty;
    public string ToNumber { get; set; } = string.Empty;
    public CallStatus Status { get; set; } = CallStatus.Ringing;
    public int DurationSeconds { get; set; }
    public string? RecordingUrl { get; set; }
    public string? Transcript { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public VoiceCallSession() { }

    public static VoiceCallSession Create(
        Guid tenantId,
        Guid contactId,
        string callSid,
        CallDirection direction,
        string fromNumber,
        string toNumber)
    {
        return new VoiceCallSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContactId = contactId,
            CallSid = callSid,
            Direction = direction,
            FromNumber = fromNumber,
            ToNumber = toNumber,
            Status = CallStatus.Ringing,
            DurationSeconds = 0,
            RecordingUrl = null,
            Transcript = null,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void CompleteCall(int durationSeconds, string? recordingUrl = null, string? transcript = null)
    {
        Status = CallStatus.Completed;
        DurationSeconds = durationSeconds;
        if (recordingUrl != null)
        {
            RecordingUrl = recordingUrl;
        }
        if (transcript != null)
        {
            Transcript = transcript;
        }
    }

    public void FailCall()
    {
        Status = CallStatus.Failed;
    }

    public void AppendTranscript(string text)
    {
        if (string.IsNullOrWhiteSpace(Transcript))
        {
            Transcript = text.Trim();
        }
        else
        {
            Transcript = $"{Transcript}\n{text.Trim()}";
        }
    }
}
