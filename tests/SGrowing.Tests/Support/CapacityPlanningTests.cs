using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Support.CapacityPlanning;
using Xunit;

namespace SGrowing.Tests.Support;

public class CapacityPlanningTests
{
    private readonly CapacityPlanningEngine _engine = new();

    [Fact]
    public void GetPlan_Returns24HourIntervalsAndMetrics()
    {
        var plan = _engine.GetPlan("cap_plan_global_support");

        plan.Should().NotBeNull();
        plan.HourlyIntervals.Should().HaveCount(24);
        plan.TotalDailyVolume.Should().BeGreaterThan(1000);
        plan.AiDeflectionRatePct.Should().Be(45.0);
        plan.TotalDeficitHours.Should().Be(1); // Hour 14 seed deficit
    }

    [Fact]
    public void RebalanceCapacity_EliminatesDeficitAndImprovesSla()
    {
        var planId = "cap_plan_global_support";
        var request = new RebalanceCapacityRequest(planId, AdditionalAgentsAllocated: 4);

        var rebalanced = _engine.RebalanceCapacity(request);

        rebalanced.TotalDeficitHours.Should().Be(0);
        var hour14 = rebalanced.HourlyIntervals.First(i => i.HourOfDay == 14);
        hour14.HasStaffingDeficit.Should().BeFalse();
        hour14.PredictedSlaAttainmentPct.Should().BeGreaterThan(95.0);
    }

    [Fact]
    public void SimulateSurge_IncreasesVolumeAndGeneratesDeficits()
    {
        var planId = "cap_plan_global_support";
        var request = new SimulateSurgeRequest(planId, SurgePercentage: 50);

        var surged = _engine.SimulateSurge(request);

        surged.TotalDailyVolume.Should().BeGreaterThan(2000);
        surged.TotalDeficitHours.Should().BeGreaterThan(1);
    }

    [Fact]
    public void GetPlan_WithUnknownId_FallsBackToDefaultPlan()
    {
        var plan = _engine.GetPlan("unknown_plan_id");
        plan.Should().NotBeNull();
        plan.PlanId.Should().Be("cap_plan_global_support");
    }

    [Fact]
    public void RebalanceCapacity_WithZeroOrNegativeAdditionalAgents_ClampsToAtLeastOne()
    {
        var originalHour14 = _engine.GetPlan("cap_plan_global_support").HourlyIntervals.First(i => i.HourOfDay == 14);
        var rebalanced = _engine.RebalanceCapacity(new RebalanceCapacityRequest("cap_plan_global_support", 0));
        var rebalancedHour14 = rebalanced.HourlyIntervals.First(i => i.HourOfDay == 14);

        // Clamps Math.Max(1, request.AdditionalAgentsAllocated), so exactly 1 agent is added
        rebalancedHour14.ScheduledStaffHeadcount.Should().Be(originalHour14.ScheduledStaffHeadcount + 1);
    }

    [Fact]
    public void SimulateSurge_ZeroSurge_PreservesOriginalVolume()
    {
        var original = _engine.GetPlan("cap_plan_global_support");
        var simulated = _engine.SimulateSurge(new SimulateSurgeRequest("cap_plan_global_support", 0));

        simulated.TotalDailyVolume.Should().Be(original.TotalDailyVolume);
    }

    [Fact]
    public void GetPlan_IsCaseInsensitive_ReturnsTargetPlan()
    {
        var plan = _engine.GetPlan("CAP_PLAN_GLOBAL_SUPPORT");

        plan.Should().NotBeNull();
        plan.PlanId.Should().Be("cap_plan_global_support");
        plan.QueueName.Should().Be("Global Omnichannel Tier-1 Support");
    }

    [Fact]
    public void SimulateSurge_100PercentSurge_DoublesIncomingVolume()
    {
        var original = _engine.GetPlan("cap_plan_global_support");
        var surged = _engine.SimulateSurge(new SimulateSurgeRequest("cap_plan_global_support", 100));

        surged.TotalDailyVolume.Should().Be(original.TotalDailyVolume * 2);
    }
}
