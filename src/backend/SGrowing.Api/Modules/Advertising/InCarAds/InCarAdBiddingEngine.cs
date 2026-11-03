using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Advertising.InCarAds;

public class InCarAdBiddingEngine
{
    private readonly ConcurrentDictionary<string, InCarAdSession> _sessions = new();
    private readonly ConcurrentBag<InCarImpressionMetric> _metrics = new();

    public InCarAdBiddingEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var s1 = new InCarAdSession(
            "CAR-SESS-001",
            "ANDROID_AUTOMOTIVE",
            "EV_CHARGING_STATION",
            65.0,
            18.5,
            "AUDIO_ONLY",
            "Electrify SuperCharge Network",
            34.50m,
            "SERVING"
        );

        var s2 = new InCarAdSession(
            "CAR-SESS-002",
            "APPLE_CARPLAY",
            "QUICK_SERVICE_RESTAURANT",
            0.0,
            72.0,
            "AUDIO_AND_PARK_DISPLAY",
            "Starbucks Artisan Roastery",
            28.00m,
            "SERVING"
        );

        _sessions[s1.SessionId] = s1;
        _sessions[s2.SessionId] = s2;

        _metrics.Add(new InCarImpressionMetric(
            "MET-001",
            "CAR-SESS-001",
            "Electrify SuperCharge Network",
            15,
            true,
            true,
            DateTime.UtcNow.AddMinutes(-20)
        ));

        _metrics.Add(new InCarImpressionMetric(
            "MET-002",
            "CAR-SESS-002",
            "Starbucks Artisan Roastery",
            10,
            true,
            false,
            DateTime.UtcNow.AddMinutes(-8)
        ));
    }

    public InCarAdOverview GetOverview()
    {
        var sessionsList = _sessions.Values.ToList();
        var metricsList = _metrics.ToList();

        int detours = metricsList.Count(m => m.NavigationalDetourAccepted);
        decimal spend = sessionsList.Sum(s => s.ClearingCpmUsd * 120.0m);

        return new InCarAdOverview(
            sessionsList.Count,
            100.0,
            detours,
            spend,
            sessionsList,
            metricsList
        );
    }

    public InCarAdSession SubmitBid(SubmitInCarBidRequest request)
    {
        var sessId = $"CAR-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        string format = request.VehicleSpeedKmh > 0 ? "AUDIO_ONLY" : "AUDIO_AND_PARK_DISPLAY";
        decimal clearingCpm = request.MaxBidCpmUsd * 0.90m;

        var session = new InCarAdSession(
            sessId,
            request.VehiclePlatform,
            request.DestinationPoiCategory,
            request.VehicleSpeedKmh,
            request.BatteryLevelPct,
            format,
            request.SponsorBrand,
            clearingCpm,
            "BID_WON"
        );

        _sessions[sessId] = session;
        return session;
    }

    public InCarImpressionMetric RecordImpression(RecordInCarImpressionRequest request)
    {
        var metricId = $"MET-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var metric = new InCarImpressionMetric(
            metricId,
            request.SessionId,
            request.SponsorBrand,
            request.AudioDurationSeconds,
            request.DriverEngagedVoiceResponse,
            request.NavigationalDetourAccepted,
            DateTime.UtcNow
        );

        _metrics.Add(metric);
        return metric;
    }
}
