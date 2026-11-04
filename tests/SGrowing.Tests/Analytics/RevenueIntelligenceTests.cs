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

    [Fact]
    public void ComputeSnapshot_NrrExceeds100_WhenExpansionOutweighsChurnAndContraction()
    {
        var snapshot = _engine.ComputeSnapshot("tenant_saas_metrics");

        // Starting: 125,000, New: 18,500, Exp: 8,200, Contraction: 2,100, Churn: 3,400
        // Net expansion = 8,200 - 2,100 - 3,400 = +2,700
        // NRR = (125,000 + 2,700) / 125,000 = 102.2%
        Assert.True(snapshot.NrrPercent > 100.0);
        Assert.Equal(102.2, snapshot.NrrPercent);
    }

    [Fact]
    public void ComputeSnapshot_GrrNeverExceeds100Percent()
    {
        var snapshot = _engine.ComputeSnapshot("tenant_saas_metrics");

        // GRR excludes expansion: (125,000 - 2,100 - 3,400) / 125,000 = 95.6%
        Assert.True(snapshot.GrrPercent <= 100.0);
        Assert.True(snapshot.GrrPercent >= 90.0);
        Assert.Equal(95.6, snapshot.GrrPercent);
    }

    [Fact]
    public void GetWaterfall_QuickRatio_EvaluatesAccurately()
    {
        var waterfall = _engine.GetWaterfall("tenant_saas_metrics");

        // Quick ratio = (New + Expansion) / (Contraction + Churn)
        // (18,500 + 8,200) / (2,100 + 3,400) = 26,700 / 5,500 = 4.85
        Assert.Equal(4.85, waterfall.QuickRatio);
        Assert.Equal(146200.0, waterfall.NetEndingMrr);
    }

    [Fact]
    public void GetCohortMatrix_AccountDecay_MonotonicallyDecreasesAcrossOffsets()
    {
        var cohorts = _engine.GetCohortMatrix("tenant_saas_metrics");
        var firstCohort = cohorts.Where(c => c.CohortMonth == "2026-05").OrderBy(c => c.MonthOffset).ToList();

        Assert.True(firstCohort.Count >= 5);
        for (int i = 1; i < firstCohort.Count; i++)
        {
            Assert.True(firstCohort[i].ActiveAccounts <= firstCohort[i - 1].ActiveAccounts,
                $"Expected ActiveAccounts at offset {i} to be <= offset {i-1}");
            Assert.True(firstCohort[i].RetentionRatePercent <= firstCohort[i - 1].RetentionRatePercent);
        }
    }

    [Fact]
    public void ComputeSnapshot_ArrEqualsTwelveTimesMrr_WithBenchmarkLtvCac()
    {
        var snapshot = _engine.ComputeSnapshot("tenant_arr_calc");

        Assert.Equal(146200.0, snapshot.Mrr);
        Assert.Equal(snapshot.Mrr * 12.0, snapshot.Arr);
        Assert.Equal(1754400.0, snapshot.Arr);
        Assert.Equal(4.8, snapshot.LtvCacRatio);
    }
}
