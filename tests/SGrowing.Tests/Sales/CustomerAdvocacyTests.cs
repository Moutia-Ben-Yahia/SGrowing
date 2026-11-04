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

        var overview = engine.GetOverview();
        var adv = overview.TopAdvocates.FirstOrDefault(a => a.AdvocateId == match.MatchedAdvocateId);
        Assert.NotNull(adv);
        Assert.Equal(2, adv.ReferenceCallsThisQuarter);
        Assert.Equal("QUOTA_REACHED", adv.Status);
        Assert.Equal(1750, adv.RewardPointsBalance);
    }

    [Fact]
    public void MatchReference_UnmatchedVertical_FallsBackToAvailableAdvocateOrTemp()
    {
        var engine = new CustomerAdvocacyEngine();
        var request = new MatchReferenceRequest(
            "HealthBio Genetics",
            "MID_MARKET",
            "HEALTHCARE",
            850
        );

        var match = engine.MatchReference(request);

        Assert.NotNull(match);
        Assert.Equal("HEALTHCARE", match.TargetIndustryVertical);
        Assert.Equal("SCHEDULED", match.ReferenceCallStatus);
        Assert.NotEmpty(match.MatchedAdvocateId);
    }

    [Fact]
    public void RecordCall_NonExistentMatchId_CreatesDefaultFallbackRecord()
    {
        var engine = new CustomerAdvocacyEngine();
        var recordRequest = new RecordReferenceCallRequest(
            "REF-UNKNOWN-999",
            "NO_SHOW",
            0,
            1
        );

        var record = engine.RecordCall(recordRequest);

        Assert.NotNull(record);
        Assert.Equal("REF-UNKNOWN-999", record.MatchId);
        Assert.Equal("NO_SHOW", record.ReferenceCallStatus);
        Assert.Equal("Generic Prospect", record.ProspectCompanyName);
    }

    [Fact]
    public void MatchReference_WithHighFatigueAdvocates_SelectsNextAvailableAdvocate()
    {
        var engine = new CustomerAdvocacyEngine();

        // Match first call to exhaust advocate quota
        var m1 = engine.MatchReference(new MatchReferenceRequest("Enterprise 1", "ENTERPRISE", "FINTECH", 5000));
        engine.RecordCall(new RecordReferenceCallRequest(m1.MatchId, "COMPLETED", 45, 5));

        // Now ADV-001 has reached quota (was 1/2, now 2/2 -> QUOTA_REACHED)
        // Next match should fallback to temporary advocate or available one
        var m2 = engine.MatchReference(new MatchReferenceRequest("Enterprise 2", "ENTERPRISE", "FINTECH", 6000));

        Assert.NotNull(m2);
        Assert.NotEqual(m1.MatchId, m2.MatchId);
        Assert.Equal("SCHEDULED", m2.ReferenceCallStatus);
    }

    [Fact]
    public void RecordCall_WhenOutcomeCancelled_UpdatesMatchStatusWithoutCrashing()
    {
        var engine = new CustomerAdvocacyEngine();
        var match = engine.MatchReference(new MatchReferenceRequest("Prospect Cancel Co", "MID_MARKET", "FINTECH", 200));

        var updated = engine.RecordCall(new RecordReferenceCallRequest(match.MatchId, "CANCELLED_BY_PROSPECT", 0, 0));

        Assert.NotNull(updated);
        Assert.Equal("CANCELLED_BY_PROSPECT", updated.ReferenceCallStatus);
    }

    [Fact]
    public void GetOverview_IncludesSeededAdvocatesAndMatches()
    {
        var engine = new CustomerAdvocacyEngine();
        var overview = engine.GetOverview();

        Assert.True(overview.TotalActiveAdvocatesCount >= 47);
        Assert.Contains(overview.TopAdvocates, a => a.AdvocateId == "ADV-001");
        Assert.Contains(overview.TopAdvocates, a => a.AdvocateId == "ADV-002");
        Assert.Contains(overview.RecentMatches, m => m.MatchId == "REF-MATCH-001");
    }
}
