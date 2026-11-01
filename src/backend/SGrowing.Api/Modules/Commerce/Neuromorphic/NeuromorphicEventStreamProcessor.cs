using System.Threading;

namespace SGrowing.Api.Modules.Commerce.Neuromorphic;

/// <summary>
/// High-throughput microsecond DVS event stream processor with spatio-temporal noise filtering,
/// dead/hot pixel masking, and temporal window aggregation.
/// </summary>
public sealed class NeuromorphicEventStreamProcessor : INeuromorphicEventStreamProcessor
{
    private const int SensorWidth = 256;
    private const int SensorHeight = 256;

    private readonly Lock _lock = new();
    private readonly HashSet<long> _deadPixels = [];
    private readonly long[,] _lastEventTimestamps = new long[SensorWidth, SensorHeight];

    private int _hotPixelThreshold = 500;
    private long _totalIngestedCount;
    private long _totalFilteredCount;
    private double _runningEventRatePerMs;
    private double _runningPolarBalanceRatio = 1.0;
    private int _totalAnomaliesDetected;
    private int _totalEStopsTriggered;

    public NeuromorphicEventStreamProcessor()
    {
        Reset();
    }

    /// <inheritdoc />
    public IReadOnlyList<DvsEvent> EnsureMonotonic(IReadOnlyList<DvsEvent> events)
    {
        if (events == null || events.Count <= 1)
        {
            return events ?? [];
        }

        bool isMonotonic = true;
        for (int i = 1; i < events.Count; i++)
        {
            if (events[i].TimestampMicroseconds < events[i - 1].TimestampMicroseconds)
            {
                isMonotonic = false;
                break;
            }
        }

        if (isMonotonic)
        {
            return events;
        }

        return [.. events.OrderBy(e => e.TimestampMicroseconds)];
    }

    /// <inheritdoc />
    public (IReadOnlyList<DvsEvent> ValidEvents, int FilteredCount) FilterNoise(
        IReadOnlyList<DvsEvent> events,
        int spatialRadius = 1,
        long temporalWindowUs = 5000)
    {
        if (events == null || events.Count == 0)
        {
            return ([], 0);
        }

        var sortedEvents = EnsureMonotonic(events);
        var validList = new List<DvsEvent>(sortedEvents.Count);
        int filteredCount = 0;

        // Count per-pixel occurrences in this batch to detect hot pixels
        var pixelBatchCounts = new Dictionary<long, int>();
        foreach (var evt in sortedEvents)
        {
            long key = EncodeCoord(evt.X, evt.Y);
            pixelBatchCounts[key] = pixelBatchCounts.GetValueOrDefault(key) + 1;
        }

        // Build index of coordinates for fast batch spatio-temporal lookup
        var coordToTimes = new Dictionary<long, List<long>>();
        foreach (var evt in sortedEvents)
        {
            long key = EncodeCoord(evt.X, evt.Y);
            if (!coordToTimes.TryGetValue(key, out var list))
            {
                list = [];
                coordToTimes[key] = list;
            }
            list.Add(evt.TimestampMicroseconds);
        }

        lock (_lock)
        {
            foreach (var evt in sortedEvents)
            {
                int x = evt.X;
                int y = evt.Y;
                long t = evt.TimestampMicroseconds;

                // 1. Dead pixel mask check
                if (IsDeadPixelInternal(x, y))
                {
                    filteredCount++;
                    continue;
                }

                // Bounds verification
                if (x < 0 || x >= SensorWidth || y < 0 || y >= SensorHeight)
                {
                    filteredCount++;
                    continue;
                }

                // 2. Hot pixel filtering check
                long currentCoordKey = EncodeCoord(x, y);
                if (pixelBatchCounts.TryGetValue(currentCoordKey, out int batchFreq) &&
                    batchFreq > _hotPixelThreshold)
                {
                    // Check if neighbors also have activity
                    bool hasNeighborActivity = false;
                    for (int dx = -spatialRadius; dx <= spatialRadius && !hasNeighborActivity; dx++)
                    {
                        for (int dy = -spatialRadius; dy <= spatialRadius; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < SensorWidth && ny >= 0 && ny < SensorHeight)
                            {
                                if (pixelBatchCounts.TryGetValue(EncodeCoord(nx, ny), out int nFreq) && nFreq > 10)
                                {
                                    hasNeighborActivity = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!hasNeighborActivity)
                    {
                        filteredCount++;
                        continue;
                    }
                }

                // 3. Spatio-temporal correlation (Background Activity Filter)
                bool hasSpatioTemporalNeighbor = false;

                for (int dx = -spatialRadius; dx <= spatialRadius && !hasSpatioTemporalNeighbor; dx++)
                {
                    for (int dy = -spatialRadius; dy <= spatialRadius; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx;
                        int ny = y + dy;

                        if (nx < 0 || nx >= SensorWidth || ny < 0 || ny >= SensorHeight)
                        {
                            continue;
                        }

                        // Check history from previous batches
                        long lastT = _lastEventTimestamps[nx, ny];
                        if (lastT > 0 && Math.Abs(t - lastT) <= temporalWindowUs)
                        {
                            hasSpatioTemporalNeighbor = true;
                            break;
                        }

                        // Check within the current batch
                        long nCoord = EncodeCoord(nx, ny);
                        if (coordToTimes.TryGetValue(nCoord, out var nTimes))
                        {
                            foreach (var nt in nTimes)
                            {
                                if (Math.Abs(t - nt) <= temporalWindowUs)
                                {
                                    hasSpatioTemporalNeighbor = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (hasSpatioTemporalNeighbor)
                {
                    _lastEventTimestamps[x, y] = t;
                    validList.Add(evt);
                }
                else
                {
                    filteredCount++;
                }
            }

            _totalIngestedCount += events.Count;
            _totalFilteredCount += filteredCount;
        }

        return (validList, filteredCount);
    }

    /// <inheritdoc />
    public (double EventRateEventsPerMs, double PolarBalanceRatio) CalculateMetrics(IReadOnlyList<DvsEvent> events)
    {
        if (events == null || events.Count == 0)
        {
            return (0.0, 1.0);
        }

        long minTime = long.MaxValue;
        long maxTime = long.MinValue;
        int onCount = 0;
        int offCount = 0;

        foreach (var evt in events)
        {
            if (evt.TimestampMicroseconds < minTime) minTime = evt.TimestampMicroseconds;
            if (evt.TimestampMicroseconds > maxTime) maxTime = evt.TimestampMicroseconds;

            if (evt.Polarity == EventPolarity.On)
                onCount++;
            else
                offCount++;
        }

        double spanMs = (maxTime >= minTime && minTime != long.MaxValue)
            ? Math.Max(1.0, (maxTime - minTime) / 1000.0)
            : 1.0;

        double rate = events.Count / spanMs;
        double balance = offCount > 0 ? (double)onCount / offCount : (onCount > 0 ? (double)onCount : 1.0);

        return (rate, balance);
    }

    /// <inheritdoc />
    public DvsEventIngestResponse IngestEvents(string streamId, string conveyorId, IReadOnlyList<DvsEvent> events)
    {
        if (events == null || events.Count == 0)
        {
            return new DvsEventIngestResponse(
                Success: true,
                IngestedCount: 0,
                FilteredCount: 0,
                EventRateEventsPerMs: 0.0,
                PolarBalanceRatio: 1.0,
                Message: "Empty event batch ingested.");
        }

        var (validEvents, filteredCount) = FilterNoise(events);
        var (rate, balance) = CalculateMetrics(validEvents);

        lock (_lock)
        {
            _runningEventRatePerMs = rate;
            _runningPolarBalanceRatio = balance;
        }

        string msg = $"Ingested {events.Count} raw events ({validEvents.Count} valid, {filteredCount} filtered). Rate: {rate:F1} ev/ms, Balance: {balance:F2}.";
        return new DvsEventIngestResponse(
            Success: true,
            IngestedCount: validEvents.Count,
            FilteredCount: filteredCount,
            EventRateEventsPerMs: rate,
            PolarBalanceRatio: balance,
            Message: msg);
    }

    /// <inheritdoc />
    public void MaskDeadPixel(int x, int y)
    {
        lock (_lock)
        {
            _deadPixels.Add(EncodeCoord(x, y));
        }
    }

    /// <inheritdoc />
    public void UnmaskDeadPixel(int x, int y)
    {
        lock (_lock)
        {
            _deadPixels.Remove(EncodeCoord(x, y));
        }
    }

    /// <inheritdoc />
    public bool IsDeadPixel(int x, int y)
    {
        lock (_lock)
        {
            return IsDeadPixelInternal(x, y);
        }
    }

    private bool IsDeadPixelInternal(int x, int y)
    {
        return _deadPixels.Contains(EncodeCoord(x, y));
    }

    /// <inheritdoc />
    public void SetHotPixelThreshold(int maxEventsPerPixelPerBatch)
    {
        lock (_lock)
        {
            _hotPixelThreshold = maxEventsPerPixelPerBatch;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            _deadPixels.Clear();
            Array.Clear(_lastEventTimestamps, 0, _lastEventTimestamps.Length);
            _totalIngestedCount = 0;
            _totalFilteredCount = 0;
            _runningEventRatePerMs = 0.0;
            _runningPolarBalanceRatio = 1.0;
            _totalAnomaliesDetected = 0;
            _totalEStopsTriggered = 0;
        }
    }

    /// <inheritdoc />
    public NeuromorphicMetricsResponse GetMetrics(string streamId = "default")
    {
        lock (_lock)
        {
            return new NeuromorphicMetricsResponse(
                StreamId: streamId,
                TotalEventsProcessed: _totalIngestedCount,
                CurrentRateEventsPerMs: _runningEventRatePerMs,
                PolarBalanceRatio: _runningPolarBalanceRatio,
                ActiveSpikeClusters: 0,
                TotalAnomaliesDetected: _totalAnomaliesDetected,
                TotalEStopsTriggered: _totalEStopsTriggered);
        }
    }

    public void RecordAnomalyStats(bool isEStop)
    {
        lock (_lock)
        {
            _totalAnomaliesDetected++;
            if (isEStop)
            {
                _totalEStopsTriggered++;
            }
        }
    }

    private static long EncodeCoord(int x, int y) => ((long)x << 32) | (uint)y;
}
