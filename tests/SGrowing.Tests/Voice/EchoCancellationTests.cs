using SGrowing.Api.Modules.Voice.EchoCancellation;
using Xunit;

namespace SGrowing.Tests.Voice;

public class EchoCancellationTests
{
    [Fact]
    public void GetOverview_ReportsSuppressionDbAndDuplexClarity()
    {
        var engine = new EchoCancellationEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveWebRtcSessionsCount > 80);
        Assert.True(overview.AverageEchoSuppressionDb < -50.0); // Less than -50 dB
        Assert.True(overview.DoubleTalkAccuracyPct > 95.0);
        Assert.True(overview.DuplexSpeechClarityScore > 95.0);
        Assert.NotEmpty(overview.RecentFrames);
        Assert.NotEmpty(overview.Profiles);
    }

    [Fact]
    public void ProcessAudioFrame_DetectsDoubleTalkAndSuppressesEcho()
    {
        var engine = new EchoCancellationEngine();
        var request = new ProcessAudioFrameRequest(
            "CALL-RTC-8801",
            -20.0,
            -22.0,
            true
        );

        var frame = engine.ProcessAudioFrame(request);

        Assert.NotNull(frame);
        Assert.StartsWith("FRAME-", frame.FrameId);
        Assert.Equal("CALL-RTC-8801", frame.CallSessionId);
        Assert.True(frame.DoubleTalkDetected);
        Assert.True(frame.ResidualEchoDecibels < -50.0);
        Assert.Equal("CONVERGED", frame.FilterState);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentFrames, f => f.FrameId == frame.FrameId);
    }

    [Fact]
    public void TuneSuppression_UpdatesFilterTapsAndStepSize()
    {
        var engine = new EchoCancellationEngine();
        var request = new TuneSuppressionRequest(
            "ECHO-PROF-001",
            4096,
            0.015
        );

        var profile = engine.TuneSuppression(request);

        Assert.NotNull(profile);
        Assert.Equal("ECHO-PROF-001", profile.ProfileId);
        Assert.Equal(4096, profile.FilterLengthTaps);
        Assert.Equal(0.015, profile.StepSizeMu);
    }
}
