using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SGrowing.Core.Diagnostics;

public static class SGrowingTelemetry
{
    public const string ActivitySourceName = "SGrowing.Platform";
    public const string MeterName = "SGrowing.Platform.Metrics";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    // Standard Metrics
    public static readonly Counter<long> OrdersPlacedCounter = Meter.CreateCounter<long>(
        "sgrowing_orders_placed_total",
        "orders",
        "Total number of checkout orders placed");

    public static readonly Counter<long> DeliveriesCompletedCounter = Meter.CreateCounter<long>(
        "sgrowing_deliveries_completed_total",
        "deliveries",
        "Total number of physical doorstep deliveries successfully completed");

    public static readonly Counter<long> DlqRetriesCounter = Meter.CreateCounter<long>(
        "sgrowing_dlq_retries_total",
        "retries",
        "Total dead-letter queue exponential backoff retry attempts");

    public static readonly UpDownCounter<long> ActiveCourierDriversCounter = Meter.CreateUpDownCounter<long>(
        "sgrowing_active_drivers_current",
        "drivers",
        "Current number of in-house courier drivers actively on route");
}
