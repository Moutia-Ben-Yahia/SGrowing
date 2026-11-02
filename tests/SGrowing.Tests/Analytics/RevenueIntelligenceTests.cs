using SGrowing.Api.Modules.Analytics.RevenueIntelligence;

namespace SGrowing.Tests.Analytics;

public class RevenueIntelligenceTests
{
    private readonly RevenueIntelligenceEngine _engine = new();

    [Fact]
    public void ComputeSnapshot_CalculatesArrAndMrrCorrectly()
    {
        var snapshot = _engine.ComputeSnapshot("tenant_enterprise_rev");

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Arr > 1_000_000);
        Assert.True(snapshot.Mrr > 100_000);
        Assert.True(snapshot.NrrPercent >= 100.0); // Expansion outweighs churn
        Assert.True(snapshot.GrrPercent > 90.0);
        Assert.True(snapshot.LtvCacRatio > 3.0);
        Assert.NotEmpty(snapshot.CohortMatrix);
    }

    [Fact]
    public void GetWaterfall_ReturnsCorrectMathBalance()
    {
        var w = _engine.GetWaterfall("tenant_enterprise_rev");

        var expectedNet = w.StartingMrr + w.NewMrr + w.ExpansionMrr - w.ContractionMrr - w.ChurnedMrr;
        Assert.Equal(expectedNet, w.NetEndingMrr);
        Assert.True(w.QuickRatio > 1.5); // Healthy SaaS benchmark > 1.5
    }

    [Fact]
    public void GetCohortMatrix_GeneratesTriangularCohortDecay()
    {
        var cohorts = _engine.GetCohortMatrix("tenant_enterprise_rev");

        Assert.NotEmpty(cohorts);
        // MonthOffset = 0 should always have 100% account retention
        var monthZeros = cohorts.Where(c => c.MonthOffset == 0).ToList();
        Assert.All(monthZeros, c => Assert.Equal(100.0, c.RetentionRatePercent));
    }
}
