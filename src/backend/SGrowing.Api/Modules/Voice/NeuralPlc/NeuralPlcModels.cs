namespace SGrowing.Api.Modules.Voice.NeuralPlc;

public enum PlcCodecType
{
    Opus48kHz,
    G722Wideband,
    G711Narrowband
}

public enum JitterBufferState
{
    Optimal,
    AdaptingGrowth,
    AdaptingShrink,
    EmergencyFlush
}

public sealed record PlcFrameReconstruction(
    string ReconstructionId,
    string CallSessionId,
    int SequenceNumber,
    PlcCodecType Codec,
    int LostConsecutiveFramesCount,
    double WaveformSynthesisLatencyMs,
    double PredictedAudioMosScore,
    bool NeuralExtrapolationSuccess
);

public sealed record AudioJitterBufferMetric(
    string MetricId,
    string CallSessionId,
    int CurrentBufferDepthMs,
    int TargetBufferDepthMs,
    double PacketLossRatePercentage,
    double JitterVarianceMs,
    JitterBufferState BufferState,
    DateTime SampledAtUtc
);

public sealed record NeuralPlcProfile(
    string ProfileId,
    PlcCodecType Codec,
    int FrameDurationMs,
    double MaxPacketLossTolerancePct,
    double MosFidelityBaseline
);

public sealed record NeuralPlcOverview(
    int ActiveStreamsCount,
    double AverageLossConcealedPct,
    double AverageWaveformMosScore,
    double CurrentBufferLatencyMs,
    IReadOnlyList<PlcFrameReconstruction> RecentReconstructions,
    IReadOnlyList<AudioJitterBufferMetric> ActiveBufferMetrics
);
