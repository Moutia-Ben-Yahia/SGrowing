namespace SGrowing.Api.Modules.Voice.SentimentDeescalation;

public enum CallerEmotionalState
{
    Delighted,
    NeutralCalm,
    MildFrustration,
    SevereAngerEscalation
}

public enum DeescalationInterventionType
{
    MaintainStandardPacing,
    SlowDownTempoAndSoftenTone,
    OfferGoodwillVoucherCredit,
    ImmediateSupervisorTakeover
}

public sealed record VoiceCallAcousticFrame(
    string CallId,
    DateTime TimestampUtc,
    double PitchF0Hz,
    double BaselineF0Hz,
    double SpeechRateWordsPerMinute,
    double VolumeDb,
    double VocalJitterPercent,
    int NegativeSentimentKeywordCount
);

public sealed record EmotionalStateEvaluation(
    string CallId,
    DateTime EvaluatedAtUtc,
    CallerEmotionalState State,
    double AgitationIndex, // 0.0 to 10.0
    DeescalationInterventionType RecommendedIntervention,
    double SpeechTempoAdjustmentRatio, // e.g. 0.85 = slow down by 15%
    double PitchModulationSemitones,   // e.g. -2.0 semitones for calmer lower register
    decimal? SuggestedGoodwillCreditEur,
    string Rationale
);

public sealed record DeescalationDispatchEvent(
    string EventId,
    string CallId,
    DeescalationInterventionType Intervention,
    string ActionSummary,
    bool TransferredToHumanSupervisor,
    DateTime DispatchedAtUtc
);
