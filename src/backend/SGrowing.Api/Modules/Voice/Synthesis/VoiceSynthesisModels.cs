using System;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Voice.Synthesis;

/// <summary>
/// Supported regional and cultural dialect accents for speech synthesis in the Maghreb.
/// </summary>
public enum DialectAccent
{
    AlgerianAlgiers = 0,
    AlgerianOran = 1,
    AlgerianConstantine = 2,
    MaghrebBilingualFrench = 3,
    Tunisian = 4,
    Moroccan = 5
}

/// <summary>
/// Voice persona gender and acoustic timbre profiles.
/// </summary>
public enum VoiceGender
{
    FemaleWarm = 0,
    MaleCalm = 1,
    FemaleProfessional = 2,
    MaleEnergetic = 3
}

/// <summary>
/// Supported acoustic transmission and telephony codecs.
/// </summary>
public enum AcousticCodec
{
    Pcm16_16kHz = 0,
    Pcm16_8kHz = 1,
    G711u_MuLaw = 2,
    Opus_16kbps = 3
}

/// <summary>
/// Request contract for synthesizing speech with neural dialect modeling and canary watermarking.
/// </summary>
public record VoiceSynthesisRequest(
    string Text,
    DialectAccent DialectAccent = DialectAccent.AlgerianAlgiers,
    VoiceGender VoiceGender = VoiceGender.FemaleWarm,
    AcousticCodec TargetCodec = AcousticCodec.Pcm16_16kHz,
    double DesiredSpeakingRate = 1.0,
    bool EmbedWatermark = true,
    string TenantId = "",
    string SessionId = ""
);

/// <summary>
/// Result of neural speech synthesis containing raw audio buffer, metadata, and watermark signature.
/// </summary>
public record SynthesizedAudioResult(
    byte[] AudioBuffer,
    double DurationMs,
    int SampleRateHz,
    AcousticCodec Codec,
    string WatermarkSignatureHex,
    int PhonemeCount,
    DialectAccent Dialect,
    DateTime CreatedAtUtc
);

/// <summary>
/// Acoustic payload embedded into synthesized audio via psychoacoustic spread-spectrum carrier.
/// </summary>
public record AcousticWatermarkPayload(
    string TenantId,
    string CallSessionId,
    DateTime TimestampUtc,
    string Nonce,
    string HmacSignatureHex
);

/// <summary>
/// Result of extracting and verifying an acoustic canary watermark from an audio buffer.
/// </summary>
public record WatermarkVerificationResult(
    bool IsAuthentic,
    string TenantId,
    string CallSessionId,
    DateTime ExtractedTimestampUtc,
    double ConfidenceScore,
    string VerificationMessage
);

/// <summary>
/// Acoustic indicators extracted from caller voice stream for sentiment and agitation scoring.
/// </summary>
public record AcousticSentimentInput(
    double AudioRmsLevelDb,
    double FundamentalFrequencyHz,
    double SpeechCadenceSyllablesPerSecond,
    double HesitationPauseRatio
);

/// <summary>
/// Prosody modulation parameters calculated to achieve vocal de-escalation and emotional empathy.
/// </summary>
public record ProsodyModulationParams(
    double PitchAdjustmentSemitones,
    double SpeakingRateMultiplier,
    double WarmthGainDb,
    double EmpathyPauseExpansionFactor,
    string CalmnessTier
);

/// <summary>
/// Comprehensive sentiment analysis result incorporating acoustic metrics and recommended prosody.
/// </summary>
public record AcousticSentimentResult(
    string SentimentTier,
    bool IsAgitated,
    double AgitationScore,
    double HesitationScore,
    ProsodyModulationParams RecommendedProsody,
    string AnalysisSummary
);

/// <summary>
/// Detailed dialect accent profile describing prosodic characteristics and cultural region.
/// </summary>
public record DialectAccentProfile(
    DialectAccent Dialect,
    string DisplayName,
    string Region,
    double BasePitchHz,
    double CadenceMultiplier,
    string DistinctiveTraits
);

/// <summary>
/// Request payload for verifying an audio watermark from binary or Base64 audio.
/// </summary>
public record VerifyWatermarkRequest(
    string? AudioBase64 = null,
    byte[]? AudioBytes = null,
    int SampleRateHz = 16000,
    string? SecretKey = null
);

/// <summary>
/// Request payload for analyzing caller acoustic sentiment from raw acoustic metrics.
/// </summary>
public record AnalyzeSentimentRequest(
    AcousticSentimentInput Input,
    string? CallerId = null,
    string? SessionId = null
);

/// <summary>
/// API response contract for speech synthesis endpoints returning Base64-encoded audio.
/// </summary>
public record SynthesizeAudioResponse(
    string AudioBase64,
    double DurationMs,
    int SampleRateHz,
    string Codec,
    string WatermarkSignatureHex,
    int PhonemeCount,
    string Dialect,
    DateTime CreatedAtUtc
);
