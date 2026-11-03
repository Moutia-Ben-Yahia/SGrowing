using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Intelligence.WinLoss;

public interface IWinLossEngine
{
    DealPostMortem AnalyzeDeal(AnalyzeDealRequest request);
    DealPostMortem? GetPostMortemByDealId(string dealId);
    List<DealPostMortem> GetAllPostMortems();
}

public class WinLossEngine : IWinLossEngine
{
    private readonly ConcurrentDictionary<string, DealPostMortem> _postMortems = new(StringComparer.OrdinalIgnoreCase);

    public WinLossEngine()
    {
        // Seed initial deals
        AnalyzeDeal(new AnalyzeDealRequest(
            "deal_omega_401",
            "Omega FinTech Corp",
            120000m,
            DealOutcome.LostToCompetitor,
            "Lost to Salesforce because customer required specialized legacy AS400 connector out of the box and our security audit was requested too late.",
            "Salesforce"
        ));

        AnalyzeDeal(new AnalyzeDealRequest(
            "deal_quantum_402",
            "Quantum Health Systems",
            250000m,
            DealOutcome.WonClosed,
            "Won because our autonomous agent swarm objection handling and sub-150ms voice WebRTC beat HubSpot by a landslide.",
            "HubSpot"
        ));
    }

    public DealPostMortem AnalyzeDeal(AnalyzeDealRequest request)
    {
        var causes = new List<RootCauseFactor>();
        var competitorInsights = new List<CompetitorTacticInsight>();
        var featureGaps = new List<string>();

        var notes = request.RepNotes.ToLowerInvariant();

        if (notes.Contains("connector") || notes.Contains("integration"))
        {
            causes.Add(new RootCauseFactor("IntegrationGap", "Legacy third-party enterprise connector missing.", 0.85));
            featureGaps.Add("Pre-built Legacy Enterprise Data Connectors");
        }

        if (notes.Contains("security") || notes.Contains("compliance") || notes.Contains("ciso"))
        {
            causes.Add(new RootCauseFactor("SecurityHurdle", "Security / CISO verification introduced too late in funnel.", 0.70));
            featureGaps.Add("Automated Day-1 CISO Compliance Package Delivery");
        }

        if (notes.Contains("price") || notes.Contains("discount") || notes.Contains("expensive"))
        {
            causes.Add(new RootCauseFactor("Pricing", "Price resistance during negotiation phase.", 0.60));
        }

        if (!string.IsNullOrWhiteSpace(request.CompetitorName))
        {
            competitorInsights.Add(new CompetitorTacticInsight(
                request.CompetitorName,
                request.Outcome == DealOutcome.LostToCompetitor
                    ? $"{request.CompetitorName} bundled legacy connectors and offered predatory multi-year discounting."
                    : $"{request.CompetitorName} suffered from slow voice latency and high seat-license bloat.",
                "Deploy proactive ROI TCO calculators and Day-1 CISO packages."
            ));
        }

        if (causes.Count == 0)
        {
            causes.Add(new RootCauseFactor("General", request.Outcome == DealOutcome.WonClosed ? "Strong product capability alignment." : "Budget cycle deferred to next fiscal year.", 0.50));
        }

        var postMortem = new DealPostMortem(
            request.DealId,
            request.CompanyName,
            request.DealValue,
            request.Outcome,
            causes,
            competitorInsights,
            featureGaps,
            DateTime.UtcNow
        );

        _postMortems[request.DealId] = postMortem;
        return postMortem;
    }

    public DealPostMortem? GetPostMortemByDealId(string dealId)
    {
        _postMortems.TryGetValue(dealId, out var pm);
        return pm;
    }

    public List<DealPostMortem> GetAllPostMortems()
    {
        return _postMortems.Values.OrderByDescending(p => p.AnalyzedAtUtc).ToList();
    }
}
