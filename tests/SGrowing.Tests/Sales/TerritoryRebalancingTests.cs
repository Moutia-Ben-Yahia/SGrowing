using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Sales.TerritoryRebalancing;
using Xunit;

namespace SGrowing.Tests.Sales;

public class TerritoryRebalancingTests
{
    private readonly TerritoryRebalancingEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsTerritoriesAndGiniMetric()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalAccounts.Should().BeGreaterThan(100);
        overview.TotalReps.Should().Be(4);
        overview.GiniWorkloadIndex.Should().Be(0.38);
        overview.Territories.Should().HaveCount(4);
        overview.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public void SimulateRebalance_GeneratesOptimizedEquilibrium()
    {
        var command = new SimulateRebalanceCommand("Equilibrium", 15);
        var result = _engine.SimulateRebalance(command);

        result.Should().NotBeNull();
        result.Strategy.Should().Be("Equilibrium");
        result.SimulatedGiniIndex.Should().BeLessThan(result.OriginalGiniIndex);
        result.AccountsReallocatedCount.Should().BeGreaterThan(0);
        result.ExpectedQuotaImprovementPct.Should().BeGreaterThan(10.0);
    }

    [Fact]
    public void ApplyRebalance_SchedulesPlanWithGracePeriod()
    {
        var command = new ApplyRebalanceCommand("SIM-TEST-001", 14, "CRO-VP-SALES");
        var result = _engine.ApplyRebalance(command);

        result.Should().NotBeNull();
        result.Status.Should().Be("Scheduled");
        result.AccountsShifted.Should().BeGreaterThan(0);
        result.EffectiveDate.Should().BeAfter(DateTime.UtcNow);
        result.AuditReceipt.Should().Contain("AUDIT-HASH");
    }

    [Fact]
    public void SimulateRebalance_GeoProximityStrategy_CalculatesProperGiniIndex()
    {
        var command = new SimulateRebalanceCommand("GeoProximity", 20);
        var result = _engine.SimulateRebalance(command);

        result.Should().NotBeNull();
        result.Strategy.Should().Be("GeoProximity");
        result.SimulatedGiniIndex.Should().Be(0.18);
        result.ExpectedQuotaImprovementPct.Should().Be(19.0); // (0.38 - 0.18) * 100 * 0.95 = 19.0
    }

    [Fact]
    public void SimulateRebalance_DefaultStrategy_CalculatesProperGiniIndex()
    {
        var command = new SimulateRebalanceCommand("CustomHybrid", 10);
        var result = _engine.SimulateRebalance(command);

        result.Should().NotBeNull();
        result.Strategy.Should().Be("CustomHybrid");
        result.SimulatedGiniIndex.Should().Be(0.15);
        result.ExpectedQuotaImprovementPct.Should().Be(21.8);
    }

    [Fact]
    public void ApplyRebalance_ZeroGracePeriod_DefaultsToFourteenDays()
    {
        var command = new ApplyRebalanceCommand("SIM-ZERO-001", 0, "OPS-ADMIN");
        var result = _engine.ApplyRebalance(command);

        result.Should().NotBeNull();
        result.EffectiveDate.Should().BeAfter(DateTime.UtcNow.AddDays(13));
        result.EffectiveDate.Should().BeBefore(DateTime.UtcNow.AddDays(15));
    }

    [Fact]
    public void GetOverview_ReturnsExpectedEfficiencyGain()
    {
        var overview = _engine.GetOverview();
        overview.ProjectedEfficiencyGainPct.Should().Be(24.6);
        overview.Recommendations.Should().HaveCount(3);
    }

    [Fact]
    public void ApplyRebalance_GeneratesRebalPlanPrefixAndAccountsShiftedMatchingRecommendations()
    {
        var command = new ApplyRebalanceCommand("SIM-CUSTOM-009", 21, "DIRECTOR_REV");
        var result = _engine.ApplyRebalance(command);

        result.RebalancePlanId.Should().StartWith("REBAL-PLAN-");
        result.AccountsShifted.Should().Be(3);
        result.Status.Should().Be("Scheduled");
    }
}
