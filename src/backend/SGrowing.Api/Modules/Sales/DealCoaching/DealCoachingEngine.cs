using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Sales.DealCoaching;

public interface IDealCoachingEngine
{
    DealCoachingOverview GetOverview();
    LiveCoachingSession StartSession(string opportunityId, string accountName, string salesRepName);
    ObjectionDetectedEvent DetectObjection(string sessionId, string transcribedSnippet);
    RepCoachingScorecard EvaluateRepScorecard(string salesRepName);
}

public sealed class DealCoachingEngine : IDealCoachingEngine
{
    private readonly ConcurrentDictionary<string, LiveCoachingSession> _sessions = new();
    private readonly ConcurrentDictionary<string, ObjectionDetectedEvent> _objectionEvents = new();
    private readonly List<CounterTacticSuggestion> _tacticsKnowledgeBase = [];

    public DealCoachingEngine()
    {
        SeedTactics();
        SeedInitialSessions();
    }

    private void SeedTactics()
    {
        _tacticsKnowledgeBase.Add(new CounterTacticSuggestion(
            "TAC-PRICE-01",
            ObjectionCategory.PricingAndBudget,
            "too expensive / budget freeze",
            "Acknowledge budget freeze, pivot to deferred milestone billing and quantified ROI timeline: 184% payback within 4.2 months.",
            "Average customer realizes $340k hard cash savings in Year 1.",
            28.4
        ));

        _tacticsKnowledgeBase.Add(new CounterTacticSuggestion(
            "TAC-SEC-01",
            ObjectionCategory.SecurityAndCompliance,
            "security review / SOC2 / multi-tenant",
            "Highlight sovereign data isolation, eBPF micro-segmentation, and offer instant pre-cleared Trust Vault package.",
            "Zero breaches across 1,200+ regulated enterprise deployments.",
            35.2
        ));

        _tacticsKnowledgeBase.Add(new CounterTacticSuggestion(
            "TAC-COMP-01",
            ObjectionCategory.CompetitorDisplacement,
            "evaluating Salesforce / HubSpot",
            "Frame legacy CRM as passive database vs Growth OS as autonomous revenue agent with real-time signal execution.",
            "4.2x faster lead velocity and 62% lower TCO compared to Salesforce custom builds.",
            41.0
        ));
    }

    private void SeedInitialSessions()
    {
        var session1 = new LiveCoachingSession(
            "COACH-SESS-001",
            "OPP-GLOBAL-890",
            "Vanguard Logistics International",
            "Alexandra Hayes",
            94.5,
            3,
            DateTime.UtcNow.AddMinutes(-22),
            "IN_PROGRESS"
        );
        _sessions.TryAdd(session1.SessionId, session1);

        var evt1 = new ObjectionDetectedEvent(
            "OBJ-EVT-001",
            session1.SessionId,
            "We love the platform but leadership has frozen all non-critical SaaS budgets for Q4.",
            ObjectionCategory.PricingAndBudget,
            0.96,
            "Pivot to milestone-gated deployment with net-60 deferred cash terms.",
            "Customer case study: Apex Freight saved $280k in first 90 days.",
            DateTime.UtcNow.AddMinutes(-8)
        );
        _objectionEvents.TryAdd(evt1.EventId, evt1);
    }

    public DealCoachingOverview GetOverview()
    {
        var active = _sessions.Values.ToList();
        var recent = _objectionEvents.Values.OrderByDescending(e => e.DetectedAtUtc).Take(10).ToList();

        return new DealCoachingOverview(
            ActiveSessionsCount: active.Count,
            TotalObjectionsDetectedQuarter: 142,
            AverageRepAdherenceScore: 92.8,
            WinRateAccelerationPct: 31.5,
            ActiveSessions: active,
            RecentObjections: recent
        );
    }

    public LiveCoachingSession StartSession(string opportunityId, string accountName, string salesRepName)
    {
        var session = new LiveCoachingSession(
            $"COACH-SESS-{Guid.NewGuid().ToString("N")[..8]}",
            opportunityId,
            accountName,
            salesRepName,
            100.0,
            0,
            DateTime.UtcNow,
            "IN_PROGRESS"
        );
        _sessions.TryAdd(session.SessionId, session);
        return session;
    }

    public ObjectionDetectedEvent DetectObjection(string sessionId, string transcribedSnippet)
    {
        var lower = transcribedSnippet.ToLowerInvariant();
        ObjectionCategory cat;
        string tactic;
        string proof;

        if (lower.Contains("expensive") || lower.Contains("budget") || lower.Contains("cost") || lower.Contains("discount"))
        {
            cat = ObjectionCategory.PricingAndBudget;
            tactic = "Pivot to realized value ledger & milestone cashflow amortization.";
            proof = "Demonstrates 3.8x ROI payback curve within 90 days.";
        }
        else if (lower.Contains("security") || lower.Contains("compliance") || lower.Contains("gdpr") || lower.Contains("audit"))
        {
            cat = ObjectionCategory.SecurityAndCompliance;
            tactic = "Deploy zero-trust eBPF attestation package and ephemeral credentials.";
            proof = "Pre-audited SOC2 Type II, ISO 27001, and HIPAA compliance guarantee.";
        }
        else if (lower.Contains("competitor") || lower.Contains("already using") || lower.Contains("vendor"))
        {
            cat = ObjectionCategory.CompetitorDisplacement;
            tactic = "Differentiate passive system of record vs active autonomous growth engine.";
            proof = "Customer migration report: +48% pipeline velocity.";
        }
        else
        {
            cat = ObjectionCategory.AuthorityAndTiming;
            tactic = "Offer executive briefing workshop with sponsor peer champion.";
            proof = "Accelerates legal & buying committee sign-off by 18 days.";
        }

        var detected = new ObjectionDetectedEvent(
            $"OBJ-{Guid.NewGuid().ToString("N")[..8]}",
            sessionId,
            transcribedSnippet,
            cat,
            0.94,
            tactic,
            proof,
            DateTime.UtcNow
        );

        _objectionEvents.TryAdd(detected.EventId, detected);

        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            _sessions[sessionId] = existing with
            {
                ObjectionsHandledCount = existing.ObjectionsHandledCount + 1,
                CurrentAdherenceScore = Math.Max(85.0, existing.CurrentAdherenceScore - 1.5)
            };
        }

        return detected;
    }

    public RepCoachingScorecard EvaluateRepScorecard(string salesRepName)
    {
        return new RepCoachingScorecard(
            $"SCORE-{Guid.NewGuid().ToString("N")[..8]}",
            salesRepName,
            OverallObjectionHandleRate: 91.4,
            TalkToListenRatio: 0.42, // optimal talk ratio under 45%
            SessionsCoachedTotal: 28,
            AverageDealSizeLiftPercentage: 24.8
        );
    }
}
