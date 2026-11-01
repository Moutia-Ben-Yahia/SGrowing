using System.Diagnostics;

namespace SGrowing.Api.Modules.Infrastructure.EdgeObservability;

/// <summary>
/// Contract for the eBPF kernel-level socket mesh observability engine.
/// Emulates kernel tracepoints and kprobes, analyzes TCP RTT variance, SYN retransmits,
/// zero-window throttles, and detects micro-burst saturation and rogue socket leaks.
/// </summary>
public interface IEbpfKernelSocketObservabilityEngine
{
    /// <summary>Ingests a single eBPF socket metric captured from kernel probes.</summary>
    void IngestMetric(EbpfSocketMetric metric);

    /// <summary>Ingests a batch of eBPF socket metrics.</summary>
    void IngestMetricsBatch(IEnumerable<EbpfSocketMetric> metrics);

    /// <summary>Retrieves recent socket metrics up to the specified limit.</summary>
    IReadOnlyList<EbpfSocketMetric> GetRecentMetrics(int limit = 100);

    /// <summary>Calculates a summary of kernel socket health across all tracked sockets.</summary>
    EbpfMetricsResponse GetMetricsSummary();

    /// <summary>Detects micro-burst network saturation across correlated sockets with sub-millisecond overhead.</summary>
    IReadOnlyList<string> DetectMicroBurstSaturation(double rttVarianceThresholdMs = 45.0, int minimumCorrelatedSockets = 3);

    /// <summary>Detects rogue socket leaks (e.g. CLOSE_WAIT accumulation, zero-byte hanging connections).</summary>
    IReadOnlyList<string> DetectRogueSocketLeaks();

    /// <summary>Calculates mean, standard deviation (variance), and maximum TCP RTT.</summary>
    (double MeanRtt, double StdDevRtt, double MaxRtt) CalculateRttStatistics();

    /// <summary>Clears recorded metrics.</summary>
    void Clear();
}

/// <summary>
/// eBPF kernel mesh observability engine providing microsecond-level TCP socket analysis.
/// </summary>
public class EbpfKernelSocketObservabilityEngine : IEbpfKernelSocketObservabilityEngine
{
    private readonly Lock _lock = new();
    private readonly List<EbpfSocketMetric> _metricsBuffer = [];
    private const int MaxBufferSize = 10_000;

    /// <summary>eBPF probe tracing outgoing TCP connection initiation.</summary>
    public const string ProbeTcpV4Connect = "kprobe/tcp_v4_connect";

    /// <summary>eBPF return probe measuring TCP send message latency and RTT.</summary>
    public const string ProbeTcpSendMsg = "kretprobe/tcp_sendmsg";

    /// <summary>eBPF tracepoint capturing TCP state transitions and zero-window probes.</summary>
    public const string ProbeInetSockSetState = "tracepoint/sock/inet_sock_set_state";

    /// <summary>TCP RTT jitter variance threshold in milliseconds.</summary>
    public const double RttJitterVarianceThresholdMs = 45.0;

    /// <summary>SYN retransmit threshold triggering burst classification.</summary>
    public const long SynRetransmitThreshold = 3;

    public void IngestMetric(EbpfSocketMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);

        var classifiedMetric = ClassifyMetric(metric);

        lock (_lock)
        {
            if (_metricsBuffer.Count >= MaxBufferSize)
            {
                _metricsBuffer.RemoveRange(0, 1_000); // Bounded memory ring buffer eviction
            }
            _metricsBuffer.Add(classifiedMetric);
        }
    }

    public void IngestMetricsBatch(IEnumerable<EbpfSocketMetric> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var classifiedList = metrics.Select(ClassifyMetric).ToList();

        lock (_lock)
        {
            var overflow = (_metricsBuffer.Count + classifiedList.Count) - MaxBufferSize;
            if (overflow > 0)
            {
                var removeCount = Math.Min(_metricsBuffer.Count, Math.Max(overflow, 1_000));
                _metricsBuffer.RemoveRange(0, removeCount);
            }
            _metricsBuffer.AddRange(classifiedList);
        }
    }

    public IReadOnlyList<EbpfSocketMetric> GetRecentMetrics(int limit = 100)
    {
        lock (_lock)
        {
            var count = Math.Min(limit, _metricsBuffer.Count);
            return _metricsBuffer
                .TakeLast(count)
                .Reverse()
                .ToList();
        }
    }

    public EbpfMetricsResponse GetMetricsSummary()
    {
        lock (_lock)
        {
            var total = _metricsBuffer.Count;
            var highJitter = _metricsBuffer.Count(m => m.Status == SocketHealthStatus.HighRttJitter);
            var synBurst = _metricsBuffer.Count(m => m.Status == SocketHealthStatus.SynRetransmitBurst);
            var zeroWindow = _metricsBuffer.Count(m => m.Status == SocketHealthStatus.ZeroWindowThrottled);

            var rogueLeaks = DetectRogueSocketLeaksInternal();
            var saturationAlerts = DetectMicroBurstSaturationInternal(RttJitterVarianceThresholdMs, 3);
            var recent = _metricsBuffer.TakeLast(50).Reverse().ToList();

            return new EbpfMetricsResponse(
                TotalSocketsTracked: total,
                SocketsInHighRttJitter: highJitter,
                SocketsInSynRetransmitBurst: synBurst,
                SocketsInZeroWindowThrottled: zeroWindow,
                RogueSocketLeaksDetected: rogueLeaks.Count,
                Sockets: recent,
                MicroBurstSaturationAlerts: saturationAlerts);
        }
    }

    public IReadOnlyList<string> DetectMicroBurstSaturation(double rttVarianceThresholdMs = 45.0, int minimumCorrelatedSockets = 3)
    {
        lock (_lock)
        {
            return DetectMicroBurstSaturationInternal(rttVarianceThresholdMs, minimumCorrelatedSockets);
        }
    }

    public IReadOnlyList<string> DetectRogueSocketLeaks()
    {
        lock (_lock)
        {
            return DetectRogueSocketLeaksInternal();
        }
    }

    public (double MeanRtt, double StdDevRtt, double MaxRtt) CalculateRttStatistics()
    {
        lock (_lock)
        {
            if (_metricsBuffer.Count == 0)
            {
                return (0.0, 0.0, 0.0);
            }

            var count = _metricsBuffer.Count;
            double sum = 0.0;
            double max = 0.0;

            for (int i = 0; i < count; i++)
            {
                var rtt = _metricsBuffer[i].SmoothedRttMs;
                sum += rtt;
                if (rtt > max)
                {
                    max = rtt;
                }
            }

            var mean = sum / count;

            double sumSquaredDiffs = 0.0;
            for (int i = 0; i < count; i++)
            {
                var diff = _metricsBuffer[i].SmoothedRttMs - mean;
                sumSquaredDiffs += diff * diff;
            }

            var stdDev = Math.Sqrt(sumSquaredDiffs / count);
            return (mean, stdDev, max);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _metricsBuffer.Clear();
        }
    }

    /// <summary>
    /// Analyzes socket attributes and classifies health state.
    /// </summary>
    private static EbpfSocketMetric ClassifyMetric(EbpfSocketMetric metric)
    {
        var status = metric.Status;

        if (metric.RttVarianceMs > RttJitterVarianceThresholdMs)
        {
            status = SocketHealthStatus.HighRttJitter;
        }
        else if (metric.SynRetransmitCount >= SynRetransmitThreshold)
        {
            status = SocketHealthStatus.SynRetransmitBurst;
        }
        else if (metric.ZeroWindowProbeCount > 0)
        {
            status = SocketHealthStatus.ZeroWindowThrottled;
        }
        else if (status == SocketHealthStatus.Normal || status == default)
        {
            status = SocketHealthStatus.Normal;
        }

        return metric with { Status = status };
    }

    private List<string> DetectMicroBurstSaturationInternal(double rttVarianceThresholdMs, int minimumCorrelatedSockets)
    {
        var alerts = new List<string>();

        if (_metricsBuffer.Count < minimumCorrelatedSockets)
        {
            return alerts;
        }

        // Group recent metrics into 5-second windows to detect simultaneous variance spikes
        var recentWindow = _metricsBuffer.TakeLast(200).ToList();

        var correlatedSpikes = recentWindow
            .Where(m => m.RttVarianceMs > rttVarianceThresholdMs || m.Status == SocketHealthStatus.HighRttJitter)
            .GroupBy(m => m.DestinationAddress)
            .Where(g => g.Count() >= minimumCorrelatedSockets)
            .ToList();

        foreach (var group in correlatedSpikes)
        {
            alerts.Add(
                $"Micro-burst network saturation detected: {group.Count()} sockets to destination '{group.Key}' " +
                $"exhibited correlated RTT variance > {rttVarianceThresholdMs}ms (Peak Variance: {group.Max(g => g.RttVarianceMs):F1}ms).");
        }

        return alerts;
    }

    private List<string> DetectRogueSocketLeaksInternal()
    {
        var alerts = new List<string>();

        // Check for CLOSE_WAIT sockets or ESTABLISHED sockets with 0 bytes transferred
        var rogueCandidates = _metricsBuffer
            .TakeLast(500)
            .Where(m => string.Equals(m.TcpState, "CLOSE_WAIT", StringComparison.OrdinalIgnoreCase)
                     || (string.Equals(m.TcpState, "ESTABLISHED", StringComparison.OrdinalIgnoreCase) && m.BytesSent == 0 && m.BytesReceived == 0))
            .GroupBy(m => $"{m.SourceAddress}:{m.SourcePort}->{m.DestinationAddress}:{m.DestinationPort}")
            .ToList();

        foreach (var group in rogueCandidates)
        {
            var sample = group.First();
            if (string.Equals(sample.TcpState, "CLOSE_WAIT", StringComparison.OrdinalIgnoreCase))
            {
                alerts.Add($"Rogue socket leak detected: Socket {sample.SocketId} ({sample.SourceAddress}:{sample.SourcePort} -> {sample.DestinationAddress}:{sample.DestinationPort}) hanging in CLOSE_WAIT.");
            }
            else
            {
                alerts.Add($"Rogue socket leak detected: Socket {sample.SocketId} ({sample.SourceAddress}:{sample.SourcePort} -> {sample.DestinationAddress}:{sample.DestinationPort}) ESTABLISHED with 0 bytes transferred.");
            }
        }

        return alerts;
    }
}
