using SGrowing.Api.Modules.Advertising.InCarAds;
using Xunit;

namespace SGrowing.Tests.Advertising;

public class InCarAdTests
{
    [Fact]
    public void GetOverview_ReportsActiveSessionsAndImpressions()
    {
        var engine = new InCarAdBiddingEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveVehicleSessionsCount >= 2);
        Assert.Equal(100.0, overview.NonDistractionCompliancePct);
        Assert.True(overview.TotalDetoursAcceptedCount >= 1);
        Assert.True(overview.TotalInCarAdSpendUsd > 0m);
        Assert.NotEmpty(overview.ActiveSessions);
        Assert.NotEmpty(overview.RecentMetrics);
    }

    [Fact]
    public void SubmitBid_CalculatesClearingCpmAndAwardsSession()
    {
        var engine = new InCarAdBiddingEngine();
        var request = new SubmitInCarBidRequest(
            "ANDROID_AUTOMOTIVE",
            "EV_CHARGING_STATION",
            75.0,
            12.5,
            "ChargePoint Global",
            40.00m
        );

        var session = engine.SubmitBid(request);

        Assert.NotNull(session);
        Assert.StartsWith("CAR-", session.SessionId);
        Assert.Equal("ANDROID_AUTOMOTIVE", session.VehiclePlatform);
        Assert.Equal("AUDIO_ONLY", session.AdFormatAllowed);
        Assert.Equal("ChargePoint Global", session.SponsorBrand);
        Assert.Equal(36.00m, session.ClearingCpmUsd);
        Assert.Equal("BID_WON", session.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveSessions, s => s.SessionId == session.SessionId);
    }

    [Fact]
    public void RecordImpression_LogsMetricAndUpdatesDetourCount()
    {
        var engine = new InCarAdBiddingEngine();
        var request = new RecordInCarImpressionRequest(
            "CAR-SESS-001",
            "Electrify SuperCharge Network",
            12,
            true,
            true
        );

        var metric = engine.RecordImpression(request);

        Assert.NotNull(metric);
        Assert.StartsWith("MET-", metric.MetricId);
        Assert.Equal("CAR-SESS-001", metric.SessionId);
        Assert.True(metric.DriverEngagedVoiceResponse);
        Assert.True(metric.NavigationalDetourAccepted);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentMetrics, m => m.MetricId == metric.MetricId);
    }
}
