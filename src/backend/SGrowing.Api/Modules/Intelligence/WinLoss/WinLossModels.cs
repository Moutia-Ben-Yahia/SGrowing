using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Intelligence.WinLoss;

public enum DealOutcome
{
    WonClosed,
    LostToCompetitor,
    LostToNoDecision,
    LostToComplianceBlocker
}

public record RootCauseFactor(
    string Category, // "Pricing", "IntegrationGap", "SecurityHurdle", "ExecutiveChampion"
    string Detail,
    double ImpactWeight // 0.0 to 1.0
);

public record CompetitorTacticInsight(
    string CompetitorName,
    string ObservedTactic,
    string CounterRecommendation
);

public record DealPostMortem(
    string DealId,
    string CompanyName,
    decimal DealValue,
    DealOutcome Outcome,
    List<RootCauseFactor> RootCauses,
    List<CompetitorTacticInsight> CompetitorInsights,
    List<string> FeatureGapsTagged,
    DateTime AnalyzedAtUtc
);

public record AnalyzeDealRequest(
    string DealId,
    string CompanyName,
    decimal DealValue,
    DealOutcome Outcome,
    string RepNotes,
    string? CompetitorName
);
