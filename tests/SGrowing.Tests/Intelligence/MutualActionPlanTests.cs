using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Intelligence.MutualActionPlan;
using Xunit;

namespace SGrowing.Tests.Intelligence;

public class MutualActionPlanTests
{
    private readonly MutualActionPlanEngine _engine = new();

    [Fact]
    public void GetPlan_ReturnsSeedPlanWithMilestones()
    {
        var plan = _engine.GetPlan("map_enterprise_aurora_01");

        plan.Should().NotBeNull();
        plan!.DealId.Should().Be("deal_aurora_9901");
        plan.ContractValueUsd.Should().Be(185000m);
        plan.Milestones.Should().HaveCount(4);
        plan.ChampionHealthScore.Should().BeGreaterThan(80);
    }

    [Fact]
    public void GetPlan_ReturnsNullForUnknownId()
    {
        var plan = _engine.GetPlan("unknown_plan_999");
        plan.Should().BeNull();
    }

    [Fact]
    public void GetPlanByDealId_ReturnsPlanForKnownDeal()
    {
        var plan = _engine.GetPlanByDealId("deal_aurora_9901");
        plan.Should().NotBeNull();
        plan!.PlanId.Should().Be("map_enterprise_aurora_01");
    }

    [Fact]
    public void GetPlanByDealId_ReturnsNullForUnknownDeal()
    {
        var plan = _engine.GetPlanByDealId("deal_never_seen_123");
        plan.Should().BeNull();
    }

    [Fact]
    public void CreatePlan_InitializesMilestonesAndTargetDates()
    {
        var request = new CreateMapRequest(
            DealId: "deal_quantum_fin_101",
            AccountName: "Quantum Financial AI",
            ContractValueUsd: 250000m,
            TargetCloseDate: DateTime.UtcNow.AddDays(45)
        );

        var plan = _engine.CreatePlan(request);

        plan.Should().NotBeNull();
        plan.DealId.Should().Be("deal_quantum_fin_101");
        plan.AccountName.Should().Be("Quantum Financial AI");
        plan.Milestones.Should().HaveCount(3);
        plan.IsAtRiskOfSlipping.Should().BeFalse();
    }

    [Fact]
    public void UpdateMilestone_BlocksCriticalMilestone_TriggersRiskFlag()
    {
        var planId = "map_enterprise_aurora_01";
        var request = new UpdateMilestoneRequest(
            PlanId: planId,
            MilestoneId: "ms_02", // Critical MSA review
            NewStatus: BuyerMilestoneStatus.Blocked,
            VerificationArtifactUrl: null
        );

        var updated = _engine.UpdateMilestone(request);

        updated.IsAtRiskOfSlipping.Should().BeTrue();
        updated.SlippageRiskRationale.Should().Contain("blocked");
        updated.ChampionHealthScore.Should().BeLessThan(88);
    }

    [Fact]
    public void UpdateMilestone_ThrowsWhenPlanNotFound()
    {
        var act = () => _engine.UpdateMilestone(new UpdateMilestoneRequest("missing_plan", "ms_01", BuyerMilestoneStatus.CompletedVerified, null));
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void RefreshRiskAnalysis_ThrowsWhenPlanNotFound()
    {
        var act = () => _engine.RefreshRiskAnalysis("missing_plan_id");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void UpdateMilestone_CompletesAllMilestones_AchievesPerfectHealth()
    {
        var planId = "map_enterprise_aurora_01";

        // Complete remaining milestones
        _engine.UpdateMilestone(new(planId, "ms_02", BuyerMilestoneStatus.CompletedVerified, null));
        _engine.UpdateMilestone(new(planId, "ms_03", BuyerMilestoneStatus.CompletedVerified, null));
        var completed = _engine.UpdateMilestone(new(planId, "ms_04", BuyerMilestoneStatus.CompletedVerified, null));

        completed.ChampionHealthScore.Should().Be(100);
        completed.IsAtRiskOfSlipping.Should().BeFalse();
    }
}
