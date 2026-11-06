using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.Telemetry;
using Xunit;

namespace SGrowing.Tests.Unit;

/// <summary>
/// Unit tests for GrowthOS OpenTelemetry distributed tracing and SRE health radar alerting mesh.
/// </summary>
public class GrowthOsOpenTelemetryTests
{
    [Fact]
    public void StartWebhookSpan_CreatesValidActivity_AndTagsParameters()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == GrowthOsTelemetryService.GrowthOsSource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var service = new GrowthOsTelemetryService();
        var deliveryId = Guid.NewGuid();
        const string eventType = "order.created";
        const string destinationUrl = "https://api.partner.com/v1/webhook";

        using var activity = service.StartWebhookSpan(eventType, destinationUrl, deliveryId);

        activity.Should().NotBeNull();
        activity!.Kind.Should().Be(ActivityKind.Client);
        activity.GetTagItem("rpc.system").Should().Be("http");
        activity.GetTagItem("webhook.event").Should().Be(eventType);
        activity.GetTagItem("webhook.url").Should().Be(destinationUrl);
        activity.GetTagItem("webhook.delivery_id").Should().Be(deliveryId.ToString());

        // Validate companion clickstream and mission task spans
        using var clickstreamSpan = service.StartClickstreamSpan("partition-7", 125);
        clickstreamSpan.Should().NotBeNull();
        clickstreamSpan!.GetTagItem("messaging.system").Should().Be("partitioned_stream");
        clickstreamSpan.GetTagItem("clickstream.partition_key").Should().Be("partition-7");
        clickstreamSpan.GetTagItem("clickstream.batch_size").Should().Be(125);

        var missionId = Guid.NewGuid();
        using var missionSpan = service.StartMissionTaskSpan(missionId, "GrowthDirector", "task-campaign-12");
        missionSpan.Should().NotBeNull();
        missionSpan!.GetTagItem("ai.mission_id").Should().Be(missionId.ToString());
        missionSpan.GetTagItem("ai.agent_role").Should().Be("GrowthDirector");
        missionSpan.GetTagItem("ai.task_id").Should().Be("task-campaign-12");
    }

    [Fact]
    public void CreateTraceContextHeaders_FormatsStandardW3CTraceparent()
    {
        var service = new GrowthOsTelemetryService();

        // 1. Without ambient activity: creates random valid W3C identifiers
        var headers = service.CreateTraceContextHeaders();
        headers.Should().NotBeNull();
        headers.TraceId.Should().HaveLength(32);
        headers.SpanId.Should().HaveLength(16);
        headers.TraceParent.Should().MatchRegex(@"^00-[0-9a-f]{32}-[0-9a-f]{16}-01$");
        headers.TraceParent.Should().Be($"00-{headers.TraceId}-{headers.SpanId}-01");

        // 2. With ambient activity: propagates active traceId and spanId
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == GrowthOsTelemetryService.GrowthOsSource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = GrowthOsTelemetryService.GrowthOsSource.StartActivity("W3CTraceContextPropagationSpan");
        activity.Should().NotBeNull();

        var ambientHeaders = service.CreateTraceContextHeaders();
        ambientHeaders.TraceId.Should().Be(activity!.TraceId.ToHexString());
        ambientHeaders.SpanId.Should().Be(activity.SpanId.ToHexString());
        ambientHeaders.TraceParent.Should().Be($"00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-01");
    }

    [Fact]
    public void GetSreHealthRadar_CalculatesErrorBudgetAndGeneratesAlertsWhenDegraded()
    {
        var service = new GrowthOsTelemetryService();

        // Baseline healthy state
        var healthyReport = service.GetSreHealthRadar(currentDlqDepth: 0, activeMissions: 3);
        healthyReport.OverallStatus.Should().Be("HEALTHY");
        healthyReport.OverallHealthScore.Should().BeGreaterThanOrEqualTo(90.0);
        healthyReport.WebhookErrorBudgetRemainingPercent.Should().Be(100.0);
        healthyReport.DeadLetterQueueDepth.Should().Be(0);
        healthyReport.ActiveMissionsCount.Should().Be(3);
        healthyReport.ActiveAlerts.Should().BeEmpty();

        // Degrade system: Record successful webhooks + failed webhooks exceeding 0.1% allowable failure rate
        for (int i = 0; i < 900; i++)
        {
            service.RecordWebhookMetric(isSuccess: true, durationMs: 42.0);
        }
        for (int i = 0; i < 30; i++)
        {
            service.RecordWebhookMetric(isSuccess: false, durationMs: 310.0);
        }

        // Evaluate under degraded conditions with DLQ depth > 10
        var degradedReport = service.GetSreHealthRadar(currentDlqDepth: 18, activeMissions: 7);

        degradedReport.DeadLetterQueueDepth.Should().Be(18);
        degradedReport.ActiveMissionsCount.Should().Be(7);
        degradedReport.P99WebhookDeliveryLatencyMs.Should().BeGreaterThan(250.0);
        degradedReport.WebhookErrorBudgetRemainingPercent.Should().BeLessThan(50.0);
        degradedReport.OverallStatus.Should().BeOneOf("DEGRADED", "CRITICAL");
        degradedReport.OverallHealthScore.Should().BeLessThan(90.0);

        // Verify active alerts
        degradedReport.ActiveAlerts.Should().Contain(a => a.AlertId == "ALERT-DLQ-DEPTH" && a.Severity == "CRITICAL");
        degradedReport.ActiveAlerts.Should().Contain(a => a.AlertId == "ALERT-LATENCY-P99" && a.Severity == "WARNING");
        degradedReport.ActiveAlerts.Should().Contain(a => a.AlertId.StartsWith("ALERT-SLO-"));
    }

    [Fact]
    public void RecordMetrics_UpdatesCountersAndHistogramsWithoutExceptions()
    {
        var recordedMeasurements = new List<(string Name, double Value)>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == GrowthOsTelemetryService.GrowthOsMeter.Name)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            recordedMeasurements.Add((instrument.Name, measurement));
        });

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedMeasurements.Add((instrument.Name, (double)measurement));
        });

        meterListener.Start();

        var service = new GrowthOsTelemetryService();

        var act = () =>
        {
            service.RecordWebhookMetric(isSuccess: true, durationMs: 48.0);
            service.RecordWebhookMetric(isSuccess: false, durationMs: 140.0);
            service.RecordClickstreamMetric(250);
            service.RecordAgentTokensConsumed("GrowthDirector", 1200);
            service.RecordAgentTokensConsumed("AdPacingAgent", 450);
        };

        act.Should().NotThrow();

        recordedMeasurements.Should().Contain(m => m.Name == "growthos.webhooks.dispatched" && m.Value == 1);
        recordedMeasurements.Should().Contain(m => m.Name == "growthos.webhooks.duration.ms" && Math.Abs(m.Value - 48.0) < 0.01);
        recordedMeasurements.Should().Contain(m => m.Name == "growthos.clickstream.events" && m.Value == 250);
        recordedMeasurements.Should().Contain(m => m.Name == "growthos.ai.tokens" && m.Value == 1200);
        recordedMeasurements.Should().Contain(m => m.Name == "growthos.ai.tokens" && m.Value == 450);
    }
}
