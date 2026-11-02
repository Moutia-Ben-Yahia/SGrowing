using SGrowing.Api.Modules.Analytics.BiReportBuilder;

namespace SGrowing.Tests.Analytics;

public class BiReportBuilderTests
{
    private readonly IBiReportEngine _engine = new BiReportEngine();

    [Fact]
    public void ListSavedReports_ReturnsPreconfiguredReports()
    {
        var reports = _engine.ListSavedReports();

        Assert.NotEmpty(reports);
        Assert.Contains(reports, r => r.ReportId == "rpt_arr_by_territory");
        Assert.Contains(reports, r => r.ReportId == "rpt_ad_capi_roas");
        Assert.NotEmpty(reports[0].Dimensions);
        Assert.NotEmpty(reports[0].Metrics);
    }

    [Fact]
    public void ExecuteReport_ComputesGroupedAggregations()
    {
        var result = _engine.ExecuteReport(new RunReportRequest(
            Dataset: ReportDataset.Opportunities,
            DimensionKey: "territory",
            MetricKey: "arr",
            Aggregation: MetricAggregationType.Sum
        ));

        Assert.NotNull(result);
        Assert.Equal(3, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.DimensionValue == "North America Enterprise");
        Assert.True(result.Rows[0].MetricValues["arr"] > 0);
    }

    [Fact]
    public void RunMonteCarloForecast_GeneratesProbabilisticDistributionCones()
    {
        var result = _engine.RunMonteCarloForecast(new MonteCarloSimRequest(
            BaselineArrUsd: 10000000.00m,
            PipelineWinRatePercent: 30.0,
            ChurnRatePercent: 5.0,
            SimulationRuns: 1000
        ));

        Assert.NotNull(result);
        Assert.Equal(1000, result.TotalSimulationRuns);
        Assert.True(result.ExpectedScenario.P10Floor < result.ExpectedScenario.P50Expected);
        Assert.True(result.ExpectedScenario.P50Expected < result.ExpectedScenario.P90Optimistic);
        Assert.NotEmpty(result.SimulatedDistributionSamples);
    }
}
