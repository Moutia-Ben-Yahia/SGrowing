using SGrowing.Api.Modules.Finance.CapitalAllocation;
using Xunit;

namespace SGrowing.Tests.Finance;

public class CapitalAllocationTests
{
    [Fact]
    public void GetOverview_ReportsAllocationsYieldAndRunway()
    {
        var engine = new CapitalAllocationEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalPortfolioValueDollars > 20_000_000m);
        Assert.True(overview.WeightedAverageYieldPercentage > 4.0);
        Assert.True(overview.NetCashRunwayMonths > 20.0);
        Assert.True(overview.ActiveTreasuryPositionsCount >= 2);
        Assert.NotEmpty(overview.ActiveAllocations);
        Assert.NotEmpty(overview.RecentOptimizations);
    }

    [Fact]
    public void AllocateCapital_CreatesActiveYieldingPosition()
    {
        var engine = new CapitalAllocationEngine();
        var request = new AllocateCapitalRequest(
            "SOVEREIGN_TBILLS_USD",
            "USD",
            10_000_000m,
            5.25,
            180.0,
            "US Federal Reserve Direct"
        );

        var alloc = engine.AllocateCapital(request);

        Assert.NotNull(alloc);
        Assert.StartsWith("ALLOC-", alloc.AllocationId);
        Assert.Equal("SOVEREIGN_TBILLS_USD", alloc.AssetClass);
        Assert.Equal(10_000_000m, alloc.AllocatedAmount);
        Assert.Equal("ACTIVE_YIELDING", alloc.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveAllocations, a => a.AllocationId == alloc.AllocationId);
    }

    [Fact]
    public void OptimizeRunway_CalculatesProjectedRunwayAndInterestIncome()
    {
        var engine = new CapitalAllocationEngine();
        var request = new OptimizeRunwayRequest(
            900_000m,
            24.0m
        );

        var opt = engine.OptimizeRunway(request);

        Assert.NotNull(opt);
        Assert.StartsWith("OPT-", opt.OptimizationId);
        Assert.True(opt.RunwayMonths > 20.0);
        Assert.True(opt.ProjectedInterestIncomeAnnualDollars > 500_000m);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentOptimizations, o => o.OptimizationId == opt.OptimizationId);
    }
}
