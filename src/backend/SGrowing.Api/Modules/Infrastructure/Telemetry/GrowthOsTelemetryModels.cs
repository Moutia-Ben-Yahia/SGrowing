namespace SGrowing.Api.Modules.Infrastructure.Telemetry;

/// <summary>
/// Represents an active or triggered SRE alert within the GrowthOS platform.
/// </summary>
/// <param name="AlertId">Unique identifier or code for the alert.</param>
/// <param name="Severity">Alert severity level (e.g., INFO, WARNING, CRITICAL).</param>
/// <param name="ServiceComponent">Subsystem or mesh component triggering the alert.</param>
/// <param name="Message">Human-readable description of the incident condition.</param>
/// <param name="TriggeredAtUtc">Timestamp when the alert was triggered in UTC.</param>
public record SreAlertDto(
    string AlertId,
    string Severity,
    string ServiceComponent,
    string Message,
    DateTime TriggeredAtUtc
);

/// <summary>
/// SRE Health Radar status report summarizing SLO error budgets, latencies, and platform vitals.
/// </summary>
/// <param name="OverallHealthScore">System health score between 0.0 and 100.0.</param>
/// <param name="OverallStatus">Status designation: HEALTHY, DEGRADED, or CRITICAL.</param>
/// <param name="WebhookErrorBudgetRemainingPercent">SLO (99.9%) error budget remaining percentage.</param>
/// <param name="P99WebhookDeliveryLatencyMs">Estimated 99th percentile webhook delivery latency in milliseconds.</param>
/// <param name="ClickstreamEventsPerSec">Current ingestion rate of clickstream events per second.</param>
/// <param name="ClickstreamPartitionLag">Drain lag across event stream partitions.</param>
/// <param name="ActiveMissionsCount">Number of active autonomous AI agent missions running.</param>
/// <param name="DeadLetterQueueDepth">Current depth of dead-letter queues across transactional outboxes.</param>
/// <param name="ActiveAlerts">List of current active SRE alerts.</param>
/// <param name="TimestampUtc">UTC generation timestamp of the health radar report.</param>
public record SreHealthRadarReport(
    double OverallHealthScore,
    string OverallStatus,
    double WebhookErrorBudgetRemainingPercent,
    double P99WebhookDeliveryLatencyMs,
    long ClickstreamEventsPerSec,
    long ClickstreamPartitionLag,
    int ActiveMissionsCount,
    int DeadLetterQueueDepth,
    List<SreAlertDto> ActiveAlerts,
    DateTime TimestampUtc
);

/// <summary>
/// Distributed trace propagation headers adhering to the W3C Trace Context specification.
/// </summary>
/// <param name="TraceParent">W3C traceparent header value (format: 00-{traceId}-{spanId}-{traceFlags}).</param>
/// <param name="TraceState">Optional W3C tracestate header value for vendor-specific state.</param>
/// <param name="SpanId">Current span identifier as a 16-character hexadecimal string.</param>
/// <param name="TraceId">Trace identifier as a 32-character hexadecimal string.</param>
public record TraceContextPropagationHeaders(
    string TraceParent,
    string? TraceState,
    string SpanId,
    string TraceId
);

/// <summary>
/// Request payload to record synthetic metrics for validation and health probe testing.
/// </summary>
public record RecordSyntheticMetricRequest(
    string? MetricType = "webhook",
    bool IsSuccess = true,
    double DurationMs = 45.0,
    int EventCount = 1,
    string? AgentRole = "GrowthDirector",
    int TokenCount = 120
);
