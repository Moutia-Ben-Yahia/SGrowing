namespace SGrowing.Api.Modules.Sales.DealCoaching;

public enum ObjectionCategory
{
    PricingAndBudget,
    SecurityAndCompliance,
    CompetitorDisplacement,
    AuthorityAndTiming,
    IntegrationComplexity
}

public sealed record LiveCoachingSession(
    string SessionId,
    string OpportunityId,
    string AccountName,
    string SalesRepName,
    double CurrentAdherenceScore,
    int ObjectionsHandledCount,
    DateTime StartedAtUtc,
    string Status
);

public sealed record ObjectionDetectedEvent(
    string EventId,
    string SessionId,
    string TranscribedSnippet,
    ObjectionCategory Category,
    double ConfidenceScore,
    string RecommendedCounterTactic,
    string EvidenceProofPoint,
    DateTime DetectedAtUtc
);

public sealed record CounterTacticSuggestion(
    string SuggestionId,
    ObjectionCategory Category,
    string ObjectionPhraseTrigger,
    string PivotTalkTrack,
    string ValueProofMetric,
    double HistoricalWinRateLift
);

public sealed record RepCoachingScorecard(
    string ScorecardId,
    string SalesRepName,
    double OverallObjectionHandleRate,
    double TalkToListenRatio,
    int SessionsCoachedTotal,
    double AverageDealSizeLiftPercentage
);

public sealed record DealCoachingOverview(
    int ActiveSessionsCount,
    int TotalObjectionsDetectedQuarter,
    double AverageRepAdherenceScore,
    double WinRateAccelerationPct,
    IReadOnlyList<LiveCoachingSession> ActiveSessions,
    IReadOnlyList<ObjectionDetectedEvent> RecentObjections
);
