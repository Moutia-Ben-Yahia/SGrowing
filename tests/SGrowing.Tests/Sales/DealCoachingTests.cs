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

    [Fact]
    public void DetectObjection_SecurityAndCompliance_DeploysZeroTrustAttestation()
    {
        var session = _engine.StartSession("OPP-SEC-01", "SecureFin Corp", "Sarah Jenkins");
        var objection = _engine.DetectObjection(session.SessionId, "Our CISO requires dedicated audit logs and SOC2 type II compliance verification.");

        Assert.NotNull(objection);
        Assert.Equal(ObjectionCategory.SecurityAndCompliance, objection.Category);
        Assert.Contains("zero-trust eBPF attestation package", objection.RecommendedCounterTactic);
        Assert.Contains("SOC2 Type II", objection.EvidenceProofPoint);
    }

    [Fact]
    public void DetectObjection_CompetitorDisplacement_FramesAutonomousEngine()
    {
        var session = _engine.StartSession("OPP-COMP-01", "Alpha Logistics", "Marcus Vance");
        var objection = _engine.DetectObjection(session.SessionId, "We are currently evaluating Salesforce CRM and are already using another vendor.");

        Assert.NotNull(objection);
        Assert.Equal(ObjectionCategory.CompetitorDisplacement, objection.Category);
        Assert.Contains("system of record vs active autonomous growth engine", objection.RecommendedCounterTactic);
    }

    [Fact]
    public void DetectObjection_AuthorityAndTiming_SuggestsExecutiveBriefing()
    {
        var session = _engine.StartSession("OPP-TIME-01", "Apex Retail", "Alexandra Hayes");
        var objection = _engine.DetectObjection(session.SessionId, "The buying committee cannot make a decision until the end of the year.");

        Assert.NotNull(objection);
        Assert.Equal(ObjectionCategory.AuthorityAndTiming, objection.Category);
        Assert.Contains("executive briefing workshop", objection.RecommendedCounterTactic);
    }

    [Fact]
    public void DetectObjection_MultipleObjections_DecrementsAdherenceScoreAndIncrementsHandledCount()
    {
        var session = _engine.StartSession("OPP-MULTI-01", "Beta Commerce", "Alexandra Hayes");
        Assert.Equal(100.0, session.CurrentAdherenceScore);
        Assert.Equal(0, session.ObjectionsHandledCount);

        _engine.DetectObjection(session.SessionId, "Your product is too expensive.");
        _engine.DetectObjection(session.SessionId, "We have a severe budget freeze.");

        var overview = _engine.GetOverview();
        var updatedSession = overview.ActiveSessions.FirstOrDefault(s => s.SessionId == session.SessionId);
        Assert.NotNull(updatedSession);
        Assert.Equal(2, updatedSession.ObjectionsHandledCount);
        Assert.Equal(97.0, updatedSession.CurrentAdherenceScore); // 100 - 1.5 - 1.5
    }

    [Fact]
    public void DetectObjection_WhenRepeatedObjections_FloorsAdherenceScoreAt85()
    {
        var session = _engine.StartSession("OPP-FLOOR-01", "Delta Global", "Alexandra Hayes");

        for (int i = 0; i < 15; i++)
        {
            _engine.DetectObjection(session.SessionId, "This price is too expensive.");
        }

        var overview = _engine.GetOverview();
        var updated = overview.ActiveSessions.FirstOrDefault(s => s.SessionId == session.SessionId);
        Assert.NotNull(updated);
        Assert.Equal(15, updated.ObjectionsHandledCount);
        Assert.Equal(85.0, updated.CurrentAdherenceScore);
    }
}
