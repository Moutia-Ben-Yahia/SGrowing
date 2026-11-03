using System;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.AgentWhisper;

public interface IAgentWhisperEngine
{
    AgentWhisperOverview GetOverview();
    AnalyzeSpeechTurnResult AnalyzeSpeechTurn(AnalyzeSpeechTurnCommand command);
    CalculateConcessionResult CalculateConcession(CalculateConcessionCommand command);
}

public class AgentWhisperEngine : IAgentWhisperEngine
{
    private readonly List<LiveCallWhisperSession> _sessions =
    [
        new("CALL-8841", "REP-101", "Sarah Jenkins", "Apex Financial Corp", 185000m, "PriceObjection", 0.35, "Offer Net 60 instead of 15% discount; protects 88% gross margin", "PricingConcession"),
        new("CALL-8842", "REP-102", "Michael Chang", "BioHealth Dynamics", 95000m, "ContractNegotiation", 0.65, "Leverage SOC2 Type II cert & enterprise SLA guarantees against competitor", "CompetitiveBattlecard"),
        new("CALL-8843", "REP-103", "Elena Rostova", "Nordic Retail Holdings", 240000m, "Discovery", 0.50, "Ask about upcoming Q4 inventory peak to anchor early rollout urgency", "TimingUrgency")
    ];

    private readonly List<ConcessionGuardrail> _guardrails =
    [
        new("MultiYearDiscount", 18.0m, 100000m, false),
        new("PaymentTermNet60", 0.0m, 50000m, false),
        new("FreeOnboardingTier", 0.0m, 75000m, false),
        new("DedicatedCsm", 0.0m, 150000m, true)
    ];

    public AgentWhisperOverview GetOverview()
    {
        var activeCount = _sessions.Count;
        var totalWhispers = 1840;
        var adherenceRate = 94.2;
        var winRateLift = 28.5;

        return new AgentWhisperOverview(
            activeCount,
            totalWhispers,
            adherenceRate,
            winRateLift,
            _sessions,
            _guardrails
        );
    }

    public AnalyzeSpeechTurnResult AnalyzeSpeechTurn(AnalyzeSpeechTurnCommand command)
    {
        var turnId = $"TURN-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var isPriceObjection = command.TranscriptText.Contains("expensive", StringComparison.OrdinalIgnoreCase) ||
                               command.TranscriptText.Contains("budget", StringComparison.OrdinalIgnoreCase) ||
                               command.TranscriptText.Contains("discount", StringComparison.OrdinalIgnoreCase);

        var intent = isPriceObjection ? "PriceSensitivity" : "FeatureInquiry";
        var whisper = isPriceObjection
            ? "Buyer pushing on price. Counter with multi-year tier: 12% discount for 24-mo lock."
            : "Buyer exploring integrations. Emphasize native bi-directional CRM syncing.";

        return new AnalyzeSpeechTurnResult(
            turnId,
            command.CallSessionId,
            intent,
            whisper,
            isPriceObjection ? 12.0m : 0.0m,
            145 // Sub-200ms whisper dispatch latency
        );
    }

    public CalculateConcessionResult CalculateConcession(CalculateConcessionCommand command)
    {
        var decisionId = $"CONCESS-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var session = _sessions.FirstOrDefault(s => s.CallSessionId == command.CallSessionId) ?? _sessions[0];

        if (command.ProposedDiscountPct <= 15.0m)
        {
            return new CalculateConcessionResult(
                decisionId,
                "Approved_Autonomous",
                command.ProposedDiscountPct,
                command.RequestedPaymentDays > 0 ? command.RequestedPaymentDays : 45,
                "Within autonomous rep concession threshold; deal margin remains above 82%"
            );
        }

        return new CalculateConcessionResult(
            decisionId,
            "CounterOffer_Recommended",
            12.0m,
            60,
            "Proposed 18%+ discount exceeds AE limits. Counter with 12% discount + Net 60 extended payment terms."
        );
    }
}
