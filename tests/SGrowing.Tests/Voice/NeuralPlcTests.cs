using SGrowing.Api.Modules.Voice.NeuralPlc;
using Xunit;

namespace SGrowing.Tests.Voice;

public sealed class NeuralPlcTests
{
    private readonly NeuralPlcEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededReconstructionsAndJitterMetrics()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveStreamsCount >= 50);
        Assert.True(overview.AverageLossConcealedPct > 95.0);
        Assert.True(overview.AverageWaveformMosScore >= 4.0);
        Assert.NotEmpty(overview.RecentReconstructions);
        Assert.NotEmpty(overview.ActiveBufferMetrics);
    }

    [Fact]
    public void ReconstructLostFrame_SingleLostOpus_SynthesizesHighMosWaveform()
    {
        var recon = _engine.ReconstructLostFrame("CALL-RTC-9901", 521, PlcCodecType.Opus48kHz, 1);

        Assert.NotNull(recon);
        Assert.StartsWith("PLC-REC-", recon.ReconstructionId);
        Assert.True(recon.NeuralExtrapolationSuccess);
        Assert.True(recon.PredictedAudioMosScore >= 4.2);
        Assert.True(recon.WaveformSynthesisLatencyMs < 3.0);
    }

    [Fact]
    public void TuneJitterBuffer_HighVariance_ExpandsBufferToAvoidUnderrun()
    {
        var tuned = _engine.TuneJitterBuffer("CALL-RTC-9901", packetLossPct: 12.5, jitterVarianceMs: 22.0);

        Assert.NotNull(tuned);
        Assert.Equal(JitterBufferState.AdaptingGrowth, tuned.BufferState);
        Assert.True(tuned.TargetBufferDepthMs >= 35);
    }
}
