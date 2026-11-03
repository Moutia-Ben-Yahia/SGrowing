using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Voice.AudioNoiseCancellation;
using Xunit;

namespace SGrowing.Tests.Voice;

public class AudioNoiseCancellationTests
{
    private readonly AudioNoiseCancellationEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActiveAudioDspChannels()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveStreamChannels.Should().Be(3);
        overview.AverageSnrGainDb.Should().BeGreaterThan(10.0);
        overview.AverageMosScore.Should().BeGreaterThan(4.0);
        overview.Channels.Should().NotBeEmpty();
    }

    [Fact]
    public void ToggleFilter_EnablesSpectralGatingAndImprovesMos()
    {
        var command = new ToggleNoiseFilterCommand("VOX-DSP-02", true, 24.0);
        var result = _engine.ToggleFilter(command);

        result.Should().NotBeNull();
        result.ChannelId.Should().Be("VOX-DSP-02");
        result.IsActive.Should().BeTrue();
        result.AppliedSuppressionDb.Should().Be(24.0);
        result.NewMosScore.Should().Be(4.7);
        result.DspLog.Should().Contain("ACTIVATED");
    }

    [Fact]
    public void ToggleFilter_ThrowsWhenChannelNotFound()
    {
        var command = new ToggleNoiseFilterCommand("INVALID-CHANNEL", true, 20.0);
        var act = () => _engine.ToggleFilter(command);

        act.Should().Throw<KeyNotFoundException>();
    }
}
