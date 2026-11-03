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
}
