using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.AgentAssist;

public interface IAgentAssistEngine
{
    LiveAgentAssistSession StreamChunk(StreamChunkRequest request);
    LiveAgentAssistSession? GetSessionById(string callSessionId);
    LiveAgentAssistSession TriggerBattlecard(string callSessionId, string competitorName);
}

public class AgentAssistEngine : IAgentAssistEngine
{
    private readonly ConcurrentDictionary<string, LiveAgentAssistSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CompetitorBattlecard> _battlecardsCatalog = new()
    {
        new("Salesforce", "salesforce", new List<string>
        {
            "Salesforce requires 6-9 months implementation; SGrowing deploys in 48 hours.",
            "Zero per-seat licensing penalties; unlimited autonomous agent swarms included.",
            "Sub-5ms vector semantic cache eliminates massive API call add-ons."
        }, "72% lower Total Cost of Ownership"),

        new("HubSpot", "hubspot", new List<string>
        {
            "HubSpot lacks native multi-region zero-trust disaster recovery.",
            "SGrowing provides verifiable SHA-256 Merkle audit vaults for SOC2 & ISO 27001.",
            "Native duplex WebRTC audio streaming under 150ms latency vs external app marketplace plugins."
        }, "3.4x faster lead response velocity"),

        new("Gong", "gong", new List<string>
        {
            "Gong only records calls post-facto; SGrowing injects in-flight live whisper coaching.",
            "Full-funnel attribution with game-theoretic Shapley values built into the same OS."
        }, "28% higher close rate on objection calls")
    };

    public AgentAssistEngine()
    {
        // Seed default live sales call
        var initialChunks = new List<TranscriptChunk>
        {
            new("chk_1", "Customer", "We are currently using HubSpot and evaluating Salesforce for enterprise scale.", 12.0, "Neutral"),
            new("chk_2", "Representative", "Understood. Many high-growth teams hit scalability hurdles when trying to integrate external agents.", 28.0, "Positive")
        };

        var initialBattlecards = new List<CompetitorBattlecard>
        {
            _battlecardsCatalog.First(b => b.CompetitorName == "HubSpot")
        };

        var initialSuggestions = new List<WhisperSuggestion>
        {
            new("w_1", "DiscoveryQuestion", "Ask: 'What specific pipeline visibility gaps prompted your team to look beyond HubSpot?'", "High")
        };

        _sessions["call_live_501"] = new LiveAgentAssistSession(
            "call_live_501",
            "Marcus Brody",
            "Vanguard Logistics Inc",
            RepTalkTimeSeconds: 45.0,
            CustomerTalkTimeSeconds: 65.0,
            RepTalkPercentage: 40.9,
            TalkPacingAlertActive: false,
            initialChunks,
            initialBattlecards,
            initialSuggestions
        );
    }

    public LiveAgentAssistSession StreamChunk(StreamChunkRequest request)
    {
        if (!_sessions.TryGetValue(request.CallSessionId, out var session))
        {
            session = new LiveAgentAssistSession(
                request.CallSessionId,
                "Alex Rivera",
                "Prospective Enterprise",
                0.0,
                0.0,
                50.0,
                false,
                new List<TranscriptChunk>(),
                new List<CompetitorBattlecard>(),
                new List<WhisperSuggestion>()
            );
        }

        var isRep = request.Speaker.Equals("Representative", StringComparison.OrdinalIgnoreCase);
        var repTime = isRep ? session.RepTalkTimeSeconds + request.DurationSeconds : session.RepTalkTimeSeconds;
        var custTime = !isRep ? session.CustomerTalkTimeSeconds + request.DurationSeconds : session.CustomerTalkTimeSeconds;
        var total = repTime + custTime;
        var repPct = total > 0 ? Math.Round((repTime / total) * 100.0, 1) : 50.0;
        var pacingAlert = repPct > 65.0; // Warning if rep is dominating conversation

        var chunk = new TranscriptChunk(
            $"chk_{DateTime.UtcNow.Ticks % 100000}",
            request.Speaker,
            request.Text,
            total,
            request.Text.Contains("concern", StringComparison.OrdinalIgnoreCase) || request.Text.Contains("expensive", StringComparison.OrdinalIgnoreCase)
                ? "Skeptical"
                : "Neutral"
        );

        var updatedChunks = new List<TranscriptChunk>(session.RecentTranscripts) { chunk };
        if (updatedChunks.Count > 10) updatedChunks.RemoveAt(0);

        var battlecards = new List<CompetitorBattlecard>(session.ActiveBattlecards);
        foreach (var bc in _battlecardsCatalog)
        {
            if (request.Text.Contains(bc.TriggerKeyword, StringComparison.OrdinalIgnoreCase) && !battlecards.Any(b => b.CompetitorName == bc.CompetitorName))
            {
                battlecards.Add(bc);
            }
        }

        var suggestions = new List<WhisperSuggestion>(session.LiveSuggestions);
        if (pacingAlert && !suggestions.Any(s => s.Category == "PacingAlert"))
        {
            suggestions.Insert(0, new WhisperSuggestion($"pacing_{DateTime.UtcNow.Ticks % 1000}", "PacingAlert", "Slow down and pause: You are speaking 65%+ of the time. Ask an open-ended question.", "High"));
        }

        var updated = session with
        {
            RepTalkTimeSeconds = Math.Round(repTime, 1),
            CustomerTalkTimeSeconds = Math.Round(custTime, 1),
            RepTalkPercentage = repPct,
            TalkPacingAlertActive = pacingAlert,
            RecentTranscripts = updatedChunks,
            ActiveBattlecards = battlecards,
            LiveSuggestions = suggestions
        };

        _sessions[request.CallSessionId] = updated;
        return updated;
    }

    public LiveAgentAssistSession? GetSessionById(string callSessionId)
    {
        _sessions.TryGetValue(callSessionId, out var session);
        return session;
    }

    public LiveAgentAssistSession TriggerBattlecard(string callSessionId, string competitorName)
    {
        if (!_sessions.TryGetValue(callSessionId, out var session))
        {
            session = new LiveAgentAssistSession(callSessionId, "Rep", "Prospect", 30, 30, 50, false, new List<TranscriptChunk>(), new List<CompetitorBattlecard>(), new List<WhisperSuggestion>());
        }

        var card = _battlecardsCatalog.FirstOrDefault(b => b.CompetitorName.Equals(competitorName, StringComparison.OrdinalIgnoreCase));
        if (card != null && !session.ActiveBattlecards.Any(b => b.CompetitorName == card.CompetitorName))
        {
            var updatedCards = new List<CompetitorBattlecard>(session.ActiveBattlecards) { card };
            session = session with { ActiveBattlecards = updatedCards };
            _sessions[callSessionId] = session;
        }

        return session;
    }
}
