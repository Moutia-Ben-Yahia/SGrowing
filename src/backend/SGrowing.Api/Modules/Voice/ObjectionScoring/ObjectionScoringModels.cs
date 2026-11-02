using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.ObjectionScoring;

public enum ObjectionCategory
{
    BudgetAndPricing,
    CompetitorEntrenchment,
    TimingAndPriority,
    ProcurementAndSecurity,
    AuthorityAndDecisionMaker,
    GeneralSkepticism
}

public record ObjectionRebuttalRequest(
    string CustomerUtterance,
    string? ContextTranscript = null,
    string Persona = "ConsultativeEnterpriseSDR"
);

public record ObjectionRebuttalResult(
    string ObjectionId,
    ObjectionCategory DetectedCategory,
    double ConfidenceScore,
    string RecommendedRebuttal,
    string PsychologicalFraming, // e.g. "Feel-Felt-Found", "Cost of Inaction Pivot", "Deflect to Proof"
    List<string> SuggestedFollowUpQuestions,
    double TurnLatencyMs,
    DateTime TimestampUtc
);

public record CallTurn(
    string Speaker, // "Agent" or "Lead"
    string Text,
    double DurationSeconds
);

public record ScoreCallRequest(
    string CallId,
    string LeadName,
    string CompanyName,
    List<CallTurn> TranscriptTurns
);

public record CallScoringResult(
    string CallId,
    double OverallScorePercent, // 0 - 100
    int DiscoveryDepthScore,    // 0 - 25
    int ObjectionHandlingScore, // 0 - 25
    int RapportScore,           // 0 - 25
    int ClosingEffectivenessScore, // 0 - 25
    string OutcomeQualification, // "SalesQualifiedLead", "NurtureRequired", "Disqualified"
    List<string> StrengthPoints,
    List<string> ImprovementCoachingNotes,
    DateTime ScoredAtUtc
);
