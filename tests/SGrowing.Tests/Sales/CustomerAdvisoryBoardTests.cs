using SGrowing.Api.Modules.Sales.CustomerAdvisoryBoard;
using Xunit;

namespace SGrowing.Tests.Sales;

public class CustomerAdvisoryBoardTests
{
    [Fact]
    public void GetOverview_ReportsMembersPairingsAndThemes()
    {
        var engine = new CustomerAdvisoryBoardEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalActiveCabMembersCount >= 2);
        Assert.True(overview.AverageEngagementHealthScore > 70.0);
        Assert.True(overview.TotalInfluencedArrUsd > 100_000m);
        Assert.NotEmpty(overview.SponsorPairings);
        Assert.NotEmpty(overview.RecentRoadmapVotes);
    }

    [Fact]
    public void AlignSponsor_CreatesExecutivePairingWithHealthScore()
    {
        var engine = new CustomerAdvisoryBoardEngine();
        var request = new AlignSponsorRequest(
            "Acme Enterprise Systems",
            "Dr. Aris Thorne",
            "Chief Technology Officer",
            "Elena Rostova",
            "SVP Engineering"
        );

        var pairing = engine.AlignSponsor(request);

        Assert.NotNull(pairing);
        Assert.StartsWith("PAIR-", pairing.PairingId);
        Assert.Equal("Acme Enterprise Systems", pairing.AccountName);
        Assert.Equal("Dr. Aris Thorne", pairing.CustomerExecutiveName);
        Assert.Equal("Elena Rostova", pairing.VendorSponsorName);
        Assert.Equal("STRONGLY_ALIGNED", pairing.Status);
        Assert.True(pairing.EngagementHealthScore >= 80.0);

        var overview = engine.GetOverview();
        Assert.Contains(overview.SponsorPairings, p => p.PairingId == pairing.PairingId);
    }

    [Fact]
    public void VoteRoadmap_AppendsWeightedPriorityRoadmapVote()
    {
        var engine = new CustomerAdvisoryBoardEngine();
        var request = new VoteRoadmapRequest(
            "Cilium eBPF Cross-Cloud WireGuard Mesh",
            "Acme Enterprise Systems",
            1,
            2_450_000m
        );

        var initialOverview = engine.GetOverview();
        var initialVotesCount = initialOverview.RecentRoadmapVotes.Count;

        var vote = engine.VoteRoadmap(request);

        Assert.NotNull(vote);
        Assert.StartsWith("VOTE-", vote.VoteId);
        Assert.Equal("Cilium eBPF Cross-Cloud WireGuard Mesh", vote.FeatureName);
        Assert.Equal(1, vote.PriorityRank);
        Assert.Equal(2_450_000m, vote.AnnualContractValueWeightUsd);

        var updatedOverview = engine.GetOverview();
        Assert.Equal(initialVotesCount + 1, updatedOverview.RecentRoadmapVotes.Count);
        Assert.Contains(updatedOverview.RecentRoadmapVotes, v => v.VoteId == vote.VoteId);
    }

    [Fact]
    public void AlignSponsor_SetsPerfectInitialHealthScoreAndZeroDaysSinceTouchpoint()
    {
        var engine = new CustomerAdvisoryBoardEngine();
        var request = new AlignSponsorRequest(
            "Omega Logistics Global",
            "Katarina Rostov",
            "Chief Executive Officer",
            "Alexander Vance",
            "CEO"
        );

        var pairing = engine.AlignSponsor(request);

        Assert.NotNull(pairing);
        Assert.Equal(0, pairing.DaysSinceLastTouchpoint);
        Assert.Equal(100.0, pairing.EngagementHealthScore);
        Assert.Equal("STRONGLY_ALIGNED", pairing.Status);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(8, 5)]
    [InlineData(3, 3)]
    public void VoteRoadmap_PriorityRankClampedBetweenOneAndFive(int inputRank, int expectedClampedRank)
    {
        var engine = new CustomerAdvisoryBoardEngine();
        var request = new VoteRoadmapRequest(
            "Real-Time Deepfake Detection Engine",
            "Omega Logistics Global",
            inputRank,
            1_500_000m
        );

        var vote = engine.VoteRoadmap(request);

        Assert.NotNull(vote);
        Assert.Equal(expectedClampedRank, vote.PriorityRank);
    }

    [Fact]
    public void GetOverview_RecalculatesInfluencedArrSumAccurately()
    {
        var engine = new CustomerAdvisoryBoardEngine();
        var initial = engine.GetOverview();
        var initialArr = initial.TotalInfluencedArrUsd;

        var req = new VoteRoadmapRequest(
            "Autonomous CAPI Signal Verification",
            "Alpha Retail Holdings",
            2,
            750_000m
        );
        engine.VoteRoadmap(req);

        var updated = engine.GetOverview();
        Assert.Equal(initialArr + 750_000m, updated.TotalInfluencedArrUsd);
    }
}
