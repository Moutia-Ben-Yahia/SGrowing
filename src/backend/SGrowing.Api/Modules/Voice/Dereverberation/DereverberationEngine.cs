using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.Dereverberation;

public interface IDereverberationEngine
{
    DereverberationOverview GetOverview();
    RoomAcousticProfile ProfileRoomAcoustics(string callSessionId, RoomAcousticType roomType, double rawRt60Sec);
    DereverberationFilterEvent CleanAudioStream(string callSessionId, double inputSnrDb);
}

public sealed class DereverberationEngine : IDereverberationEngine
{
    private readonly ConcurrentDictionary<string, RoomAcousticProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, DereverberationFilterEvent> _filterEvents = new();

    public DereverberationEngine()
    {
        SeedInitialState();
    }

    private void SeedInitialState()
    {
        var p1 = new RoomAcousticProfile(
            "ROOM-PROF-001",
            "CALL-RTC-8801",
            RoomAcousticType.GlassConferenceRoom,
            EstimatedRt60DecaySeconds: 0.85,
            DirectToReverberantRatioDb: 4.2,
            DateTime.UtcNow.AddMinutes(-5)
        );
        _profiles.TryAdd(p1.ProfileId, p1);

        var f1 = new DereverberationFilterEvent(
            "FLT-EVT-001",
            "CALL-RTC-8801",
            InputSnrDb: 12.4,
            OutputSnrDb: 24.8,
            SpectralEchoSuppressionDb: -18.5,
            ProcessingDelayMs: 2.1,
            FilterActive: true
        );
        _filterEvents.TryAdd(f1.FilterEventId, f1);
    }

    public DereverberationOverview GetOverview()
    {
        var profs = _profiles.Values.Take(10).ToList();
        var flts = _filterEvents.Values.Take(10).ToList();

        return new DereverberationOverview(
            ActiveProcessedStreamsCount: 112,
            AverageRt60SuppressedSec: 0.72,
            AverageSnrImprovementDb: 12.4,
            AsrWordErrorRateReductionPct: 42.1,
            RecentProfiles: profs,
            RecentFilterEvents: flts
        );
    }

    public RoomAcousticProfile ProfileRoomAcoustics(string callSessionId, RoomAcousticType roomType, double rawRt60Sec)
    {
        var profile = new RoomAcousticProfile(
            $"ROOM-PROF-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            callSessionId,
            roomType,
            rawRt60Sec,
            DirectToReverberantRatioDb: Math.Round(12.0 - (rawRt60Sec * 8.0), 1),
            DateTime.UtcNow
        );
        _profiles.TryAdd(profile.ProfileId, profile);
        return profile;
    }

    public DereverberationFilterEvent CleanAudioStream(string callSessionId, double inputSnrDb)
    {
        double outputSnr = Math.Round(inputSnrDb + 12.5, 1);
        var flt = new DereverberationFilterEvent(
            $"FLT-EVT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            callSessionId,
            inputSnrDb,
            outputSnr,
            SpectralEchoSuppressionDb: -19.2,
            ProcessingDelayMs: 1.85,
            FilterActive: true
        );
        _filterEvents.TryAdd(flt.FilterEventId, flt);
        return flt;
    }
}
