namespace SGrowing.Api.Modules.Analytics.BiReportBuilder;

public enum ReportDataset
{
    Contacts,
    Opportunities,
    AdConversions,
    BillingInvoices,
    VoiceCalls
}

public enum MetricAggregationType
{
    Sum,
    Average,
    Count,
    Min,
    Max
}

public record ReportDimension(
    string FieldKey,
    string DisplayLabel
);

public record ReportMetric(
    string MetricKey,
    string DisplayLabel,
    MetricAggregationType Aggregation
);

public record CustomReportDefinition(
    string ReportId,
    string Title,
    ReportDataset Dataset,
    List<ReportDimension> Dimensions,
    List<ReportMetric> Metrics,
    string ChartType,
    DateTime CreatedAtUtc
);

public record ReportPivotRow(
    string DimensionValue,
    Dictionary<string, decimal> MetricValues
);

public record ReportExecutionResult(
    string ReportId,
    string Title,
    List<ReportPivotRow> Rows,
    int TotalRecordsProcessed,
    double ExecutionLatencyMs
);

public record MonteCarloArrScenario(
    string ScenarioName,
    decimal ProjectedArrUsd,
    double ProbabilityPercent,
    decimal P10Floor,
    decimal P50Expected,
    decimal P90Optimistic
);

public record MonteCarloForecastResult(
    decimal CurrentArrUsd,
    int TotalSimulationRuns,
    MonteCarloArrScenario ExpectedScenario,
    List<decimal> SimulatedDistributionSamples
);

public record RunReportRequest(
    ReportDataset Dataset,
    string DimensionKey,
    string MetricKey,
    MetricAggregationType Aggregation
);

public record MonteCarloSimRequest(
    decimal BaselineArrUsd,
    double PipelineWinRatePercent = 28.5,
    double ChurnRatePercent = 4.2,
    int SimulationRuns = 1000
);
