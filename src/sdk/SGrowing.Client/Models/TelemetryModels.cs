using System.Text.Json.Serialization;

namespace SGrowing.Client.Models;

/// <summary>
/// Active SRE incident alert.
/// </summary>
public record SreAlertDto(
    [property: JsonPropertyName("alertId")] string AlertId,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("serviceComponent")] string ServiceComponent,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("triggeredAtUtc")] DateTime TriggeredAtUtc
);

/// <summary>
/// SRE Health Radar status report summarizing SLO error budgets, latencies, and platform vitals.
/// </summary>
public record SreHealthRadarReport(
    [property: JsonPropertyName("overallHealthScore")] double OverallHealthScore,
    [property: JsonPropertyName("overallStatus")] string OverallStatus,
    [property: JsonPropertyName("webhookErrorBudgetRemainingPercent")] double WebhookErrorBudgetRemainingPercent,
    [property: JsonPropertyName("p99WebhookDeliveryLatencyMs")] double P99WebhookDeliveryLatencyMs,
    [property: JsonPropertyName("clickstreamEventsPerSec")] long ClickstreamEventsPerSec,
    [property: JsonPropertyName("clickstreamPartitionLag")] long ClickstreamPartitionLag,
    [property: JsonPropertyName("activeMissionsCount")] int ActiveMissionsCount,
    [property: JsonPropertyName("deadLetterQueueDepth")] int DeadLetterQueueDepth,
    [property: JsonPropertyName("activeAlerts")] List<SreAlertDto> ActiveAlerts,
    [property: JsonPropertyName("timestampUtc")] DateTime TimestampUtc
);

/// <summary>
/// Distributed trace propagation headers adhering to the W3C Trace Context specification.
/// </summary>
public record TraceContextPropagationHeaders(
    [property: JsonPropertyName("traceParent")] string TraceParent,
    [property: JsonPropertyName("traceState")] string? TraceState,
    [property: JsonPropertyName("spanId")] string SpanId,
    [property: JsonPropertyName("traceId")] string TraceId
);

/// <summary>
/// Request payload to record synthetic metrics for validation and health probe testing.
/// </summary>
public record RecordSyntheticMetricRequest(
    [property: JsonPropertyName("metricType")] string? MetricType = "webhook",
    [property: JsonPropertyName("isSuccess")] bool IsSuccess = true,
    [property: JsonPropertyName("durationMs")] double DurationMs = 45.0,
    [property: JsonPropertyName("eventCount")] int EventCount = 1,
    [property: JsonPropertyName("agentRole")] string? AgentRole = "GrowthDirector",
    [property: JsonPropertyName("tokenCount")] int TokenCount = 120
);
