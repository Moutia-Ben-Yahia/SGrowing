using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Sales.CompetitiveBattlecard;

public sealed class CompetitiveBattlecardEngine
{
    private readonly ConcurrentDictionary<string, CompetitorProfileRecord> _competitors = new();
    private readonly ConcurrentDictionary<string, LiveBattlecardRecord> _battlecards = new();
    private readonly ConcurrentBag<WinLossSignalRecord> _signals = new();

    public CompetitiveBattlecardEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var c1 = new CompetitorProfileRecord(
            "COMP-APEX-01",
            "ApexCloud Legacy CRM",
            "Enterprise CRM & Sales Automation",
            320000.00m,
            68.5,
            new[] { "Rigid multi-year lock-in with 25% annual price escalators", "Lack of native AI voice telephony streaming and real-time CAPI sync", "Slow 9-month professional services onboarding" },
            new[] { "Offer zero-cost migration credits for remaining legacy term", "Bundle native real-time telephony with no third-party telephony taxes" },
            "HIGH"
        );

        var c2 = new CompetitorProfileRecord(
            "COMP-TITAN-02",
            "Titanium Marketing Cloud",
            "Marketing Automation & DSP",
            210000.00m,
            74.2,
            new[] { "Siloed customer identities unable to reconcile anonymous ad sessions", "No sovereign e-invoicing clearance or CBAM carbon accounting" },
            new[] { "Demonstrate SGrowing zero-loss identity stitching graph live", "Highlight instant multi-jurisdiction compliance" },
            "MEDIUM"
        );

        _competitors[c1.CompetitorId] = c1;
        _competitors[c2.CompetitorId] = c2;

        var s1 = new WinLossSignalRecord(
            "SIG-WIN-101",
            c1.CompetitorId,
            "WON",
            450000.00m,
            "Replaced ApexCloud due to mandatory price increase and slow onboarding",
            DateTime.UtcNow.AddDays(-5)
        );

        var s2 = new WinLossSignalRecord(
            "SIG-WIN-102",
            c2.CompetitorId,
            "WON",
            280000.00m,
            "Won on real-time clean room attribution and native omni-hub telemetry",
            DateTime.UtcNow.AddDays(-2)
        );

        _signals.Add(s1);
        _signals.Add(s2);
    }

    public BattlecardOverview GetOverview()
    {
        var compList = _competitors.Values.ToList();
        var signalList = _signals.ToList();
        var totalPipeline = compList.Sum(c => c.EstimatedAverageDealSizeUsd);
        var winSignals = signalList.Count(s => s.Outcome == "WON");
        var aggWinRate = signalList.Count > 0 ? (double)winSignals / signalList.Count * 100.0 : 71.3;

        return new BattlecardOverview(
            compList.Count,
            aggWinRate,
            _battlecards.Count,
            totalPipeline,
            compList,
            signalList
        );
    }

    public LiveBattlecardRecord GenerateBattlecard(GenerateBattlecardRequest request)
    {
        var competitor = _competitors.GetValueOrDefault(request.CompetitorId);
        var compName = competitor?.CompetitorName ?? "Unknown Competitor";
        var cardId = $"BTC-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var digest = $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}";

        var playbook = new List<string>
        {
            $"Direct Positioning: Emphasize SGrowing autonomous AI mesh over {compName}'s legacy manual configuration",
            $"Commercial Counter: Offer buyout incentive for existing {compName} contract terms",
            "Technical Differentiator: Demonstrate zero-latency sub-second signal dispatch across voice and ad channels"
        };

        var battlecard = new LiveBattlecardRecord(
            cardId,
            request.CompetitorId,
            request.OpportunityId,
            DateTime.UtcNow.ToString("o"),
            $"Live Competitive Counter-Strategy for {compName} in {request.TargetVertical}",
            playbook,
            digest
        );

        _battlecards[cardId] = battlecard;
        return battlecard;
    }

    public WinLossSignalRecord SubmitWinLossSignal(SubmitWinLossSignalRequest request)
    {
        var signalId = $"SIG-{request.Outcome}-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var signal = new WinLossSignalRecord(
            signalId,
            request.CompetitorId,
            request.Outcome.ToUpperInvariant(),
            request.DealValueUsd,
            request.KeyDecidingFactor,
            DateTime.UtcNow
        );

        _signals.Add(signal);
        return signal;
    }
}
