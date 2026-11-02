using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.CallCopilot;

public interface ICallCopilotEngine
{
    List<CallCopilotSession> ListSessions();
    CallCopilotSession? GetSession(string sessionId);
    CallCopilotSession StartSession(string agentId, string prospectPhone, string prospectCompany);
    DiarizedUtterance IngestUtterance(IngestUtteranceRequest request);
    CallSummaryResult EndSessionAndSummarize(string sessionId);
    List<LiveObjectionBattlecard> GetAvailableBattlecards();
}

public class CallCopilotEngine : ICallCopilotEngine
{
    private readonly ConcurrentDictionary<string, CallCopilotSession> _sessions = new();
    private readonly List<LiveObjectionBattlecard> _battlecards = new();

    public CallCopilotEngine()
    {
        SeedDefaultBattlecards();
        SeedSampleSession();
    }

    private void SeedDefaultBattlecards()
    {
        _battlecards.Add(new LiveObjectionBattlecard(
            CardId: "bc_pricing_roi",
            TriggerKeyword: "budget",
            ObjectionCategory: "Pricing / Cost",
            RecommendedResponse: "Acknowledge budget caution, then emphasize that SGrowing replaces 5 separate SaaS tools (CDP, Dialers, CMS, AI tools, Attribution), delivering a median 340% ROI within 90 days.",
            ProofPointMetric: "Average cost reduction of $48,000/yr per sales pod"
        ));

        _battlecards.Add(new LiveObjectionBattlecard(
            CardId: "bc_competitor_stack",
            TriggerKeyword: "hubspot",
            ObjectionCategory: "Legacy Sprawl",
            RecommendedResponse: "Position SGrowing's native real-time WebRTC SDR voice agents and automated multi-touch attribution as next-gen AI capabilities that legacy CRMs charge six figures to replicate via fragile plugins.",
            ProofPointMetric: "Zero-latency two-way sync vs 15-minute polling delays"
        ));

        _battlecards.Add(new LiveObjectionBattlecard(
            CardId: "bc_security_compliance",
            TriggerKeyword: "security",
            ObjectionCategory: "Enterprise Governance",
            RecommendedResponse: "Reassure that SGrowing provides air-gapped self-hosted deployments with cryptographically verifiable offline licenses and zero external telemetry.",
            ProofPointMetric: "SOC2 Type II, HIPAA, and GDPR sovereign data compliant"
        ));
    }

    private void SeedSampleSession()
    {
        var sessionId = "call_demo_live";
        var utterances = new List<DiarizedUtterance>
        {
            new("utt_1", SpeakerRole.Agent, "Hello David, thanks for joining! How are your outbound campaigns pacing this quarter?", 0.98, 0.45, 3.2, DateTime.UtcNow.AddMinutes(-5)),
            new("utt_2", SpeakerRole.Prospect, "We're struggling with response rates and our SDR budget is tight this quarter.", 0.95, -0.35, 4.1, DateTime.UtcNow.AddMinutes(-4)),
            new("utt_3", SpeakerRole.Agent, "I understand completely. That's actually why we built autonomous multi-channel warmup and voice dialers.", 0.99, 0.60, 3.8, DateTime.UtcNow.AddMinutes(-3))
        };

        var session = new CallCopilotSession(
            SessionId: sessionId,
            AgentId: "agent_sarah_connor",
            ProspectPhone: "+1-415-555-0199",
            ProspectCompany: "Nexus Innovations Inc",
            Status: "Active",
            Utterances: utterances,
            SurfacedBattlecards: new List<LiveObjectionBattlecard> { _battlecards[0] },
            AverageSentiment: 0.23,
            ExecutiveSummary: null,
            StartedAtUtc: DateTime.UtcNow.AddMinutes(-5)
        );

        _sessions[sessionId] = session;
    }

    public List<CallCopilotSession> ListSessions() =>
        _sessions.Values.OrderByDescending(s => s.StartedAtUtc).ToList();

    public CallCopilotSession? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    public CallCopilotSession StartSession(string agentId, string prospectPhone, string prospectCompany)
    {
        var sessionId = "call_" + Guid.NewGuid().ToString("N")[..8];
        var session = new CallCopilotSession(
            SessionId: sessionId,
            AgentId: agentId,
            ProspectPhone: prospectPhone,
            ProspectCompany: prospectCompany,
            Status: "Active",
            Utterances: new List<DiarizedUtterance>(),
            SurfacedBattlecards: new List<LiveObjectionBattlecard>(),
            AverageSentiment: 0.0,
            ExecutiveSummary: null,
            StartedAtUtc: DateTime.UtcNow
        );

        _sessions[sessionId] = session;
        return session;
    }

    public DiarizedUtterance IngestUtterance(IngestUtteranceRequest request)
    {
        if (!_sessions.TryGetValue(request.SessionId, out var session))
        {
            throw new KeyNotFoundException($"Call session '{request.SessionId}' not found.");
        }

        // Basic sentiment estimation (-1.0 to +1.0)
        var textLower = request.Text.ToLowerInvariant();
        var sentiment = 0.0;
        if (textLower.Contains("struggling") || textLower.Contains("expensive") || textLower.Contains("problem") || textLower.Contains("tight"))
        {
            sentiment -= 0.60;
        }
        else if (textLower.Contains("great") || textLower.Contains("interesting") || textLower.Contains("love") || textLower.Contains("perfect"))
        {
            sentiment += 0.75;
        }

        var utterance = new DiarizedUtterance(
            UtteranceId: "utt_" + Guid.NewGuid().ToString("N")[..8],
            Speaker: request.Speaker,
            Text: request.Text,
            Confidence: 0.96,
            SentimentScore: sentiment,
            DurationSeconds: request.DurationSeconds,
            TimestampUtc: DateTime.UtcNow
        );

        session.Utterances.Add(utterance);

        // Check for battlecard triggers
        foreach (var card in _battlecards)
        {
            if (textLower.Contains(card.TriggerKeyword) && !session.SurfacedBattlecards.Any(c => c.CardId == card.CardId))
            {
                session.SurfacedBattlecards.Add(card);
            }
        }

        // Recompute average sentiment
        var avg = session.Utterances.Average(u => u.SentimentScore);
        _sessions[request.SessionId] = session with { AverageSentiment = Math.Round(avg, 2) };

        return utterance;
    }

    public CallSummaryResult EndSessionAndSummarize(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Call session '{sessionId}' not found.");
        }

        var summary = $"Executive summary for call with {session.ProspectCompany}: Discussed outbound challenges, SDR capacity, and demonstrated SGrowing autonomous voice capabilities. Prospect receptive to ROI narrative.";
        var actionItems = new List<string>
        {
            "Send personalized CPQ quote with 90-day ROI calculation.",
            "Schedule technical walkthrough with Enterprise Solutions Architect.",
            "Log disposition 'Interested - Demo Completed' to CRM pipeline."
        };

        var finalSession = session with
        {
            Status = "Completed",
            ExecutiveSummary = summary
        };
        _sessions[sessionId] = finalSession;

        return new CallSummaryResult(
            SessionId: sessionId,
            ExecutiveSummary: summary,
            ActionItems: actionItems,
            OverallDisposition: "Qualified Opportunity",
            FinalSentiment: finalSession.AverageSentiment
        );
    }

    public List<LiveObjectionBattlecard> GetAvailableBattlecards() => _battlecards;
}
