using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.AudioNoiseCancellation;

public enum NoiseType
{
    None = 0,
    AmbientHvac = 1,
    KeyboardTyping = 2,
    StreetTraffic = 3,
    BackgroundChatter = 4
}

public record AudioChannelTelemetry(
    string ChannelId,
    string RepName,
    string CallSessionId,
    double NoiseFloorDb,
    double SnrDb,                  // Signal-to-Noise Ratio (dB)
    double MeanOpinionScoreMos,    // 1.0 to 5.0
    NoiseType DetectedNoise,
    bool IsFilterActive,
    int DspLatencyMs
);

public record NoiseCancellationOverview(
    int ActiveStreamChannels,
    double AverageSnrGainDb,
    double AverageMosScore,
    int ProcessedPacketsPerSecond,
    List<AudioChannelTelemetry> Channels
);

public record ToggleNoiseFilterCommand(
    string ChannelId,
    bool EnableFilter,
    double TargetSuppressionDb // e.g. 18.0 - 28.0 dB
);

public record NoiseFilterResult(
    string ChannelId,
    bool IsActive,
    double AppliedSuppressionDb,
    double NewSnrDb,
    double NewMosScore,
    string DspLog
);
