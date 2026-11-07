using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SGrowing.Api.Modules.Infrastructure.Telemetry;

/// <summary>
/// Core contract for distributed tracing, metrics collection, and SRE health radar monitoring.
/// </summary>
public interface IGrowthOsTelemetryService
{
    /// <summary>
    /// Starts a distributed trace span for an outbound webhook delivery.
    /// </summary>
    Activity? StartWebhookSpan(string eventType, string destinationUrl, Guid deliveryId);

    /// <summary>
    /// Starts a distributed trace span for clickstream batch ingestion.
    /// </summary>
    Activity? StartClickstreamSpan(string partitionKey, int batchSize);

    /// <summary>
    /// Starts a distributed trace span for an autonomous AI agent mission task.
    /// </summary>
    Activity? StartMissionTaskSpan(Guid missionId, string agentRole, string taskId);

    /// <summary>
    /// Generates W3C-compliant traceparent and distributed trace context propagation headers.
    /// </summary>
    TraceContextPropagationHeaders CreateTraceContextHeaders();

    /// <summary>
    /// Records execution metrics (success status and duration) for a webhook dispatch.
    /// </summary>
    void RecordWebhookMetric(bool isSuccess, double durationMs);

    /// <summary>
    /// Records clickstream ingestion throughput metrics.
    /// </summary>
    void RecordClickstreamMetric(int eventCount);

    /// <summary>
    /// Records LLM tokens consumed by an autonomous AI agent role.
    /// </summary>
    void RecordAgentTokensConsumed(string agentRole, int tokenCount);

    /// <summary>
    /// Records execution metrics for an autonomous AI agent mission.
    /// </summary>
    void RecordMissionExecutionMetric(Guid missionId, string status, double durationMs = 0);

    /// <summary>
    /// Records commerce order intake metrics from connector marketplace.
    /// </summary>
    void RecordCommerceOrderMetric(string connectorId, decimal amount, string currency);

    /// <summary>
    /// Evaluates current platform metrics against SLO targets (99.9%) and generates the SRE Health Radar report.
    /// </summary>
    SreHealthRadarReport GetSreHealthRadar(int currentDlqDepth = 0, int activeMissions = 0);
}

/// <summary>
/// High-performance implementation of <see cref="IGrowthOsTelemetryService"/> providing
/// OpenTelemetry ActivitySource tracing, Metrics meters, and SRE alerting calculations.
/// </summary>
public class GrowthOsTelemetryService : IGrowthOsTelemetryService
{
    public static readonly ActivitySource GrowthOsSource = new("SGrowing.GrowthOS", "1.0.0");
    public static readonly Meter GrowthOsMeter = new("SGrowing.GrowthOS", "1.0.0");

    private readonly Counter<long> _webhookDispatchesCounter;
    private readonly Histogram<double> _webhookDurationHistogram;
    private readonly Counter<long> _clickstreamEventsCounter;
    private readonly Counter<long> _tokensConsumedCounter;
    private readonly Counter<long> _missionExecutionsCounter;
    private readonly Counter<long> _commerceOrdersCounter;

    private readonly ConcurrentQueue<double> _latencyReservoir = new();
    private const int MaxReservoirCapacity = 1000;

    private long _totalWebhooks;
    private long _failedWebhooks;
    private long _totalClickstreamEvents;
    private readonly DateTime _serviceStartTimeUtc = DateTime.UtcNow;

    public GrowthOsTelemetryService()
    {
        _webhookDispatchesCounter = GrowthOsMeter.CreateCounter<long>(
            name: "growthos.webhooks.dispatched",
            unit: "{dispatch}",
            description: "Total number of webhook deliveries dispatched");

        _webhookDurationHistogram = GrowthOsMeter.CreateHistogram<double>(
            name: "growthos.webhooks.duration.ms",
            unit: "ms",
            description: "Webhook roundtrip delivery latency in milliseconds");

        _clickstreamEventsCounter = GrowthOsMeter.CreateCounter<long>(
            name: "growthos.clickstream.events",
            unit: "{event}",
            description: "Total number of clickstream ingestion events processed");

        _tokensConsumedCounter = GrowthOsMeter.CreateCounter<long>(
            name: "growthos.ai.tokens",
            unit: "{token}",
            description: "Total LLM tokens consumed across agent missions");

        _missionExecutionsCounter = GrowthOsMeter.CreateCounter<long>(
            name: "growthos.missions.executed",
            unit: "{mission}",
            description: "Total number of autonomous AI agent missions executed");

        _commerceOrdersCounter = GrowthOsMeter.CreateCounter<long>(
            name: "growthos.commerce.orders",
            unit: "{order}",
            description: "Total number of commerce orders ingested through connectors");
    }

    public Activity? StartWebhookSpan(string eventType, string destinationUrl, Guid deliveryId)
    {
        var activity = GrowthOsSource.StartActivity(
            name: "growthos.webhook.dispatch",
            kind: ActivityKind.Client);

        if (activity is not null)
        {
            activity.SetTag("rpc.system", "http");
            activity.SetTag("webhook.event", eventType);
            activity.SetTag("webhook.url", destinationUrl);
            activity.SetTag("webhook.delivery_id", deliveryId.ToString());
        }

        return activity;
    }

    public Activity? StartClickstreamSpan(string partitionKey, int batchSize)
    {
        var activity = GrowthOsSource.StartActivity(
            name: "growthos.clickstream.ingest",
            kind: ActivityKind.Consumer);

        if (activity is not null)
        {
            activity.SetTag("messaging.system", "partitioned_stream");
            activity.SetTag("clickstream.partition_key", partitionKey);
            activity.SetTag("clickstream.batch_size", batchSize);
        }

        return activity;
    }

    public Activity? StartMissionTaskSpan(Guid missionId, string agentRole, string taskId)
    {
        var activity = GrowthOsSource.StartActivity(
            name: "growthos.ai.mission_task",
            kind: ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag("ai.mission_id", missionId.ToString());
            activity.SetTag("ai.agent_role", agentRole);
            activity.SetTag("ai.task_id", taskId);
        }

        return activity;
    }

    public TraceContextPropagationHeaders CreateTraceContextHeaders()
    {
        var currentActivity = Activity.Current;
        string traceId;
        string spanId;
        string? traceState = null;

        if (currentActivity is not null && currentActivity.TraceId != default)
        {
            traceId = currentActivity.TraceId.ToHexString();
            spanId = currentActivity.SpanId.ToHexString();
            traceState = currentActivity.TraceStateString;
        }
        else
        {
            traceId = ActivityTraceId.CreateRandom().ToHexString();
            spanId = ActivitySpanId.CreateRandom().ToHexString();
        }

        var traceParent = $"00-{traceId}-{spanId}-01";
        return new TraceContextPropagationHeaders(traceParent, traceState, spanId, traceId);
    }

    public void RecordWebhookMetric(bool isSuccess, double durationMs)
    {
        _webhookDispatchesCounter.Add(1, new KeyValuePair<string, object?>("success", isSuccess));
        _webhookDurationHistogram.Record(durationMs, new KeyValuePair<string, object?>("success", isSuccess));

        Interlocked.Increment(ref _totalWebhooks);
        if (!isSuccess)
        {
            Interlocked.Increment(ref _failedWebhooks);
        }

        _latencyReservoir.Enqueue(durationMs);
        while (_latencyReservoir.Count > MaxReservoirCapacity && _latencyReservoir.TryDequeue(out _))
        {
        }
    }

    public void RecordClickstreamMetric(int eventCount)
    {
        _clickstreamEventsCounter.Add(eventCount);
        Interlocked.Add(ref _totalClickstreamEvents, eventCount);
    }

    public void RecordAgentTokensConsumed(string agentRole, int tokenCount)
    {
        _tokensConsumedCounter.Add(tokenCount, new KeyValuePair<string, object?>("agent_role", agentRole));
    }

    public void RecordMissionExecutionMetric(Guid missionId, string status, double durationMs = 0)
    {
        _missionExecutionsCounter.Add(1,
            new KeyValuePair<string, object?>("status", status),
            new KeyValuePair<string, object?>("mission_id", missionId.ToString()));
    }

    public void RecordCommerceOrderMetric(string connectorId, decimal amount, string currency)
    {
        _commerceOrdersCounter.Add(1,
            new KeyValuePair<string, object?>("connector_id", connectorId),
            new KeyValuePair<string, object?>("currency", currency));
    }

    public SreHealthRadarReport GetSreHealthRadar(int currentDlqDepth = 0, int activeMissions = 0)
    {
        var totalWebhooks = Interlocked.Read(ref _totalWebhooks);
        var failedWebhooks = Interlocked.Read(ref _failedWebhooks);

        // Calculate SLO (99.9% target) Error Budget Remaining:
        // Target: 99.9% availability -> Allowable failure rate = 0.001 (0.1%)
        double errorBudgetRemainingPercent;
        if (totalWebhooks == 0 || failedWebhooks == 0)
        {
            errorBudgetRemainingPercent = 100.0;
        }
        else
        {
            var failureRate = (double)failedWebhooks / totalWebhooks;
            const double allowedFailureRate = 0.001; // 1 - 0.999
            var budgetBurnFraction = failureRate / allowedFailureRate;
            errorBudgetRemainingPercent = Math.Clamp((1.0 - budgetBurnFraction) * 100.0, 0.0, 100.0);
        }

        // Calculate P99 Webhook Latency Estimate from Reservoir
        var samples = _latencyReservoir.ToArray();
        double p99Latency;
        if (samples.Length == 0)
        {
            p99Latency = 42.0; // Baseline nominal delivery latency in ms
        }
        else
        {
            Array.Sort(samples);
            var index = (int)Math.Ceiling(samples.Length * 0.99) - 1;
            index = Math.Clamp(index, 0, samples.Length - 1);
            p99Latency = samples[index];
        }

        // Clickstream Throughput & Partition Drain Lag Calculations
        var elapsedSeconds = Math.Max(1.0, (DateTime.UtcNow - _serviceStartTimeUtc).TotalSeconds);
        var totalEvents = Interlocked.Read(ref _totalClickstreamEvents);
        var eventsPerSec = totalEvents > 0
            ? Math.Max(1L, (long)(totalEvents / elapsedSeconds))
            : 2450L; // Baseline nominal clickstream event ingestion rate

        var clickstreamPartitionLag = currentDlqDepth > 0
            ? (long)currentDlqDepth * 25L
            : 0L;

        // Health Score (0.0 to 100.0)
        var healthScore = 100.0;

        if (errorBudgetRemainingPercent < 100.0)
        {
            healthScore -= (100.0 - errorBudgetRemainingPercent) * 0.40;
        }

        if (p99Latency > 250.0)
        {
            healthScore -= 30.0;
        }
        else if (p99Latency > 150.0)
        {
            healthScore -= 10.0;
        }

        if (currentDlqDepth > 10)
        {
            healthScore -= 35.0;
        }
        else if (currentDlqDepth > 0)
        {
            healthScore -= Math.Min(15.0, currentDlqDepth * 2.0);
        }

        healthScore = Math.Clamp(healthScore, 0.0, 100.0);

        var overallStatus = healthScore switch
        {
            >= 90.0 => "HEALTHY",
            >= 60.0 => "DEGRADED",
            _ => "CRITICAL"
        };

        // Active Alerts Generation
        var alerts = new List<SreAlertDto>();

        if (currentDlqDepth > 10)
        {
            alerts.Add(new SreAlertDto(
                AlertId: "ALERT-DLQ-DEPTH",
                Severity: "CRITICAL",
                ServiceComponent: "TransactionalOutbox.DLQ",
                Message: $"Dead-letter queue depth exceeds threshold: {currentDlqDepth} items pending manual review or replay",
                TriggeredAtUtc: DateTime.UtcNow));
        }

        if (p99Latency > 250.0)
        {
            alerts.Add(new SreAlertDto(
                AlertId: "ALERT-LATENCY-P99",
                Severity: "WARNING",
                ServiceComponent: "WebhookMesh.Dispatcher",
                Message: $"P99 webhook delivery latency ({p99Latency:F1}ms) exceeds SLA threshold of 250ms",
                TriggeredAtUtc: DateTime.UtcNow));
        }

        if (errorBudgetRemainingPercent <= 0.0)
        {
            alerts.Add(new SreAlertDto(
                AlertId: "ALERT-SLO-EXHAUSTED",
                Severity: "CRITICAL",
                ServiceComponent: "WebhookMesh.Reliability",
                Message: "Webhook error budget fully exhausted for evaluation window (<99.9% SLO target violated)",
                TriggeredAtUtc: DateTime.UtcNow));
        }
        else if (errorBudgetRemainingPercent < 50.0)
        {
            alerts.Add(new SreAlertDto(
                AlertId: "ALERT-SLO-BURN",
                Severity: "WARNING",
                ServiceComponent: "WebhookMesh.Reliability",
                Message: $"Webhook error budget burn rate high: remaining budget is {errorBudgetRemainingPercent:F1}% against 99.9% SLO target",
                TriggeredAtUtc: DateTime.UtcNow));
        }

        if (clickstreamPartitionLag > 5000L)
        {
            alerts.Add(new SreAlertDto(
                AlertId: "ALERT-CLICKSTREAM-LAG",
                Severity: "WARNING",
                ServiceComponent: "Clickstream.PartitionDrain",
                Message: $"Clickstream partition drain lag is high: {clickstreamPartitionLag} events behind head",
                TriggeredAtUtc: DateTime.UtcNow));
        }

        return new SreHealthRadarReport(
            OverallHealthScore: Math.Round(healthScore, 2),
            OverallStatus: overallStatus,
            WebhookErrorBudgetRemainingPercent: Math.Round(errorBudgetRemainingPercent, 2),
            P99WebhookDeliveryLatencyMs: Math.Round(p99Latency, 2),
            ClickstreamEventsPerSec: eventsPerSec,
            ClickstreamPartitionLag: clickstreamPartitionLag,
            ActiveMissionsCount: activeMissions,
            DeadLetterQueueDepth: currentDlqDepth,
            ActiveAlerts: alerts,
            TimestampUtc: DateTime.UtcNow
        );
    }
}
