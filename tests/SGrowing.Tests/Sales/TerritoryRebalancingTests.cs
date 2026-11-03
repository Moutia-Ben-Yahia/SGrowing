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
}
