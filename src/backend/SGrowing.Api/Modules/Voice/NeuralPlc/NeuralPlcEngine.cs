using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.NeuralPlc;

public interface INeuralPlcEngine
{
    NeuralPlcOverview GetOverview();
    PlcFrameReconstruction ReconstructLostFrame(string callSessionId, int sequenceNumber, PlcCodecType codec, int consecutiveLost);
    AudioJitterBufferMetric TuneJitterBuffer(string callSessionId, double packetLossPct, double jitterVarianceMs);
}

public sealed class NeuralPlcEngine : INeuralPlcEngine
{
    private readonly ConcurrentDictionary<string, PlcFrameReconstruction> _reconstructions = new();
    private readonly ConcurrentDictionary<string, AudioJitterBufferMetric> _metrics = new();

    public NeuralPlcEngine()
    {
        SeedInitialState();
    }

    private void SeedInitialState()
    {
        var recon1 = new PlcFrameReconstruction(
            "PLC-REC-001",
            "CALL-SIP-8910",
            412,
            PlcCodecType.Opus48kHz,
            1,
            WaveformSynthesisLatencyMs: 1.42,
            PredictedAudioMosScore: 4.45,
            NeuralExtrapolationSuccess: true
        );
        _reconstructions.TryAdd(recon1.ReconstructionId, recon1);

        var metric1 = new AudioJitterBufferMetric(
            "JIT-MET-001",
            "CALL-SIP-8910",
            CurrentBufferDepthMs: 20,
            TargetBufferDepthMs: 18,
            PacketLossRatePercentage: 4.2,
            JitterVarianceMs: 3.8,
            JitterBufferState.Optimal,
            DateTime.UtcNow.AddSeconds(-5)
        );
        _metrics.TryAdd(metric1.MetricId, metric1);
    }

    public NeuralPlcOverview GetOverview()
    {
        var recons = _reconstructions.Values.OrderByDescending(r => r.SequenceNumber).Take(10).ToList();
        var buffers = _metrics.Values.ToList();

        return new NeuralPlcOverview(
            ActiveStreamsCount: 68,
            AverageLossConcealedPct: 98.6,
            AverageWaveformMosScore: 4.41,
            CurrentBufferLatencyMs: 18.5,
            RecentReconstructions: recons,
            ActiveBufferMetrics: buffers
        );
    }

    public PlcFrameReconstruction ReconstructLostFrame(string callSessionId, int sequenceNumber, PlcCodecType codec, int consecutiveLost)
    {
        // Deep neural waveform synthesis degrades slightly with consecutive lost frames
        double baseMos = codec == PlcCodecType.Opus48kHz ? 4.50 : 4.10;
        double penalty = consecutiveLost * 0.15;
        double mosScore = Math.Max(3.2, Math.Round(baseMos - penalty, 2));

        var recon = new PlcFrameReconstruction(
            $"PLC-REC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            callSessionId,
            sequenceNumber,
            codec,
            consecutiveLost,
            WaveformSynthesisLatencyMs: 1.65,
            PredictedAudioMosScore: mosScore,
            NeuralExtrapolationSuccess: consecutiveLost <= 4
        );

        _reconstructions.TryAdd(recon.ReconstructionId, recon);
        return recon;
    }

    public AudioJitterBufferMetric TuneJitterBuffer(string callSessionId, double packetLossPct, double jitterVarianceMs)
    {
        int targetDepth;
        JitterBufferState state;

        if (jitterVarianceMs > 15.0 || packetLossPct > 10.0)
        {
            targetDepth = 40;
            state = JitterBufferState.AdaptingGrowth;
        }
        else if (jitterVarianceMs < 5.0 && packetLossPct < 2.0)
        {
            targetDepth = 15;
            state = JitterBufferState.AdaptingShrink;
        }
        else
        {
            targetDepth = 20;
            state = JitterBufferState.Optimal;
        }

        var metric = new AudioJitterBufferMetric(
            $"JIT-MET-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            callSessionId,
            CurrentBufferDepthMs: targetDepth + 2,
            TargetBufferDepthMs: targetDepth,
            PacketLossRatePercentage: packetLossPct,
            JitterVarianceMs: jitterVarianceMs,
            state,
            DateTime.UtcNow
        );

        _metrics.TryAdd(metric.MetricId, metric);
        return metric;
    }
}
