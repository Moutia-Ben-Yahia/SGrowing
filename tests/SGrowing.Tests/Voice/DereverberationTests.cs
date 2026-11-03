using SGrowing.Api.Modules.Voice.Dereverberation;
using Xunit;

namespace SGrowing.Tests.Voice;

public sealed class DereverberationTests
{
    private readonly DereverberationEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededProfilesAndMetrics()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveProcessedStreamsCount >= 50);
        Assert.True(overview.AverageSnrImprovementDb > 10.0);
        Assert.NotEmpty(overview.RecentProfiles);
        Assert.NotEmpty(overview.RecentFilterEvents);
    }

    [Fact]
    public void ProfileRoomAcoustics_GlassConference_CalculatesCorrectDirectToReverberantRatio()
    {
        var profile = _engine.ProfileRoomAcoustics("CALL-RTC-902", RoomAcousticType.GlassConferenceRoom, 0.75);

        Assert.NotNull(profile);
        Assert.StartsWith("ROOM-PROF-", profile.ProfileId);
        Assert.Equal(0.75, profile.EstimatedRt60DecaySeconds);
        Assert.True(profile.DirectToReverberantRatioDb > 0.0);
    }

    [Fact]
    public void CleanAudioStream_LowSnr_AppliesSpectralDereverberationGain()
    {
        var flt = _engine.CleanAudioStream("CALL-RTC-902", 10.0);

        Assert.NotNull(flt);
        Assert.StartsWith("FLT-EVT-", flt.FilterEventId);
        Assert.True(flt.FilterActive);
        Assert.True(flt.OutputSnrDb >= 22.0);
        Assert.True(flt.ProcessingDelayMs < 3.0);
    }
}
