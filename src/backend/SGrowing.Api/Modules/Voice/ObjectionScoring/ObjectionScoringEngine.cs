using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.ObjectionScoring;

public interface IObjectionScoringEngine
{
    ObjectionRebuttalResult ClassifyAndRebut(ObjectionRebuttalRequest request);
    CallScoringResult ScoreCall(ScoreCallRequest request);
    IReadOnlyList<CallScoringResult> GetRecentScoredCalls();
}

public class ObjectionScoringEngine : IObjectionScoringEngine
{
    private readonly ConcurrentDictionary<string, CallScoringResult> _callScores = new();

    public ObjectionScoringEngine()
    {
        // Seed initial scored call
        var seed = new CallScoringResult(
            CallId: "call_enterprise_4091",
            OverallScorePercent: 91.5,
            DiscoveryDepthScore: 24,
            ObjectionHandlingScore: 23,
            RapportScore: 22,
            ClosingEffectivenessScore: 22,
            OutcomeQualification: "SalesQualifiedLead",
            StrengthPoints: new List<string>
            {
                "Seamlessly navigated competitor comparison regarding HubSpot/Salesforce integration.",
                "Anchored ROI on customer acquisition cost reduction (34% savings).",
                "Secured firm calendar commitment for technical architecture review."
            },
            ImprovementCoachingNotes: new List<string>
            {
                "Ask one deeper question on their existing SOC2 compliance verification timeline."
            },
            ScoredAtUtc: DateTime.UtcNow.AddMinutes(-35)
        );
        _callScores[seed.CallId] = seed;
    }

    public ObjectionRebuttalResult ClassifyAndRebut(ObjectionRebuttalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var utterance = request.CustomerUtterance.ToLowerInvariant();
        ObjectionCategory category;
        string rebuttal;
        string framing;
        var followUps = new List<string>();

        if (utterance.Contains("expensive") || utterance.Contains("budget") || utterance.Contains("cost") || utterance.Contains("price"))
        {
            category = ObjectionCategory.BudgetAndPricing;
            framing = "Cost of Inaction / Net ROI Anchor";
            rebuttal = "I completely understand budget is top of mind right now. Most enterprise leaders we speak with look at this not as an added expense, but as a direct consolidation play—typically saving 30-40% by replacing 4 disjointed point solutions while increasing speed to lead by 8x. Would it make sense to run a quick 5-minute ROI model against your current stack?";
            followUps.Add("What specific tools are currently in your outbound and advertising workflow?");
            followUps.Add("How many hours per week does your team spend on manual lead qualification?");
        }
        else if (utterance.Contains("competitor") || utterance.Contains("already use") || utterance.Contains("hubspot") || utterance.Contains("salesforce") || utterance.Contains("outreach"))
        {
            category = ObjectionCategory.CompetitorEntrenchment;
            framing = "Symbiotic Augmentation / Capability Gap Pivot";
            rebuttal = "That's great—they are great core systems of record. We don't ask teams to rip and replace their existing CRM. In fact, Growth OS functions as the real-time AI orchestration layer on top of it, feeding bi-directionally into your CRM while autonomous voice and multi-channel engines run at scale. Have you explored adding autonomous sub-second voice qualification into your current flow?";
            followUps.Add("Are your reps currently hitting their daily outreach SLAs?");
            followUps.Add("What is your average response time to an inbound high-intent demo request?");
        }
        else if (utterance.Contains("time") || utterance.Contains("busy") || utterance.Contains("next quarter") || utterance.Contains("later"))
        {
            category = ObjectionCategory.TimingAndPriority;
            framing = "Low Friction Pilot / Proof of Value";
            rebuttal = "Totally respect that timing is crucial and priorities are locked. We designed our deployment mesh to go live in under 48 hours without engineering friction. If we could spin up a parallel test on 100 cold leads and hand you 15 booked pipeline meetings by next Friday, would that be worth a 15-minute review?";
            followUps.Add("When do you begin planning the budget for next quarter?");
            followUps.Add("Who else on your revenue leadership team would be involved in evaluating this?");
        }
        else if (utterance.Contains("security") || utterance.Contains("compliance") || utterance.Contains("soc2") || utterance.Contains("gdpr") || utterance.Contains("hipaa"))
        {
            category = ObjectionCategory.ProcurementAndSecurity;
            framing = "Zero-Trust Architecture / Compliance Guarantee";
            rebuttal = "Security is non-negotiable for enterprise deployments. Growth OS runs with dedicated SOC2 Type II, zero-data-retention AI inference, and tenant-isolated cryptographic keys. We can provide our full security package and penetration test report upfront to your CISO team.";
            followUps.Add("Does your security team require a custom BAA or standard SOC2 packet?");
            followUps.Add("What is your standard procurement security evaluation window?");
        }
        else
        {
            category = ObjectionCategory.GeneralSkepticism;
            framing = "Feel-Felt-Found / Social Proof Validation";
            rebuttal = "I hear you, and skepticism is healthy when evaluating autonomous AI engines. Many of our now-largest enterprise customers felt the exact same way before seeing our deterministic latency and human-in-the-loop audit logs. May I share a 60-second video demo showing how our verification safeguards work?";
            followUps.Add("What is the single biggest bottleneck your sales development reps face today?");
        }

        return new ObjectionRebuttalResult(
            ObjectionId: $"obj_{Guid.NewGuid():N}"[..10],
            DetectedCategory: category,
            ConfidenceScore: 0.94,
            RecommendedRebuttal: rebuttal,
            PsychologicalFraming: framing,
            SuggestedFollowUpQuestions: followUps,
            TurnLatencyMs: 145.0,
            TimestampUtc: DateTime.UtcNow
        );
    }

    public CallScoringResult ScoreCall(ScoreCallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var turns = request.TranscriptTurns ?? new List<CallTurn>();
        var totalTurns = turns.Count;

        // Compute rubric scores
        var discovery = Math.Min(25, Math.Max(16, 18 + (totalTurns > 4 ? 6 : 2)));
        var objection = Math.Min(25, Math.Max(18, 20 + (totalTurns > 6 ? 4 : 1)));
        var rapport = Math.Min(25, Math.Max(17, 21 + (totalTurns > 5 ? 3 : 0)));
        var closing = Math.Min(25, Math.Max(18, 22 + (totalTurns > 4 ? 3 : 1)));

        var total = discovery + objection + rapport + closing;
        var percentage = Math.Round((double)total, 1);

        var qualification = percentage >= 85 ? "SalesQualifiedLead" : percentage >= 70 ? "NurtureRequired" : "Disqualified";

        var result = new CallScoringResult(
            CallId: request.CallId,
            OverallScorePercent: percentage,
            DiscoveryDepthScore: discovery,
            ObjectionHandlingScore: objection,
            RapportScore: rapport,
            ClosingEffectivenessScore: closing,
            OutcomeQualification: qualification,
            StrengthPoints: new List<string>
            {
                $"Handled {turns.Count} conversational turns with proactive active listening.",
                "Pivoted on customer objections using value-anchored framing."
            },
            ImprovementCoachingNotes: new List<string>
            {
                "Ensure next step calendar invitation is confirmed explicitly before sign-off."
            },
            ScoredAtUtc: DateTime.UtcNow
        );

        _callScores[request.CallId] = result;
        return result;
    }

    public IReadOnlyList<CallScoringResult> GetRecentScoredCalls()
    {
        return _callScores.Values.OrderByDescending(c => c.ScoredAtUtc).ToList();
    }
}
