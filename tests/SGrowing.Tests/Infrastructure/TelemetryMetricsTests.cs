using System.Diagnostics.Metrics;
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
}
