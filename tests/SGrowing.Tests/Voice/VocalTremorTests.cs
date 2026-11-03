using SGrowing.Api.Modules.Voice.VocalTremor;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VocalTremorTests
{
    private readonly VocalTremorEngine _engine = new();

    [Fact]
    public void ProcessWhisperEnhancement_BoostsUnvoicedWhispers()
    {
        var whisperResult = _engine.ProcessWhisperEnhancement("CALL-TEST-W1", -45.0);
        Assert.True(whisperResult.WhisperDetected);
        Assert.True(whisperResult.EnhancedGainDb > 10.0);
        Assert.True(whisperResult.FormantBoostRatio > 2.0);

        var voicedResult = _engine.ProcessWhisperEnhancement("CALL-TEST-W2", -18.0);
        Assert.False(voicedResult.WhisperDetected);
        Assert.Equal(0.0, voicedResult.EnhancedGainDb);
    }

    [Fact]
    public void AnalyzeAcoustics_DetectsMicroTremorStressAndTriggersAlert()
    {
        var request = new AnalyzeAcousticsRequest(
            CallId: "CALL-TEST-STRESS-01",
            CallerId: "+1-800-555-9911",
            EnergyDbfs: -22.0,
            FundamentalFrequencyHz: 210.0,
            JitterPercentage: 2.1,
            ShimmerPercentage: 4.8,
            ModulationFrequencyHz: 10.5 // Within 8-14 Hz physiological micro-tremor range
        );

        var metrics = _engine.AnalyzeAcoustics(request);

        Assert.NotNull(metrics);
        Assert.True(metrics.AcuteStressScore >= 75);
        Assert.Equal("SevereDuress", metrics.DuressLevel);
        Assert.True(metrics.SupervisorAlertTriggered);
    }

    [Fact]
    public void GetSessionSummaries_TracksPeakStressAndDeEscalation()
    {
        var sessions = _engine.GetSessionSummaries();
        Assert.NotEmpty(sessions);

        var duressSession = sessions.FirstOrDefault(s => s.FinalStressClassification == "SevereDuress");
        Assert.NotNull(duressSession);
        Assert.True(duressSession.DeEscalationRecommended);
        Assert.True(duressSession.PeakStressScore > 80);
    }
}
