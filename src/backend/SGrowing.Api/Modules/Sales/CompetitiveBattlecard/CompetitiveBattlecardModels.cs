using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Sales.CompetitiveBattlecard;

public sealed record CompetitorProfileRecord(
    string CompetitorId,
    string CompetitorName,
    string MarketCategory,
    decimal EstimatedAverageDealSizeUsd,
    double WinRateAgainstPct,
    IReadOnlyList<string> KnownKillPoints,
    IReadOnlyList<string> PricingCounterStrategies,
    string ThreatLevel // HIGH, MEDIUM, LOW
);

public sealed record LiveBattlecardRecord(
    string BattlecardId,
    string CompetitorId,
    string OpportunityId,
    string GeneratedTimestamp,
    string ExecutiveSummary,
    IReadOnlyList<string> ObjectionHandlingPlaybook,
    string CryptographicProofDigest
);

public sealed record WinLossSignalRecord(
    string SignalId,
    string CompetitorId,
    string Outcome, // WON, LOST
    decimal DealValueUsd,
    string KeyDecidingFactor,
    DateTime LoggedAt
);

public sealed record BattlecardOverview(
    int TrackedCompetitorsCount,
    double AggregateWinRatePct,
    int ActiveBattlecardsCount,
    decimal TotalProtectedPipelineValueUsd,
    IReadOnlyList<CompetitorProfileRecord> Competitors,
    IReadOnlyList<WinLossSignalRecord> RecentSignals
);

public sealed record GenerateBattlecardRequest(
    string CompetitorId,
    string OpportunityId,
    string TargetVertical,
    decimal DealValueUsd
);

public sealed record SubmitWinLossSignalRequest(
    string CompetitorId,
    string Outcome,
    decimal DealValueUsd,
    string KeyDecidingFactor
);
