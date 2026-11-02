namespace SGrowing.Api.Modules.Voice.WebRtcSdr;

public enum VoiceSessionStatus
{
    Initiated,
    Ringing,
    Active,
    Paused,
    Completed,
    Failed
}

public enum VadState
{
    Silence,
    UserSpeaking,
    AgentSpeaking,
    BargeInInterrupted
}

public record TranscriptItem(
    string Speaker, // "Lead" or "AI_SDR"
    string Text,
    double Confidence,
    DateTime TimestampUtc
);

public record VoiceSdrSession(
    string SessionId,
    string TenantId,
    string LeadId,
    string PhoneNumber,
    string AgentPersona,
    VoiceSessionStatus Status,
    VadState CurrentVadState,
    int LatencyMs,
    int TurnCount,
    List<TranscriptItem> Transcript,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc
);

public record StartVoiceSessionRequest(
    string TenantId,
    string LeadId,
    string PhoneNumber,
    string AgentPersona
);

public record AudioStreamPacket(
    string SessionId,
    string Base64AudioData,
    string AudioFormat, // "pcm_16000" or "opus"
    bool IsLeadSpeaking
);

public record AudioStreamResponse(
    string SessionId,
    VadState NewVadState,
    string? AiReplyText,
    string? Base64SynthesizedAudio,
    int RoundtripLatencyMs
);

public record EndVoiceSessionRequest(
    string SessionId,
    string Disposition // "Qualified", "CallLater", "WrongNumber", "NotInterested"
);
