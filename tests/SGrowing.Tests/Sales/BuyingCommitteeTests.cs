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
}
