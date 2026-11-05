using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SGrowing.Api.Common.Observability;

public static class GrowthOsTelemetry
{
    public const string ServiceName = "SGrowing.Platform";
    public const string ServiceVersion = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);
}

/// <summary>
/// OpenTelemetry metrics instruments for SGrowing Platform observability.
/// Aligns with Prometheus/Grafana dashboards for Outbox, RAG, CAPI, WebRTC, and Offline Courier Sync.
/// </summary>
public static class GrowthOsMetrics
{
    public static readonly Meter Meter = new(GrowthOsTelemetry.ServiceName, GrowthOsTelemetry.ServiceVersion);

    public static readonly Histogram<double> OutboxDispatchLatency =
        Meter.CreateHistogram<double>("sgrowing.outbox.dispatch_latency_ms", "ms", "Latency of transactional outbox dispatch in milliseconds");

    public static readonly Counter<long> OutboxDlqCounter =
        Meter.CreateCounter<long>("sgrowing.outbox.dlq_count", "{message}", "Count of dead-lettered messages in transactional outbox");

    public static readonly Histogram<double> RagRetrievalLatency =
        Meter.CreateHistogram<double>("sgrowing.rag.retrieval_latency_ms", "ms", "Latency of hybrid RAG dense/sparse vector retrieval in milliseconds");

    public static readonly Histogram<double> CapiLatency =
        Meter.CreateHistogram<double>("sgrowing.capi.latency_ms", "ms", "Latency of Meta Conversions API (CAPI) dispatches in milliseconds");

    public static readonly Histogram<double> WebRtcJitter =
        Meter.CreateHistogram<double>("sgrowing.webrtc.jitter_ms", "ms", "Measured network jitter across WebRTC audio streams in milliseconds");

    public static readonly Counter<long> HitlApprovalsCounter =
        Meter.CreateCounter<long>("sgrowing.hitl.approvals_total", "{approval}", "Count of HITL approval decisions processed");

    public static readonly Counter<long> CourierOfflineSyncCounter =
        Meter.CreateCounter<long>("sgrowing.courier.offline_sync_total", "{delivery}", "Total count of field courier deliveries synced offline");

    public static void RecordOutboxDispatchLatency(double latencyMs, string topic)
    {
        OutboxDispatchLatency.Record(latencyMs, new KeyValuePair<string, object?>("topic", topic));
    }

    public static void IncrementOutboxDlq(long count = 1, string? reason = null)
    {
        OutboxDlqCounter.Add(count, new KeyValuePair<string, object?>("reason", reason ?? "max_retries_exceeded"));
    }

    public static void RecordRagRetrievalLatency(double latencyMs, int topK)
    {
        RagRetrievalLatency.Record(latencyMs, new KeyValuePair<string, object?>("top_k", topK));
    }

    public static void RecordCapiLatency(double latencyMs, string eventName, bool success)
    {
        CapiLatency.Record(latencyMs,
            new KeyValuePair<string, object?>("event_name", eventName),
            new KeyValuePair<string, object?>("success", success));
    }

    public static void RecordWebRtcJitter(double jitterMs, string sessionId)
    {
        WebRtcJitter.Record(jitterMs, new KeyValuePair<string, object?>("session_id", sessionId));
    }

    public static void IncrementHitlApproval(string operationType, string status)
    {
        HitlApprovalsCounter.Add(1,
            new KeyValuePair<string, object?>("operation_type", operationType),
            new KeyValuePair<string, object?>("status", status));
    }

    public static void IncrementCourierSync(int count, string wilayaCode)
    {
        CourierOfflineSyncCounter.Add(count, new KeyValuePair<string, object?>("wilaya_code", wilayaCode));
    }
}
