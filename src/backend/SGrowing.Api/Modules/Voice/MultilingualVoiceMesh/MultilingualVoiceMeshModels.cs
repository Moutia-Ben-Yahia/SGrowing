using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.MultilingualVoiceMesh;

public sealed record MultilingualVoiceOverview(
    int ActiveTranslationStreamsCount,
    int SupportedLanguagePairsCount,
    double AverageTranslationLatencyMs,
    double VocalTimbreSimilarityScore,
    IReadOnlyList<LiveTranslationSession> ActiveSessions,
    IReadOnlyList<VoiceCloneProfile> RegisteredVoiceClones
);

public sealed record LiveTranslationSession(
    string SessionId,
    string SourceLanguageCode, // en-US, es-ES, de-DE, ja-JP, zh-CN
    string TargetLanguageCode,
    string SpeakerName,
    double CurrentLatencyMs,
    int ChunksTranslated,
    string StreamStatus
);

public sealed record VoiceCloneProfile(
    string ProfileId,
    string SpeakerName,
    string ReferenceAudioHash,
    double PitchBaseHz,
    double TimbreResonanceScore,
    DateTime CreatedAt
);

public sealed record TranslateAudioChunkRequest(
    string SessionId,
    string AudioChunkBase64,
    string SourceLang,
    string TargetLang,
    string VoiceProfileId
);

public sealed record TranslatedAudioChunkResult(
    string SessionId,
    string TranslatedAudioChunkBase64,
    string DetectedSourceText,
    string TranslatedText,
    long TranslationLatencyMs,
    double VocalSimilarityPct
);

public sealed record RegisterVoiceCloneRequest(
    string SpeakerName,
    string ReferenceAudioSampleBase64
);

public sealed record VoiceCloneReceipt(
    string ProfileId,
    string SpeakerName,
    string Status,
    string TimbreEmbeddingDigest,
    DateTime Timestamp
);
