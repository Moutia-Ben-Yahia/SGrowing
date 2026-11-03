using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.AgentAssist;

public record TranscriptChunk(
    string ChunkId,
    string Speaker, // "Customer" or "Representative"
    string Text,
    double TimestampSeconds,
    string Sentiment // "Positive", "Neutral", "Skeptical", "Frustrated"
);

public record CompetitorBattlecard(
    string CompetitorName,
    string TriggerKeyword,
    List<string> DifferentiatorBullets,
    string ProofMetric
);

public record WhisperSuggestion(
    string SuggestionId,
    string Category, // "ObjectionCounter", "PacingAlert", "ComplianceWarning", "DiscoveryQuestion"
    string CoachingPrompt,
    string Priority // "High", "Medium", "Info"
);

public record LiveAgentAssistSession(
    string CallSessionId,
    string RepName,
    string CustomerCompany,
    double RepTalkTimeSeconds,
    double CustomerTalkTimeSeconds,
    double RepTalkPercentage,
    bool TalkPacingAlertActive,
    List<TranscriptChunk> RecentTranscripts,
    List<CompetitorBattlecard> ActiveBattlecards,
    List<WhisperSuggestion> LiveSuggestions
);

public record StreamChunkRequest(
    string CallSessionId,
    string Speaker,
    string Text,
    double DurationSeconds
);
