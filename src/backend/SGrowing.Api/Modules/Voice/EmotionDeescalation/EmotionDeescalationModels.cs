using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.EmotionDeescalation;

public sealed record EmotionDeescalationOverview(
    int ActiveCallsMonitoredCount,
    double DeescalationSuccessRatePct,
    double AverageAcousticStressScore,
    int PendingSupervisorEscalationsCount,
    IReadOnlyList<LiveCallAcousticTelemetry> ActiveTelemetry,
    IReadOnlyList<SupervisorEscalationTicket> RecentEscalations
);

public sealed record LiveCallAcousticTelemetry(
    string CallId,
    string AgentId,
    string CustomerPhone,
    double PitchVarianceHz,
    double SpeechVelocityWordsPerMin,
    double FrustrationScore,
    string DetectedEmotion,
    string RecommendedResponseStrategy
);

public sealed record SupervisorEscalationTicket(
    string EscalationId,
    string CallId,
    string CustomerPhone,
    double PeakFrustrationScore,
    string TriggerReason,
    string SuggestedConcessionOffer,
    string Status,
    DateTime CreatedAt
);

public sealed record AnalyzeAcousticsRequest(
    string CallId,
    string AgentId,
    string AudioChunkBase64,
    double PitchVarianceHz,
    double SpeechVelocityWordsPerMin,
    string LastSpokenPhrase
);

public sealed record AcousticAnalysisResult(
    string AnalysisId,
    string CallId,
    double SentimentScore,
    double FrustrationLevel,
    string DetectedEmotion,
    bool RequiresUrgentDeescalation,
    string SuggestedCalmingResponseScript,
    long LatencyMs
);

public sealed record SupervisorHandoffRequest(
    string CallId,
    string SupervisorId,
    string EscalationReason,
    string SuggestedConcessionOffer
);

public sealed record SupervisorHandoffReceipt(
    string TransferId,
    string CallId,
    string SupervisorId,
    string Status,
    string ContextBriefcaseId,
    DateTime Timestamp
);
