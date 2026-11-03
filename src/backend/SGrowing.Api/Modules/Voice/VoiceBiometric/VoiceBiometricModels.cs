namespace SGrowing.Api.Modules.Voice.VoiceBiometric;

public record VoiceBiometricProfile(
    string ProfileId,
    string CallerIdentifier,
    string ExecutiveName,
    double BaselinePitchJitterPct,
    double SpectralCentroidMeanHz,
    string CryptographicVoiceprintHash,
    DateTime EnrolledAt,
    string Status // ACTIVE_VERIFIED, STEP_UP_REQUIRED, QUARANTINED
);

public record CallSecurityAuditRecord(
    string AuditId,
    string CallSessionId,
    string CallerIdentifier,
    double BiometricMatchScore, // 0.0 to 1.0 (>=0.85 verified)
    double SyntheticDeepfakeProbability, // 0.0 to 1.0 (>=0.80 deepfake alert)
    string VocoderArtifactSignature,
    string DefenseActionTaken, // VERIFIED_ALLOWED, CHALLENGE_STEP_UP, TERMINATED_QUARANTINED
    DateTime AuditedAt
);

public record VoiceBiometricOverview(
    int TotalEnrolledProfilesCount,
    int ActiveScannedCallsCount,
    int DeepfakeAttacksBlockedCount,
    double DeepfakeDefenseAccuracyPct, // e.g. 99.4%
    List<VoiceBiometricProfile> EnrolledProfiles,
    List<CallSecurityAuditRecord> RecentSecurityAudits
);

public record VerifyAudioFrameRequest(
    string CallSessionId,
    string CallerIdentifier,
    double PitchJitterSamplePct,
    double SpectralCentroidSampleHz,
    bool ContainsPhaseDiscontinuity
);

public record QuarantineCallRequest(
    string CallSessionId,
    string Reason
);
