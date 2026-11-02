namespace SGrowing.Api.Modules.Voice.CallCopilot;

public enum SpeakerRole
{
    Agent,
    Prospect,
    System
}

public record DiarizedUtterance(
    string UtteranceId,
    SpeakerRole Speaker,
    string Text,
    double Confidence,
    double SentimentScore,
    double DurationSeconds,
    DateTime TimestampUtc
);

public record LiveObjectionBattlecard(
    string CardId,
    string TriggerKeyword,
    string ObjectionCategory,
    string RecommendedResponse,
    string ProofPointMetric
);

public record CallCopilotSession(
    string SessionId,
    string AgentId,
    string ProspectPhone,
    string ProspectCompany,
    string Status,
    List<DiarizedUtterance> Utterances,
    List<LiveObjectionBattlecard> SurfacedBattlecards,
    double AverageSentiment,
    string? ExecutiveSummary,
    DateTime StartedAtUtc
);

public record IngestUtteranceRequest(
    string SessionId,
    SpeakerRole Speaker,
    string Text,
    double DurationSeconds = 2.5
);

public record CallSummaryResult(
    string SessionId,
    string ExecutiveSummary,
    List<string> ActionItems,
    string OverallDisposition,
    double FinalSentiment
);
