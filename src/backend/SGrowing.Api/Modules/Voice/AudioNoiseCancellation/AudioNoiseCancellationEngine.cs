using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.AudioNoiseCancellation;

public interface IAudioNoiseCancellationEngine
{
    NoiseCancellationOverview GetOverview();
    NoiseFilterResult ToggleFilter(ToggleNoiseFilterCommand command);
}

public class AudioNoiseCancellationEngine : IAudioNoiseCancellationEngine
{
    private readonly ConcurrentDictionary<string, AudioChannelTelemetry> _channels = new();

    public AudioNoiseCancellationEngine()
    {
        SeedChannels();
    }

    private void SeedChannels()
    {
        var c1 = new AudioChannelTelemetry(
            ChannelId: "VOX-DSP-01",
            RepName: "Victoria Sterling",
            CallSessionId: "CALL-SES-781",
            NoiseFloorDb: -32.5,
            SnrDb: 18.2,
            MeanOpinionScoreMos: 4.6,
            DetectedNoise: NoiseType.KeyboardTyping,
            IsFilterActive: true,
            DspLatencyMs: 8
        );

        var c2 = new AudioChannelTelemetry(
            ChannelId: "VOX-DSP-02",
            RepName: "Marcus Vance",
            CallSessionId: "CALL-SES-782",
            NoiseFloorDb: -26.0,
            SnrDb: 12.4,
            MeanOpinionScoreMos: 3.8,
            DetectedNoise: NoiseType.StreetTraffic,
            IsFilterActive: false, // Needs filter!
            DspLatencyMs: 6
        );

        var c3 = new AudioChannelTelemetry(
            ChannelId: "VOX-DSP-03",
            RepName: "Dr. Evelyn Reed",
            CallSessionId: "CALL-SES-783",
            NoiseFloorDb: -42.0,
            SnrDb: 24.8,
            MeanOpinionScoreMos: 4.85,
            DetectedNoise: NoiseType.AmbientHvac,
            IsFilterActive: true,
            DspLatencyMs: 9
        );

        _channels[c1.ChannelId] = c1;
        _channels[c2.ChannelId] = c2;
        _channels[c3.ChannelId] = c3;
    }

    public NoiseCancellationOverview GetOverview()
    {
        var list = _channels.Values.ToList();
        var avgMos = list.Count > 0 ? list.Average(c => c.MeanOpinionScoreMos) : 4.5;

        return new NoiseCancellationOverview(
            ActiveStreamChannels: list.Count,
            AverageSnrGainDb: +16.8, // 16.8 dB SNR improvement
            AverageMosScore: Math.Round(avgMos, 2),
            ProcessedPacketsPerSecond: 14200,
            Channels: list
        );
    }

    public NoiseFilterResult ToggleFilter(ToggleNoiseFilterCommand command)
    {
        if (!_channels.TryGetValue(command.ChannelId, out var existing))
        {
            throw new KeyNotFoundException($"Audio channel {command.ChannelId} not found.");
        }

        var newSnr = command.EnableFilter ? existing.SnrDb + 8.5 : Math.Max(10.0, existing.SnrDb - 8.5);
        var newMos = command.EnableFilter ? 4.7 : 3.6;

        var updated = existing with
        {
            IsFilterActive = command.EnableFilter,
            SnrDb = Math.Round(newSnr, 1),
            MeanOpinionScoreMos = newMos,
            DspLatencyMs = command.EnableFilter ? 11 : 5
        };

        _channels[command.ChannelId] = updated;

        var log = $"WebRTC DSP Engine: Spectral gating filter {(command.EnableFilter ? "ACTIVATED" : "DEACTIVATED")} on {command.ChannelId}. Suppression: {command.TargetSuppressionDb} dB, New MOS: {newMos}.";

        return new NoiseFilterResult(
            ChannelId: command.ChannelId,
            IsActive: command.EnableFilter,
            AppliedSuppressionDb: command.EnableFilter ? command.TargetSuppressionDb : 0.0,
            NewSnrDb: Math.Round(newSnr, 1),
            NewMosScore: newMos,
            DspLog: log
        );
    }
}
