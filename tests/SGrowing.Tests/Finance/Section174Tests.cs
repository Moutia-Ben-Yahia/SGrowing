using SGrowing.Api.Modules.Finance.Section174;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class Section174Tests
{
    private readonly Section174Engine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededSchedulesAndAllocations()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalActiveSchedulesCount >= 2);
        Assert.True(overview.TotalDomesticCapitalizedUsd > 10000000.0);
        Assert.NotEmpty(overview.ActiveSchedules);
        Assert.NotEmpty(overview.RecentAllocations);
    }

    [Fact]
    public void CreateSchedule_Domestic5Year_Applies10PercentMidYearConvention()
    {
        var sched = _engine.CreateSchedule(2026, JurisdictionResearchType.UnitedStatesDomestic5Year, 10000000.0);

        Assert.NotNull(sched);
        Assert.StartsWith("SCHED-174-2026-", sched.ScheduleId);
        Assert.Equal(1000000.0, sched.Year1DeductionUsd); // 10% year 1
        Assert.Equal(2000000.0, sched.AnnualSubsequentDeductionUsd); // 20% year 2-5
        Assert.Equal(210000.0, sched.CumulativeTaxShieldGeneratedUsd); // 21% rate
    }

    [Fact]
    public void AllocateLabor_EngineeringSprint_CalculatesQualifyingAmount()
    {
        var alloc = _engine.AllocateLabor("SPRINT-2026-05", "Core AI Swarm", 500000.0, 0.90, JurisdictionResearchType.UnitedStatesDomestic5Year);

        Assert.NotNull(alloc);
        Assert.StartsWith("ALLOC-LABOR-", alloc.AllocationId);
        Assert.Equal(450000.0, alloc.QualifyingCapitalizedAmountUsd);
    }
}
