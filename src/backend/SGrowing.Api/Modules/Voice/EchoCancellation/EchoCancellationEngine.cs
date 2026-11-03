using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.EchoCancellation;

public class EchoCancellationEngine
{
    private readonly ConcurrentBag<AudioDspFrameMetric> _frames = new();
    private readonly ConcurrentDictionary<string, EchoFilterProfile> _profiles = new();

    public EchoCancellationEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var p1 = new EchoFilterProfile(
            "ECHO-PROF-001",
            "OPUS_48KHZ",
            2048,
            0.025,
            -58.4
        );

        var p2 = new EchoFilterProfile(
            "ECHO-PROF-002",
            "G722_16KHZ",
            1024,
            0.040,
            -52.0
        );

        _profiles[p1.ProfileId] = p1;
        _profiles[p2.ProfileId] = p2;

        _frames.Add(new AudioDspFrameMetric(
            "FRAME-001",
            "CALL-RTC-7701",
            -54.2,
            -18.4,
            -22.0,
            true, // Double-talk detected (caller talking over agent)
            0.98,
            "CONVERGED",
            DateTime.UtcNow.AddMinutes(-12)
        ));

        _frames.Add(new AudioDspFrameMetric(
            "FRAME-002",
            "CALL-RTC-7702",
            -56.8,
            -45.0,
            -16.2,
            false, // Far-end agent speaking only
            0.99,
            "CONVERGED",
            DateTime.UtcNow.AddMinutes(-5)
        ));
    }

    public EchoCancellationOverview GetOverview()
    {
        var framesList = _frames.ToList();
        var profilesList = _profiles.Values.ToList();

        double avgSuppression = framesList.Count > 0 ? framesList.Average(f => f.ResidualEchoDecibels) : -55.0;

        return new EchoCancellationOverview(
            framesList.Select(f => f.CallSessionId).Distinct().Count() + 85,
            Math.Round(avgSuppression, 1),
            99.2, // 99.2% double-talk detection accuracy
            98.5, // 98.5% clarity score
            framesList,
            profilesList
        );
    }

    public AudioDspFrameMetric ProcessAudioFrame(ProcessAudioFrameRequest request)
    {
        var frameId = $"FRAME-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        bool doubleTalk = request.ForceDoubleTalkEvaluation || (request.NearEndSpeechEnergyDb > -30.0 && request.FarEndReferenceEnergyDb > -30.0);
        double residualEcho = doubleTalk ? -52.4 : -58.2;

        var metric = new AudioDspFrameMetric(
            frameId,
            request.CallSessionId,
            residualEcho,
            request.NearEndSpeechEnergyDb,
            request.FarEndReferenceEnergyDb,
            doubleTalk,
            0.98,
            "CONVERGED",
            DateTime.UtcNow
        );

        _frames.Add(metric);
        return metric;
    }

    public EchoFilterProfile TuneSuppression(TuneSuppressionRequest request)
    {
        if (_profiles.TryGetValue(request.ProfileId, out var existing))
        {
            var updated = existing with
            {
                FilterLengthTaps = request.FilterLengthTaps,
                StepSizeMu = request.StepSizeMu
            };
            _profiles[request.ProfileId] = updated;
            return updated;
        }

        var newProfile = new EchoFilterProfile(
            request.ProfileId,
            "OPUS_48KHZ",
            request.FilterLengthTaps,
            request.StepSizeMu,
            -56.0
        );
        _profiles[request.ProfileId] = newProfile;
        return newProfile;
    }
}
