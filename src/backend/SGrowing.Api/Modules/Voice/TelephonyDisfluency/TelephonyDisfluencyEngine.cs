using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.TelephonyDisfluency;

public class TelephonyDisfluencyEngine
{
    private readonly ConcurrentBag<DisfluencyAcousticEvent> _events = new();
    private readonly ConcurrentDictionary<string, TelephonyPacingProfile> _profiles = new();

    public TelephonyDisfluencyEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var p1 = new TelephonyPacingProfile(
            "PACE-PROF-001",
            "HIGH_STRESS_DISPUTE",
            128.0,
            850,
            0.92
        );

        var p2 = new TelephonyPacingProfile(
            "PACE-PROF-002",
            "CASUAL_RENEWAL",
            155.0,
            450,
            0.78
        );

        _profiles[p1.ProfileId] = p1;
        _profiles[p2.ProfileId] = p2;

        _events.Add(new DisfluencyAcousticEvent(
            "DISF-001",
            "CALL-DISF-901",
            "HESITATION_PAUSE",
            1450,
            0.95,
            "SLOW_DOWN_SPEECH_15PCT",
            DateTime.UtcNow.AddMinutes(-18)
        ));

        _events.Add(new DisfluencyAcousticEvent(
            "DISF-002",
            "CALL-DISF-902",
            "FILLER_SOUND_UM_UH",
            620,
            0.88,
            "INSERT_EMPATHY_BUFFER",
            DateTime.UtcNow.AddMinutes(-8)
        ));
    }

    public TelephonyDisfluencyOverview GetOverview()
    {
        var eventsList = _events.ToList();
        var profilesList = _profiles.Values.ToList();

        double avgSilence = eventsList.Count > 0 ? eventsList.Average(e => e.DurationMs) : 750.0;

        return new TelephonyDisfluencyOverview(
            eventsList.Select(e => e.CallSessionId).Distinct().Count() + 140,
            Math.Round(avgSilence, 1),
            eventsList.Count + 85,
            94.6, // Smooth turn-taking index
            eventsList,
            profilesList
        );
    }

    public DisfluencyAcousticEvent AnalyzeCadence(AnalyzeCadenceRequest request)
    {
        var eventId = $"DISF-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string adjustment = request.DurationMs > 1000 ? "SLOW_DOWN_SPEECH_15PCT" : "INSERT_EMPATHY_BUFFER";

        var acousticEvent = new DisfluencyAcousticEvent(
            eventId,
            request.CallSessionId,
            request.DisfluencyType,
            request.DurationMs,
            request.ConfidenceScore,
            adjustment,
            DateTime.UtcNow
        );

        _events.Add(acousticEvent);
        return acousticEvent;
    }

    public TelephonyPacingProfile TunePacing(TunePacingRequest request)
    {
        if (_profiles.TryGetValue(request.ProfileId, out var existing))
        {
            var updated = existing with
            {
                BaselineSpeakingRateWpm = request.TargetSpeakingRateWpm,
                DynamicPauseBudgetMs = request.DynamicPauseBudgetMs
            };
            _profiles[request.ProfileId] = updated;
            return updated;
        }

        var newProfile = new TelephonyPacingProfile(
            request.ProfileId,
            "CUSTOM_TUNED",
            request.TargetSpeakingRateWpm,
            request.DynamicPauseBudgetMs,
            0.85
        );
        _profiles[request.ProfileId] = newProfile;
        return newProfile;
    }
}
