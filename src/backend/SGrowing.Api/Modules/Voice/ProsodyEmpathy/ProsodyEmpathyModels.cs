namespace SGrowing.Api.Modules.Voice.ProsodyEmpathy;

public enum CallerEmotionState
{
    NeutralBalanced,
    FrustratedAngry,
    AnxiousDistressed,
    UrgentCritical,
    SatisfiedDelighted
}

public sealed record ProsodyModulationProfile(
    string ProfileId,
    string VoiceActorId,
    double PitchF0ModulationSemitones,
    double SpeechRateMultiplier, // e.g. 0.88x for calming deceleration
    double WarmthHarmonicBoostDb,
    CallerEmotionState TargetEmotionResponse
);

public sealed record ToneShifterEvent(
    string EventId,
    string CallSessionId,
    CallerEmotionState DetectedCallerEmotion,
    double EmotionConfidenceScore,
    double AppliedPitchShiftSemitones,
    double AppliedTempoAdjustmentRatio,
    string AppliedTimbreFilterName,
    DateTime ShiftedAtUtc
);

public sealed record ProsodyEmpathyOverview(
    int ActiveCallsWithToneShiftingCount,
    double CustomerDeEscalationSuccessRatePct,
    double AverageResponseLatencyMs,
    IReadOnlyList<ToneShifterEvent> RecentToneEvents,
    IReadOnlyList<ProsodyModulationProfile> ActiveProfiles
);
