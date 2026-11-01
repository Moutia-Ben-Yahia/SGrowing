namespace SGrowing.Api.Modules.Commerce.Neuromorphic;

/// <summary>
/// Interface for high-throughput microsecond DVS event stream processor.
/// </summary>
public interface INeuromorphicEventStreamProcessor
{
    /// <summary>
    /// Ensures microsecond timestamp monotonic ordering on incoming event streams.
    /// </summary>
    IReadOnlyList<DvsEvent> EnsureMonotonic(IReadOnlyList<DvsEvent> events);

    /// <summary>
    /// Filters spatio-temporal thermal noise, dead pixels, and hot pixels.
    /// </summary>
    (IReadOnlyList<DvsEvent> ValidEvents, int FilteredCount) FilterNoise(
        IReadOnlyList<DvsEvent> events,
        int spatialRadius = 1,
        long temporalWindowUs = 5000);

    /// <summary>
    /// Calculates aggregate event rate (events/ms) and polar balance ratio (On/Off).
    /// </summary>
    (double EventRateEventsPerMs, double PolarBalanceRatio) CalculateMetrics(IReadOnlyList<DvsEvent> events);

    /// <summary>
    /// Ingests a batch of events, applies noise filtering, updates running metrics, and returns ingestion statistics.
    /// </summary>
    DvsEventIngestResponse IngestEvents(string streamId, string conveyorId, IReadOnlyList<DvsEvent> events);

    /// <summary>
    /// Masks a pixel as dead/defective.
    /// </summary>
    void MaskDeadPixel(int x, int y);

    /// <summary>
    /// Unmasks a previously masked pixel.
    /// </summary>
    void UnmaskDeadPixel(int x, int y);

    /// <summary>
    /// Checks whether a pixel is currently masked as dead.
    /// </summary>
    bool IsDeadPixel(int x, int y);

    /// <summary>
    /// Sets the maximum allowable events per pixel per batch before being flagged as a hot pixel.
    /// </summary>
    void SetHotPixelThreshold(int maxEventsPerPixelPerBatch);

    /// <summary>
    /// Resets all internal buffers, running metrics, and masks.
    /// </summary>
    void Reset();

    /// <summary>
    /// Retrieves current processing metrics.
    /// </summary>
    NeuromorphicMetricsResponse GetMetrics(string streamId = "default");
}
