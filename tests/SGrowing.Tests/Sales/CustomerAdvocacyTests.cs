using SGrowing.Api.Modules.Sales.CustomerAdvocacy;
using Xunit;

namespace SGrowing.Tests.Sales;

public class CustomerAdvocacyTests
{
    [Fact]
    public void GetOverview_ReportsAdvocatesCallsAndWinRateLift()
    {
        var engine = new CustomerAdvocacyEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalActiveAdvocatesCount > 40);
        Assert.True(overview.ReferenceCallsCompletedQuarter >= 20);
        Assert.True(overview.WinRateLiftPct > 30.0);
        Assert.True(overview.TotalRewardPointsDistributed > 10_000);
        Assert.NotEmpty(overview.TopAdvocates);
        Assert.NotEmpty(overview.RecentMatches);
    }

    [Fact]
    public void MatchReference_FindsAvailableAdvocateAndCreatesScheduledMatch()
    {
        var engine = new CustomerAdvocacyEngine();
        var request = new MatchReferenceRequest(
            "Fintech Apex Ventures",
            "ENTERPRISE",
            "FINTECH",
            3500
        );

        var match = engine.MatchReference(request);

        Assert.NotNull(match);
        Assert.StartsWith("REF-MATCH-", match.MatchId);
        Assert.Equal("Fintech Apex Ventures", match.ProspectCompanyName);
        Assert.Equal("FINTECH", match.TargetIndustryVertical);
        Assert.Equal("SCHEDULED", match.ReferenceCallStatus);
        Assert.True(match.MatchCompatibilityScore > 90.0);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentMatches, m => m.MatchId == match.MatchId);
    }

    [Fact]
    public void RecordCall_UpdatesStatusAndAwardsRewardPoints()
    {
        var engine = new CustomerAdvocacyEngine();
        var matchRequest = new MatchReferenceRequest(
            "Acme Global Holdings",
            "ENTERPRISE",
            "FINTECH",
            2000
        );
        var match = engine.MatchReference(matchRequest);

        var recordRequest = new RecordReferenceCallRequest(
            match.MatchId,
            "COMPLETED",
            30,
            5
        );

        var updated = engine.RecordCall(recordRequest);

        Assert.NotNull(updated);
        Assert.Equal("COMPLETED", updated.ReferenceCallStatus);
    }
}
