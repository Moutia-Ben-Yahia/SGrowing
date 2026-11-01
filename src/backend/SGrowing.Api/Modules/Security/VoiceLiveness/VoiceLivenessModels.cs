namespace SGrowing.Api.Modules.Security.VoiceLiveness;

/// <summary>
/// Verdict classification for real-time voice acoustic liveness.
/// </summary>
public enum LivenessVerdict
{
    AuthenticLiveHuman = 1,
    SuspiciousReplay = 2,
    DefiniteDeepfakeSynthetic = 3,
    InconclusiveLowAudioQuality = 4
}

/// <summary>
/// Specific synthetic voice generation vector detected.
/// </summary>
public enum SyntheticAttackVector
{
    NeuralTtsVoiceClone = 1,
    VoiceConversionFilter = 2,
    AcousticReplayAttack = 3,
    DiffusionMelSpectrogramArtifact = 4,
    SplicedAudioDeepfake = 5
}

/// <summary>
/// Extracted biometric and spectral acoustic parameters from audio frames.
/// </summary>
public sealed record AudioSpectralFeatures(
    int SampleRateHz,
    double DurationSeconds,
    double SpectralFluxMean,
    double HarmonicToNoiseRatioDb,
    double HighFrequencyRollOffHz,
    int PhaseDiscontinuityCount,
    double SyntheticVocoderArtifactScore // 0.0 (natural) to 1.0 (artificial)
);

/// <summary>
/// Dynamic randomized phonetic challenge phrase issued to verify caller liveness.
/// </summary>
public sealed record BiometricChallenge(
    string ChallengeId,
    string CallSessionId,
    string RequiredPhoneticPhrase,
    string RandomNumericSalt,
    int MinimumResponseLatencyMs,
    int MaximumResponseLatencyMs,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    bool IsCompleted = false,
    bool Passed = false
);

/// <summary>
/// Holistic biometric evaluation output for a call audio sample.
/// </summary>
public sealed record VoiceLivenessEvaluationResult(
    string EvaluationId,
    string CallSessionId,
    LivenessVerdict Verdict,
    double SyntheticRiskScore, // 0.0 (authentic) to 1.0 (synthetic clone)
    AudioSpectralFeatures SpectralFeatures,
    bool PassedBiometricChallenge,
    IReadOnlyList<string> TriggeredRedFlags,
    DateTime EvaluatedAtUtc
);

/// <summary>
/// Request to issue a dynamic biometric phonetic challenge for a call session.
/// </summary>
public sealed record IssueChallengeRequest(
    string CallSessionId,
    int ValiditySeconds = 30
);

/// <summary>
/// Request to analyze an audio sample for synthetic deepfake biomarkers.
/// </summary>
public sealed record VerifyVoiceLivenessRequest(
    string CallSessionId,
    float[] AudioPcmSamples,
    int SampleRateHz = 16000,
    string? ActiveChallengeId = null,
    int CallerResponseLatencyMs = 800
);
