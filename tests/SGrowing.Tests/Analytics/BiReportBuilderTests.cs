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

    [Fact]
    public void ExecuteReport_AdConversionsDataset_AggregatesCampaignMetrics()
    {
        var result = _engine.ExecuteReport(new RunReportRequest(
            Dataset: ReportDataset.AdConversions,
            DimensionKey: "campaignName",
            MetricKey: "roas",
            Aggregation: MetricAggregationType.Average
        ));

        Assert.NotNull(result);
        Assert.Equal(3, result.Rows.Count);
        Assert.Contains(result.Rows, r => r.DimensionValue == "Google Ads CAPI Search");
        Assert.Contains(result.Rows, r => r.DimensionValue == "LinkedIn B2B ICP Surging");
        Assert.True(result.Rows[0].MetricValues["roas"] > 0);
        Assert.True(result.TotalRecordsProcessed > 0);
    }

    [Theory]
    [InlineData(10, 100)]      // Below min (100) -> clamped to 100
    [InlineData(8000, 5000)]   // Above max (5000) -> clamped to 5000
    [InlineData(500, 500)]     // Within range -> preserved
    public void RunMonteCarloForecast_ClampsSimulationRunsToValidRange(int requestedRuns, int expectedRuns)
    {
        var result = _engine.RunMonteCarloForecast(new MonteCarloSimRequest(
            BaselineArrUsd: 5_000_000m,
            PipelineWinRatePercent: 25.0,
            ChurnRatePercent: 4.0,
            SimulationRuns: requestedRuns
        ));

        Assert.Equal(expectedRuns, result.TotalSimulationRuns);
    }

    [Fact]
    public void RunMonteCarloForecast_ZeroBaseline_ProducesZeroDistribution()
    {
        var result = _engine.RunMonteCarloForecast(new MonteCarloSimRequest(
            BaselineArrUsd: 0m,
            PipelineWinRatePercent: 20.0,
            ChurnRatePercent: 5.0,
            SimulationRuns: 200
        ));

        Assert.Equal(0m, result.ExpectedScenario.P10Floor);
        Assert.Equal(0m, result.ExpectedScenario.P50Expected);
        Assert.Equal(0m, result.ExpectedScenario.P90Optimistic);
        Assert.All(result.SimulatedDistributionSamples, sample => Assert.Equal(0m, sample));
    }

    [Fact]
    public void ExecuteReport_TitleContainsDatasetAndDimensionMetadata()
    {
        var result = _engine.ExecuteReport(new RunReportRequest(
            Dataset: ReportDataset.Opportunities,
            DimensionKey: "sales_rep",
            MetricKey: "pipeline_value",
            Aggregation: MetricAggregationType.Sum
        ));

        Assert.StartsWith("adhoc_", result.ReportId);
        Assert.Contains("Opportunities", result.Title);
        Assert.Contains("sales_rep", result.Title);
        Assert.True(result.ExecutionLatencyMs > 0);
    }
}
