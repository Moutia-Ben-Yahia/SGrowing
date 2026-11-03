using SGrowing.Api.Modules.Voice.TelephonyDisfluency;
using Xunit;

namespace SGrowing.Tests.Voice;

public class TelephonyDisfluencyTests
{
    [Fact]
    public void GetOverview_ReportsSessionsSilenceAndProfiles()
    {
        var engine = new TelephonyDisfluencyEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalAnalyzedSessionsCount > 100);
        Assert.True(overview.AverageCallerSilenceDurationMs > 500.0);
        Assert.True(overview.DisfluenciesDetectedLast24Hours > 50);
        Assert.True(overview.TurnTakingSmoothnessIndex > 90.0);
        Assert.NotEmpty(overview.RecentEvents);
        Assert.NotEmpty(overview.PacingProfiles);
    }

    [Fact]
    public void AnalyzeCadence_DetectsDisfluencyAndRecommendsPacingAdjustment()
    {
        var engine = new TelephonyDisfluencyEngine();
        var request = new AnalyzeCadenceRequest(
            "CALL-DISF-903",
            "HESITATION_PAUSE",
            1200,
            0.94,
            "HIGH_STRESS_DISPUTE"
        );

        var evt = engine.AnalyzeCadence(request);

        Assert.NotNull(evt);
        Assert.StartsWith("DISF-", evt.EventId);
        Assert.Equal("CALL-DISF-903", evt.CallSessionId);
        Assert.Equal("SLOW_DOWN_SPEECH_15PCT", evt.RecommendedPacingAdjustment);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentEvents, e => e.EventId == evt.EventId);
    }

    [Fact]
    public void TunePacing_UpdatesPacingProfileRateAndPauseBudget()
    {
        var engine = new TelephonyDisfluencyEngine();
        var request = new TunePacingRequest(
            "PACE-PROF-001",
            120.0,
            900
        );

        var updated = engine.TunePacing(request);

        Assert.NotNull(updated);
        Assert.Equal("PACE-PROF-001", updated.ProfileId);
        Assert.Equal(120.0, updated.BaselineSpeakingRateWpm);
        Assert.Equal(900, updated.DynamicPauseBudgetMs);
    }
}
