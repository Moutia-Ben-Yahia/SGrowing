using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using SGrowing.Api.Common.Observability;
using Xunit;

namespace SGrowing.Tests.Unit;

public class GrowthOsTelemetryTests
{
    [Fact]
    public void GrowthOsTelemetry_ActivitySource_HasCorrectPlatformIdentity()
    {
        GrowthOsTelemetry.ServiceName.Should().Be("SGrowing.Platform");
        GrowthOsTelemetry.ServiceVersion.Should().Be("1.0.0");
        GrowthOsTelemetry.ActivitySource.Name.Should().Be("SGrowing.Platform");
        GrowthOsTelemetry.ActivitySource.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void GrowthOsTelemetry_CanCreateAndTraceActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == GrowthOsTelemetry.ServiceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = GrowthOsTelemetry.ActivitySource.StartActivity("TelemetryTestOperation");
        activity.Should().NotBeNull();
        activity!.SetTag("tenant.id", Guid.NewGuid().ToString());
        activity.SetTag("engine", "RAG");

        activity.Tags.Should().Contain(t => t.Key == "engine" && t.Value == "RAG");
    }

    [Fact]
    public void GrowthOsMetrics_InstrumentsPublishCorrectMeasurements()
    {
        var recordedValues = new List<(string InstrumentName, double Value)>();

        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == GrowthOsTelemetry.ServiceName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            recordedValues.Add((instrument.Name, measurement));
        });

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            recordedValues.Add((instrument.Name, (double)measurement));
        });

        meterListener.Start();

        // Exercise all instruments
        GrowthOsMetrics.RecordOutboxDispatchLatency(42.5, "commerce.orders");
        GrowthOsMetrics.IncrementOutboxDlq(3, "max_retries_exceeded");
        GrowthOsMetrics.RecordRagRetrievalLatency(18.2, 10);
        GrowthOsMetrics.RecordCapiLatency(135.0, "Purchase", true);
        GrowthOsMetrics.RecordWebRtcJitter(12.4, "session_voice_44");
        GrowthOsMetrics.IncrementHitlApproval("AdSpendAdjustment", "Approved");
        GrowthOsMetrics.IncrementCourierSync(7, "16");

        recordedValues.Should().Contain(m => m.InstrumentName == "sgrowing.outbox.dispatch_latency_ms" && Math.Abs(m.Value - 42.5) < 0.01);
        recordedValues.Should().Contain(m => m.InstrumentName == "sgrowing.outbox.dlq_count" && Math.Abs(m.Value - 3) < 0.01);
        recordedValues.Should().Contain(m => m.InstrumentName == "sgrowing.rag.retrieval_latency_ms" && Math.Abs(m.Value - 18.2) < 0.01);
        recordedValues.Should().Contain(m => m.InstrumentName == "sgrowing.capi.latency_ms" && Math.Abs(m.Value - 135.0) < 0.01);
        recordedValues.Should().Contain(m => m.InstrumentName == "sgrowing.webrtc.jitter_ms" && Math.Abs(m.Value - 12.4) < 0.01);
        recordedValues.Should().Contain(m => m.InstrumentName == "sgrowing.hitl.approvals_total" && Math.Abs(m.Value - 1) < 0.01);
        recordedValues.Should().Contain(m => m.InstrumentName == "sgrowing.courier.offline_sync_total" && Math.Abs(m.Value - 7) < 0.01);
    }
}
