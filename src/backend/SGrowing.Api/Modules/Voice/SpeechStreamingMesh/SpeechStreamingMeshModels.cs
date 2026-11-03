using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.SpeechStreamingMesh;

public sealed record SpeechStreamingOverview(
    int ActiveFullDuplexStreamsCount,
    double P99AudioLatencyMs,
    double BargeInCancellationLatencyMs,
    double NaturalTurnTakingConfidencePct,
    IReadOnlyList<ActiveS2sSession> ActiveSessions,
    IReadOnlyList<BargeInEventRecord> RecentBargeIns
);

public sealed record ActiveS2sSession(
    string SessionId,
    string ClientAudioCodec, // Opus 48kHz, PCM16, G.711u
    string VoicePersona, // Executive British, Conversational Silicon Valley
    double CurrentRoundTripLatencyMs,
    int FramesProcessed,
    string StreamStatus
);

public sealed record BargeInEventRecord(
    string EventId,
    string SessionId,
    double UserSpeechOnsetLatencyMs,
    string CutoffTtsPhrase,
    DateTime Timestamp
);

public sealed record ProcessAudioChunkRequest(
    string SessionId,
    string AudioChunkBase64,
    int ChunkSequenceNumber,
    bool IsFinalOfTurn
);

public sealed record ProcessAudioChunkResult(
    string SessionId,
    int IngestedSequenceNumber,
    string SynthesizedAudioChunkBase64,
    double InferredPitchHz,
    string EmotionTone,
    long ChunkLatencyMs
);

public sealed record TriggerBargeInRequest(
    string SessionId,
    double OnsetVolumeDecibels,
    string CurrentPlaybackTimestamp
);

public sealed record BargeInReceipt(
    string ReceiptId,
    string SessionId,
    string Status,
    double CutoffLatencyMs,
    DateTime Timestamp
);
