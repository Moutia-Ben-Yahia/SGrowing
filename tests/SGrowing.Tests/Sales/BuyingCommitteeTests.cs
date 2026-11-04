using SGrowing.Api.Modules.Sales.BuyingCommittee;
using Xunit;

namespace SGrowing.Tests.Sales;

public class BuyingCommitteeTests
{
    [Fact]
    public void GetOverview_ComputesWeightedConsensusAndIdentifiesBlockers()
    {
        var engine = new BuyingCommitteeGraphEngine();

        var overview = engine.GetOverview("ACC-NORTHSTAR");

        Assert.NotNull(overview);
        Assert.Equal("ACC-NORTHSTAR", overview.AccountId);
        Assert.True(overview.CoalitionConsensusScore >= 0 && overview.CoalitionConsensusScore <= 100);
        Assert.True(overview.TotalStakeholdersCount >= 4);
        Assert.True(overview.AlignedChampionsCount >= 1);
        Assert.True(overview.ActiveBlockersCount >= 1);
        Assert.NotEmpty(overview.Interventions);
    }

    [Fact]
    public void AddStakeholder_CalculatesStatusAndEnrichesCommittee()
    {
        var engine = new BuyingCommitteeGraphEngine();
        var request = new AddStakeholderRequest(
            "ACC-NORTHSTAR",
            "Diana Prince",
            "General Counsel",
            "LEGAL_GATEKEEPER",
            0.8,
            -0.3,
            "Legal & Regulatory"
        );

        var member = engine.AddStakeholder(request);

        Assert.NotNull(member);
        Assert.StartsWith("STK-", member.StakeholderId);
        Assert.Equal("Diana Prince", member.FullName);
        Assert.Equal("BLOCKED", member.CurrentStatus);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Stakeholders, s => s.StakeholderId == member.StakeholderId);
    }

    [Fact]
    public void TriggerIntervention_SchedulesPlaybookAndLinksStakeholder()
    {
        var engine = new BuyingCommitteeGraphEngine();
        var request = new TriggerInterventionRequest(
            "ACC-NORTHSTAR",
            "STK-002",
            "ROI_CALCULATOR_SESSION",
            "Sarah Jenkins (RevOps Director)"
        );

        var intervention = engine.TriggerIntervention(request);

        Assert.NotNull(intervention);
        Assert.StartsWith("INT-", intervention.InterventionId);
        Assert.Equal("STK-002", intervention.TargetStakeholderId);
        Assert.Equal("PENDING", intervention.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Interventions, i => i.InterventionId == intervention.InterventionId);
    }

    [Fact]
    public void AddStakeholder_UndecidedSentiment_AssignsUndecidedStatus()
    {
        var engine = new BuyingCommitteeGraphEngine();
        var request = new AddStakeholderRequest(
            "ACC-NORTHSTAR",
            "Evan Wright",
            "Senior IT Director",
            "TECHNICAL_EVALUATOR",
            0.6,
            0.1, // Between -0.2 and 0.3 -> UNDECIDED
            "Infrastructure IT"
        );

        var member = engine.AddStakeholder(request);

        Assert.NotNull(member);
        Assert.Equal("UNDECIDED", member.CurrentStatus);
        Assert.Equal(0.1, member.SentimentScore);
    }

    [Fact]
    public void AddStakeholder_ClampsInfluenceWeightAndSentimentScore()
    {
        var engine = new BuyingCommitteeGraphEngine();
        var request = new AddStakeholderRequest(
            "ACC-NORTHSTAR",
            "Overly Powerful Executive",
            "Board Member",
            "ECONOMIC_BUYER",
            5.0,  // Clamp to 1.0
            -2.5, // Clamp to -1.0
            "Board of Directors"
        );

        var member = engine.AddStakeholder(request);

        Assert.NotNull(member);
        Assert.Equal(1.0, member.InfluenceWeight);
        Assert.Equal(-1.0, member.SentimentScore);
        Assert.Equal("BLOCKED", member.CurrentStatus);
    }

    [Fact]
    public void GetOverview_AddingChampion_IncreasesConsensusScoreAndAlignedCount()
    {
        var engine = new BuyingCommitteeGraphEngine();
        var initialOverview = engine.GetOverview("ACC-NORTHSTAR");

        var request = new AddStakeholderRequest(
            "ACC-NORTHSTAR",
            "Rachel Green",
            "VP Business Transformation",
            "CHAMPION",
            1.0,
            1.0,
            "Digital Innovation"
        );

        engine.AddStakeholder(request);

        var updatedOverview = engine.GetOverview("ACC-NORTHSTAR");
        Assert.Equal(initialOverview.TotalStakeholdersCount + 1, updatedOverview.TotalStakeholdersCount);
        Assert.Equal(initialOverview.AlignedChampionsCount + 1, updatedOverview.AlignedChampionsCount);
        Assert.True(updatedOverview.CoalitionConsensusScore >= initialOverview.CoalitionConsensusScore);
    }
}
