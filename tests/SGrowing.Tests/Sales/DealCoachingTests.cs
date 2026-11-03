using SGrowing.Api.Modules.Sales.DealCoaching;
using Xunit;

namespace SGrowing.Tests.Sales;

public sealed class DealCoachingTests
{
    private readonly DealCoachingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededSessionsAndMetrics()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveSessionsCount >= 1);
        Assert.True(overview.AverageRepAdherenceScore > 80.0);
        Assert.NotEmpty(overview.ActiveSessions);
        Assert.NotEmpty(overview.RecentObjections);
    }

    [Fact]
    public void DetectObjection_PricingPhrase_PivotsToValueRealization()
    {
        var session = _engine.StartSession("OPP-TEST-101", "Acme Enterprise Corp", "John Doe");
        var objection = _engine.DetectObjection(session.SessionId, "Your licensing cost is too expensive for our current budget freeze.");

        Assert.NotNull(objection);
        Assert.Equal(ObjectionCategory.PricingAndBudget, objection.Category);
        Assert.Contains("Pivot to realized value ledger", objection.RecommendedCounterTactic);
        Assert.True(objection.ConfidenceScore > 0.90);
    }

    [Fact]
    public void EvaluateRepScorecard_ReturnsOptimalTalkRatioAndMetrics()
    {
        var scorecard = _engine.EvaluateRepScorecard("Alexandra Hayes");

        Assert.NotNull(scorecard);
        Assert.StartsWith("SCORE-", scorecard.ScorecardId);
        Assert.Equal("Alexandra Hayes", scorecard.SalesRepName);
        Assert.True(scorecard.TalkToListenRatio <= 0.45);
        Assert.True(scorecard.OverallObjectionHandleRate > 85.0);
    }
}
