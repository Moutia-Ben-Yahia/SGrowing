namespace SGrowing.Core.Entities;

public enum AudioEncoding
{
    Mulaw8kHz = 1,
    Pcm16kHz = 2,
    Opus = 3
}

public enum VadStatus
{
    Silence = 1,
    SpeechDetected = 2,
    BargeInInterrupted = 3
}

public class StreamingAudioFrame
{
    public long SequenceNumber { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string PayloadBase64 { get; set; } = string.Empty;
    public int ByteCount { get; set; }
    public double RmsEnergy { get; set; }
    public bool IsSpeech { get; set; }
}

public class VadEvent
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public bool IsSpeechStart { get; set; }
    public double EnergyLevel { get; set; }
    public double Confidence { get; set; }
}

public class StreamingVoiceCallSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string CallSid { get; set; } = string.Empty;
    public string StreamSid { get; set; } = string.Empty;
    public AudioEncoding Encoding { get; set; } = AudioEncoding.Mulaw8kHz;
    public int SampleRate { get; set; } = 8000;
    public VadStatus CurrentVadStatus { get; set; } = VadStatus.Silence;
    public bool IsAgentSpeaking { get; set; }
    public int InterruptionCount { get; set; }
    public long TotalFramesReceived { get; set; }
    public double AverageProcessingLatencyMs { get; set; }
    public bool IsTransferred { get; set; }
    public string? TransferTargetSipUri { get; set; }
    public string? TransferReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }

    public static StreamingVoiceCallSession Create(Guid tenantId, string callSid, string streamSid)
    {
        return new StreamingVoiceCallSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CallSid = callSid,
            StreamSid = streamSid,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void RecordInboundFrame(int byteCount, double rmsEnergy, bool isSpeech, double latencyMs)
    {
        TotalFramesReceived++;
        AverageProcessingLatencyMs = (AverageProcessingLatencyMs * (TotalFramesReceived - 1) + latencyMs) / TotalFramesReceived;

        if (isSpeech && IsAgentSpeaking)
        {
            CurrentVadStatus = VadStatus.BargeInInterrupted;
            InterruptionCount++;
            IsAgentSpeaking = false; // Agent is interrupted
        }
        else if (isSpeech)
        {
            CurrentVadStatus = VadStatus.SpeechDetected;
        }
        else
        {
            CurrentVadStatus = VadStatus.Silence;
        }
    }

    public void SetAgentSpeaking(bool speaking)
    {
        IsAgentSpeaking = speaking;
        if (speaking)
        {
            CurrentVadStatus = VadStatus.Silence;
        }
    }

    public void TriggerWarmTransfer(string sipUri, string reason)
    {
        IsTransferred = true;
        TransferTargetSipUri = sipUri;
        TransferReason = reason;
        IsAgentSpeaking = false;
        EndedAtUtc = DateTime.UtcNow;
    }
}
