using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.AgentWhisper;

public record LiveCallWhisperSession(
    string CallSessionId,
    string RepId,
    string RepName,
    string BuyerOrganization,
    decimal TargetDealAcv,
    string CurrentCallPhase, // "Discovery", "Demo", "PriceObjection", "ContractNegotiation"
    double SentimentScore, // -1.0 to 1.0
    string ActiveWhisperPrompt,
    string WhisperCategory // "PricingConcession", "CompetitiveBattlecard", "TimingUrgency", "ContractFlex"
);

public record ConcessionGuardrail(
    string ConcessionType, // "PaymentTermNet60", "MultiYearDiscount", "FreeOnboardingTier", "DedicatedCsm"
    decimal MaximumAllowedDiscountPct,
    decimal MinimumAcvThreshold,
    bool RequiresManagerApproval
);

public record AgentWhisperOverview(
    int ActiveCallsCount,
    int TotalWhispersDispatchedToday,
    double ConcessionAdherenceRatePct,
    double DealWinRateLiftPct,
    IReadOnlyList<LiveCallWhisperSession> ActiveSessions,
    IReadOnlyList<ConcessionGuardrail> Guardrails
);

public record AnalyzeSpeechTurnCommand(
    string CallSessionId,
    string Speaker, // "Buyer", "Rep"
    string TranscriptText,
    int AudioTurnDurationMs
);

public record AnalyzeSpeechTurnResult(
    string TurnAnalysisId,
    string CallSessionId,
    string DetectedIntent,
    string RecommendedWhisperText,
    decimal MaxConcessionAllowedPct,
    int LatencyDispatchedMs
);

public record CalculateConcessionCommand(
    string CallSessionId,
    decimal ProposedDiscountPct,
    int RequestedPaymentDays
);

public record CalculateConcessionResult(
    string ConcessionDecisionId,
    string Status, // "Approved_Autonomous", "CounterOffer_Recommended", "Escalated_To_VP"
    decimal ApprovedDiscountPct,
    int ApprovedPaymentDays,
    string RationaleNarrative
);
