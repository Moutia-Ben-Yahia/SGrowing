namespace SGrowing.Api.Modules.Voice.TelephonyDisfluency;

public record DisfluencyAcousticEvent(
    string EventId,
    string CallSessionId,
    string DisfluencyType, // HESITATION_PAUSE, FILLER_SOUND_UM_UH, PROLONGATION, FALSE_START_REPAIR
    int DurationMs,
    double ConfidenceScore,
    string RecommendedPacingAdjustment, // SLOW_DOWN_SPEECH_15PCT, INSERT_EMPATHY_BUFFER, MAINTAIN_CADENCE
    DateTime DetectedAt
);

public record TelephonyPacingProfile(
    string ProfileId,
    string CallerSegment, // HIGH_STRESS_DISPUTE, CASUAL_RENEWAL, SENIOR_ASSIST, TECHNICAL_INTEGRATION
    double BaselineSpeakingRateWpm, // words per minute (e.g. 145 wpm)
    int DynamicPauseBudgetMs,        // e.g. 600ms
    double AdaptationResponsiveness
);

public record TelephonyDisfluencyOverview(
    int TotalAnalyzedSessionsCount,
    double AverageCallerSilenceDurationMs,
    int DisfluenciesDetectedLast24Hours,
    double TurnTakingSmoothnessIndex, // 0.0 to 100.0 (e.g. 94.6)
    List<DisfluencyAcousticEvent> RecentEvents,
    List<TelephonyPacingProfile> PacingProfiles
);

public record AnalyzeCadenceRequest(
    string CallSessionId,
    string DisfluencyType,
    int DurationMs,
    double ConfidenceScore,
    string CallerSegment
);

public record TunePacingRequest(
    string ProfileId,
    double TargetSpeakingRateWpm,
    int DynamicPauseBudgetMs
);
