using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Analytics.BiReportBuilder;

public interface IBiReportEngine
{
    List<CustomReportDefinition> ListSavedReports();
    ReportExecutionResult ExecuteReport(RunReportRequest request);
    MonteCarloForecastResult RunMonteCarloForecast(MonteCarloSimRequest request);
}

public class BiReportEngine : IBiReportEngine
{
    private readonly ConcurrentDictionary<string, CustomReportDefinition> _reports = new();

    public BiReportEngine()
    {
        SeedDefaultReports();
    }

    private void SeedDefaultReports()
    {
        var rep1 = new CustomReportDefinition(
            ReportId: "rpt_arr_by_territory",
            Title: "Enterprise Pipeline ARR by Territory",
            Dataset: ReportDataset.Opportunities,
            Dimensions: new List<ReportDimension> { new("territory", "Sales Territory") },
            Metrics: new List<ReportMetric> { new("arr", "Contract ARR (USD)", MetricAggregationType.Sum) },
            ChartType: "BarChart",
            CreatedAtUtc: DateTime.UtcNow.AddDays(-10)
        );

        var rep2 = new CustomReportDefinition(
            ReportId: "rpt_ad_capi_roas",
            Title: "Server-Side CAPI ROAS by Ad Campaign",
            Dataset: ReportDataset.AdConversions,
            Dimensions: new List<ReportDimension> { new("campaignName", "Campaign Name") },
            Metrics: new List<ReportMetric> { new("roas", "Return on Ad Spend (x)", MetricAggregationType.Average) },
            ChartType: "Heatmap",
            CreatedAtUtc: DateTime.UtcNow.AddDays(-5)
        );

        _reports[rep1.ReportId] = rep1;
        _reports[rep2.ReportId] = rep2;
    }

    public List<CustomReportDefinition> ListSavedReports() =>
        _reports.Values.OrderByDescending(r => r.CreatedAtUtc).ToList();

    public ReportExecutionResult ExecuteReport(RunReportRequest request)
    {
        var rows = new List<ReportPivotRow>();

        if (request.Dataset == ReportDataset.Opportunities)
        {
            rows.Add(new ReportPivotRow("North America Enterprise", new Dictionary<string, decimal> { { request.MetricKey, 1450000m } }));
            rows.Add(new ReportPivotRow("EMEA Strategic", new Dictionary<string, decimal> { { request.MetricKey, 920000m } }));
            rows.Add(new ReportPivotRow("APAC & Global Tech", new Dictionary<string, decimal> { { request.MetricKey, 640000m } }));
        }
        else
        {
            rows.Add(new ReportPivotRow("Google Ads CAPI Search", new Dictionary<string, decimal> { { request.MetricKey, 4.35m } }));
            rows.Add(new ReportPivotRow("Meta Lookalike 1% Tier 1", new Dictionary<string, decimal> { { request.MetricKey, 3.80m } }));
            rows.Add(new ReportPivotRow("LinkedIn B2B ICP Surging", new Dictionary<string, decimal> { { request.MetricKey, 5.10m } }));
        }

        return new ReportExecutionResult(
            ReportId: "adhoc_" + Guid.NewGuid().ToString("N")[..8],
            Title: $"{request.Dataset} Aggregation by {request.DimensionKey}",
            Rows: rows,
            TotalRecordsProcessed: 1850,
            ExecutionLatencyMs: 3.2
        );
    }

    public MonteCarloForecastResult RunMonteCarloForecast(MonteCarloSimRequest request)
    {
        var runs = Math.Max(100, Math.Min(5000, request.SimulationRuns));
        var random = new Random(42); // deterministic seed for reproducibility
        var results = new List<decimal>(runs);

        var baseGrowthRate = (request.PipelineWinRatePercent / 100.0) - (request.ChurnRatePercent / 100.0);

        for (int i = 0; i < runs; i++)
        {
            // Box-Muller transform for normal distribution
            var u1 = 1.0 - random.NextDouble();
            var u2 = 1.0 - random.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            // 15% annual standard deviation volatility
            var simulatedGrowth = baseGrowthRate + (0.15 * randStdNormal);
            var landing = Math.Max(0m, request.BaselineArrUsd * (1.0m + (decimal)simulatedGrowth));
            results.Add(Math.Round(landing, 2));
        }

        results.Sort();
        var p10Index = (int)(runs * 0.10);
        var p50Index = (int)(runs * 0.50);
        var p90Index = (int)(runs * 0.90);

        var p10 = results[p10Index];
        var p50 = results[p50Index];
        var p90 = results[p90Index];

        var scenario = new MonteCarloArrScenario(
            ScenarioName: "Probabilistic 12-Month ARR Target",
            ProjectedArrUsd: p50,
            ProbabilityPercent: 50.0,
            P10Floor: p10,
            P50Expected: p50,
            P90Optimistic: p90
        );

        // Return a downsampled distribution (20 points) for charting
        var step = runs / 20;
        var samples = new List<decimal>();
        for (int i = 0; i < runs; i += step)
        {
            samples.Add(results[i]);
        }

        return new MonteCarloForecastResult(
            CurrentArrUsd: request.BaselineArrUsd,
            TotalSimulationRuns: runs,
            ExpectedScenario: scenario,
            SimulatedDistributionSamples: samples
        );
    }
}
