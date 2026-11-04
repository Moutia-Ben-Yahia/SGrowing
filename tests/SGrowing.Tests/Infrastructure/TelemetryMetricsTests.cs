using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using SGrowing.Core.Diagnostics;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class TelemetryMetricsTests
{
    [Fact]
    public void SGrowingTelemetry_ConstantsAndInstances_AreConfigured()
    {
        Assert.Equal("SGrowing.Platform", SGrowingTelemetry.ActivitySourceName);
        Assert.Equal("SGrowing.Platform.Metrics", SGrowingTelemetry.MeterName);
        Assert.NotNull(SGrowingTelemetry.ActivitySource);
        Assert.NotNull(SGrowingTelemetry.Meter);
    }

    [Fact]
    public void SGrowingTelemetry_Counters_AreInstantiated()
    {
        Assert.NotNull(SGrowingTelemetry.OrdersPlacedCounter);
        Assert.NotNull(SGrowingTelemetry.DeliveriesCompletedCounter);
        Assert.NotNull(SGrowingTelemetry.DlqRetriesCounter);
        Assert.NotNull(SGrowingTelemetry.ActiveCourierDriversCounter);
        Assert.NotNull(SGrowingTelemetry.WarehouseAllocationsCounter);
        Assert.NotNull(SGrowingTelemetry.WhatsAppDispatchesCounter);
        Assert.NotNull(SGrowingTelemetry.AutonomousFunnelsGeneratedCounter);
        Assert.NotNull(SGrowingTelemetry.AgentHeartbeatsExecutedCounter);
        Assert.NotNull(SGrowingTelemetry.MediaVaultUploadsCounter);
        Assert.NotNull(SGrowingTelemetry.AudioTranscriptionsCounter);
        Assert.NotNull(SGrowingTelemetry.InvoicesReconciledCounter);
        Assert.NotNull(SGrowingTelemetry.WorkflowsExecutedCounter);
        Assert.NotNull(SGrowingTelemetry.SlaBreachesEscalatedCounter);
        Assert.NotNull(SGrowingTelemetry.AgentRemediationsCounter);
        Assert.NotNull(SGrowingTelemetry.EventFanoutsDispatchedCounter);
        Assert.NotNull(SGrowingTelemetry.CartReservationsAcquiredCounter);
        Assert.NotNull(SGrowingTelemetry.CartReservationsExpiredCounter);
        Assert.NotNull(SGrowingTelemetry.WhatsAppNewsletterBroadcastsCounter);
        Assert.NotNull(SGrowingTelemetry.AiModelFallbacksTriggeredCounter);
        Assert.NotNull(SGrowingTelemetry.CallCenterCollisionLocksAcquiredCounter);
        Assert.NotNull(SGrowingTelemetry.CsatSurveysRecordedCounter);
        Assert.NotNull(SGrowingTelemetry.TenantSnapshotsDeployedCounter);
        Assert.NotNull(SGrowingTelemetry.WaterfallEnrichmentsExecutedCounter);
        Assert.NotNull(SGrowingTelemetry.AndroidSmsDispatchedCounter);
        Assert.NotNull(SGrowingTelemetry.VoiceAmdDetectionsCounter);
    }

    [Fact]
    public void MeterListener_CanRecordCounterValues()
    {
        long recordedValue = 0;
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == SGrowingTelemetry.MeterName &&
                instrument.Name == "sgrowing_orders_placed_total")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "sgrowing_orders_placed_total")
            {
                recordedValue += measurement;
            }
        });

        meterListener.Start();

        SGrowingTelemetry.OrdersPlacedCounter.Add(1);
        SGrowingTelemetry.OrdersPlacedCounter.Add(5);

        meterListener.RecordObservableInstruments();

        Assert.Equal(6, recordedValue);
    }

    [Fact]
    public void MeterListener_CanRecordUpDownCounter()
    {
        long recordedDriverDelta = 0;
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == SGrowingTelemetry.MeterName &&
                instrument.Name == "sgrowing_active_drivers_current")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "sgrowing_active_drivers_current")
            {
                recordedDriverDelta += measurement;
            }
        });

        meterListener.Start();

        SGrowingTelemetry.ActiveCourierDriversCounter.Add(3);
        SGrowingTelemetry.ActiveCourierDriversCounter.Add(-1);

        meterListener.RecordObservableInstruments();

        Assert.Equal(2, recordedDriverDelta);
    }

    [Fact]
    public void MeterListener_CanRecordDlqRetriesCounter_WithCustomTagKeyPairs()
    {
        long totalRetries = 0;
        string? capturedQueueTag = null;
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == SGrowingTelemetry.MeterName &&
                instrument.Name == "sgrowing_dlq_retries_total")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "sgrowing_dlq_retries_total")
            {
                totalRetries += measurement;
                foreach (var tag in tags)
                {
                    if (tag.Key == "queue_topic")
                    {
                        capturedQueueTag = tag.Value?.ToString();
                    }
                }
            }
        });

        meterListener.Start();

        SGrowingTelemetry.DlqRetriesCounter.Add(1, new KeyValuePair<string, object?>("queue_topic", "order.fulfillment.failed"));
        SGrowingTelemetry.DlqRetriesCounter.Add(2, new KeyValuePair<string, object?>("queue_topic", "order.fulfillment.failed"));

        meterListener.RecordObservableInstruments();

        totalRetries.Should().Be(3);
        capturedQueueTag.Should().Be("order.fulfillment.failed");
    }

    [Fact]
    public void MeterListener_CanRecordWorkflowsExecutedCounter_WithStatusAndTenant()
    {
        long workflowExecutions = 0;
        var statuses = new List<string>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == SGrowingTelemetry.MeterName &&
                instrument.Name == "sgrowing_workflows_executed_total")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "sgrowing_workflows_executed_total")
            {
                workflowExecutions += measurement;
                foreach (var tag in tags)
                {
                    if (tag.Key == "status" && tag.Value is string s)
                    {
                        statuses.Add(s);
                    }
                }
            }
        });

        meterListener.Start();

        SGrowingTelemetry.WorkflowsExecutedCounter.Add(1,
            new KeyValuePair<string, object?>("tenant_id", "tenant-alpha"),
            new KeyValuePair<string, object?>("status", "completed"));

        SGrowingTelemetry.WorkflowsExecutedCounter.Add(1,
            new KeyValuePair<string, object?>("tenant_id", "tenant-beta"),
            new KeyValuePair<string, object?>("status", "failed"));

        meterListener.RecordObservableInstruments();

        workflowExecutions.Should().Be(2);
        statuses.Should().Contain("completed");
        statuses.Should().Contain("failed");
    }

    [Fact]
    public void MeterListener_CanRecordCartReservationsLifecycle_AcquiredAndExpired()
    {
        long acquiredCount = 0;
        long expiredCount = 0;
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == SGrowingTelemetry.MeterName &&
                (instrument.Name == "sgrowing_cart_reservations_acquired_total" ||
                 instrument.Name == "sgrowing_cart_reservations_expired_total"))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "sgrowing_cart_reservations_acquired_total")
            {
                acquiredCount += measurement;
            }
            else if (instrument.Name == "sgrowing_cart_reservations_expired_total")
            {
                expiredCount += measurement;
            }
        });

        meterListener.Start();

        SGrowingTelemetry.CartReservationsAcquiredCounter.Add(5);
        SGrowingTelemetry.CartReservationsExpiredCounter.Add(2);

        meterListener.RecordObservableInstruments();

        acquiredCount.Should().Be(5);
        expiredCount.Should().Be(2);
    }

    [Fact]
    public void ActivitySource_CanCreateAndTraceCustomActivitySpan()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SGrowingTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using (var activity = SGrowingTelemetry.ActivitySource.StartActivity("CustomPipelineOperation"))
        {
            activity.Should().NotBeNull();
            activity?.SetTag("operation.type", "lead_scoring");
            activity?.SetTag("tenant.id", "tenant-104");
            activity?.SetStatus(ActivityStatusCode.Ok, "Processed successfully");

            activity?.GetTagItem("operation.type").Should().Be("lead_scoring");
            activity?.GetTagItem("tenant.id").Should().Be("tenant-104");
            activity?.Status.Should().Be(ActivityStatusCode.Ok);
        }
    }
}
