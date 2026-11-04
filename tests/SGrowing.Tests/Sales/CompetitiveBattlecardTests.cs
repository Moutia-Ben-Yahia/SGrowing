using System;
using System.Linq;
using SGrowing.Api.Modules.Sales.CompetitiveBattlecard;
using Xunit;

namespace SGrowing.Tests.Sales;

public sealed class CompetitiveBattlecardTests
{
    [Fact]
    public void GetOverview_ReturnsInitialCompetitorsAndSignals()
    {
        var engine = new CompetitiveBattlecardEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TrackedCompetitorsCount >= 2);
        Assert.True(overview.AggregateWinRatePct > 50.0);
        Assert.NotEmpty(overview.Competitors);
        Assert.NotEmpty(overview.RecentSignals);
    }

    [Fact]
    public void GenerateBattlecard_ProducesActionablePlaybook()
    {
        var engine = new CompetitiveBattlecardEngine();
        var request = new GenerateBattlecardRequest(
            "COMP-APEX-01",
            "OPP-ENTERPRISE-5544",
            "Fintech & Digital Banking",
            500000.00m
        );

        var battlecard = engine.GenerateBattlecard(request);

        Assert.NotNull(battlecard);
        Assert.StartsWith("BTC-", battlecard.BattlecardId);
        Assert.NotEmpty(battlecard.ObjectionHandlingPlaybook);
        Assert.StartsWith("0x", battlecard.CryptographicProofDigest);
    }

    [Fact]
    public void SubmitWinLossSignal_RecordsDealOutcome()
    {
        var engine = new CompetitiveBattlecardEngine();
        var request = new SubmitWinLossSignalRequest(
            "COMP-TITAN-02",
            "WON",
            320000.00m,
            "Replaced due to SGrowing native multi-channel agent orchestration"
        );

        var signal = engine.SubmitWinLossSignal(request);

        Assert.NotNull(signal);
        Assert.StartsWith("SIG-WON-", signal.SignalId);
        Assert.Equal("WON", signal.Outcome);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentSignals, s => s.SignalId == signal.SignalId);
    }

    [Fact]
    public void GenerateBattlecard_UnknownCompetitor_FallsBackToGenericPositioning()
    {
        var engine = new CompetitiveBattlecardEngine();
        var request = new GenerateBattlecardRequest(
            "COMP-UNKNOWN-99",
            "OPP-GEN-101",
            "Healthcare Systems",
            180000.00m
        );

        var battlecard = engine.GenerateBattlecard(request);

        Assert.NotNull(battlecard);
        Assert.Contains("Unknown Competitor", battlecard.ExecutiveSummary);
        Assert.Contains(battlecard.ObjectionHandlingPlaybook, p => p.Contains("Unknown Competitor"));
        Assert.StartsWith("0x", battlecard.CryptographicProofDigest);
    }

    [Fact]
    public void SubmitWinLossSignal_LostDeal_DecreasesAggregateWinRate()
    {
        var engine = new CompetitiveBattlecardEngine();
        // Seeded state has 2 WON signals (100% win rate)
        var initialOverview = engine.GetOverview();
        Assert.Equal(100.0, initialOverview.AggregateWinRatePct);

        var lostRequest = new SubmitWinLossSignalRequest(
            "COMP-APEX-01",
            "LOST",
            250000.00m,
            "Buyer renewed incumbent contract due to internal budget freezes"
        );
        engine.SubmitWinLossSignal(lostRequest);

        var updatedOverview = engine.GetOverview();
        // 2 wins out of 3 signals = 66.7%
        Assert.Equal(66.7, Math.Round(updatedOverview.AggregateWinRatePct, 1));
        Assert.Equal(3, updatedOverview.RecentSignals.Count);
    }

    [Fact]
    public void SubmitWinLossSignal_NormalizesOutcomeToUppercase()
    {
        var engine = new CompetitiveBattlecardEngine();
        var request = new SubmitWinLossSignalRequest(
            "COMP-APEX-01",
            "won",
            120000.00m,
            "Lower TCO demonstrated"
        );

        var signal = engine.SubmitWinLossSignal(request);

        Assert.Equal("WON", signal.Outcome);
        Assert.StartsWith("SIG-WON-", signal.SignalId);
    }

    [Fact]
    public void GenerateBattlecard_IncrementsActiveBattlecardsCountInOverview()
    {
        var engine = new CompetitiveBattlecardEngine();
        var initialOverview = engine.GetOverview();
        Assert.Equal(0, initialOverview.ActiveBattlecardsCount);

        var request = new GenerateBattlecardRequest(
            "COMP-APEX-01",
            "OPP-SCALE-88",
            "Fintech",
            400000m
        );
        engine.GenerateBattlecard(request);

        var updatedOverview = engine.GetOverview();
        Assert.Equal(1, updatedOverview.ActiveBattlecardsCount);
    }

    [Fact]
    public void GetOverview_TotalProtectedPipelineValueUsd_ComputesSumOfCompetitorAverageDealSizes()
    {
        var engine = new CompetitiveBattlecardEngine();
        var overview = engine.GetOverview();

        // Seeded: COMP-APEX-01 (320,000) + COMP-TITAN-02 (210,000) = 530,000
        Assert.Equal(530000.00m, overview.TotalProtectedPipelineValueUsd);
    }
}
