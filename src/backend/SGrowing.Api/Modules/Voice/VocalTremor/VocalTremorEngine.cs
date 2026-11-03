using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Voice.VocalTremor;

public interface IVocalTremorEngine
{
    WhisperEnhancementResult ProcessWhisperEnhancement(string callId, double energyDbfs);
    VocalTremorMetrics AnalyzeAcoustics(AnalyzeAcousticsRequest request);
    IReadOnlyList<VocalTremorSessionSummary> GetSessionSummaries();
    VocalTremorSessionSummary? GetSessionSummary(string callId);
}

public class VocalTremorEngine : IVocalTremorEngine
{
    private readonly ConcurrentDictionary<string, VocalTremorSessionSummary> _sessions = new();
    private readonly ConcurrentBag<VocalTremorMetrics> _metricLogs = new();

    public VocalTremorEngine()
    {
        SeedInitialSessions();
    }

    private void SeedInitialSessions()
    {
        var sess1 = new VocalTremorSessionSummary(
            CallId: "CALL-VOICE-881",
            CallerId: "+1-415-555-0199",
            TotalFramesAnalyzed: 1840,
            WhisperFramesDetected: 24,
            PeakStressScore: 42,
            FinalStressClassification: "MildStress",
            DeEscalationRecommended: false,
            SessionStartTimeUtc: DateTime.UtcNow.AddMinutes(-12)
        );

        var sess2 = new VocalTremorSessionSummary(
            CallId: "CALL-VOICE-882",
            CallerId: "+1-212-555-0144",
            TotalFramesAnalyzed: 950,
            WhisperFramesDetected: 410,
            PeakStressScore: 89,
            FinalStressClassification: "SevereDuress",
            DeEscalationRecommended: true,
            SessionStartTimeUtc: DateTime.UtcNow.AddMinutes(-5)
        );

        _sessions[sess1.CallId] = sess1;
        _sessions[sess2.CallId] = sess2;
    }

    public WhisperEnhancementResult ProcessWhisperEnhancement(string callId, double energyDbfs)
    {
        // Whisper range typically -55 dBFS to -35 dBFS with low harmonic voicing
        bool isWhisper = energyDbfs <= -35.0 && energyDbfs >= -60.0;
        double origGain = 0.0;
        double enhancedGain = isWhisper ? 14.5 : 0.0;
        double formantBoost = isWhisper ? 2.4 : 1.0;
        string status = isWhisper
            ? "Whisper Detected: Applied +14.5dB Adaptive Gain & 2.4x Formant Boost"
            : "Voiced Speech: Standard Pass-Through";

        return new WhisperEnhancementResult(
            FrameId: $"FRM-{AcousticSeed()}",
            CallId: callId,
            WhisperDetected: isWhisper,
            OriginalGainDb: origGain,
            EnhancedGainDb: enhancedGain,
            FormantBoostRatio: formantBoost,
            StatusDescription: status,
            ProcessedAtUtc: DateTime.UtcNow
        );
    }

    public VocalTremorMetrics AnalyzeAcoustics(AnalyzeAcousticsRequest request)
    {
        // Micro-vocal tremor physiological oscillation occurs in 8.0 Hz - 14.0 Hz
        bool isMicroTremorActive = request.ModulationFrequencyHz >= 8.0 && request.ModulationFrequencyHz <= 14.0;
        
        // Calculate acute stress score: combines tremor modulation, jitter, and shimmer
        int stressScore = 15;
        if (isMicroTremorActive) stressScore += 45;
        if (request.JitterPercentage > 1.2) stressScore += 20;
        if (request.ShimmerPercentage > 3.5) stressScore += 15;
        stressScore = Math.Clamp(stressScore, 0, 100);

        string duress = stressScore switch
        {
            >= 75 => "SevereDuress",
            >= 40 => "MildStress",
            _ => "Normal"
        };

        bool alertTriggered = stressScore >= 75;

        var metrics = new VocalTremorMetrics(
            CallId: request.CallId,
            AverageTremorModulationHz: request.ModulationFrequencyHz,
            JitterVariance: request.JitterPercentage,
            AcuteStressScore: stressScore,
            DuressLevel: duress,
            SupervisorAlertTriggered: alertTriggered,
            EvaluatedAtUtc: DateTime.UtcNow
        );

        _metricLogs.Add(metrics);

        // Update or insert session summary
        _sessions.AddOrUpdate(
            request.CallId,
            new VocalTremorSessionSummary(
                CallId: request.CallId,
                CallerId: request.CallerId,
                TotalFramesAnalyzed: 1,
                WhisperFramesDetected: request.EnergyDbfs <= -35.0 ? 1 : 0,
                PeakStressScore: stressScore,
                FinalStressClassification: duress,
                DeEscalationRecommended: alertTriggered,
                SessionStartTimeUtc: DateTime.UtcNow
            ),
            (id, existing) => new VocalTremorSessionSummary(
                CallId: id,
                CallerId: request.CallerId,
                TotalFramesAnalyzed: existing.TotalFramesAnalyzed + 1,
                WhisperFramesDetected: request.EnergyDbfs <= -35.0 ? existing.WhisperFramesDetected + 1 : existing.WhisperFramesDetected,
                PeakStressScore: Math.Max(existing.PeakStressScore, stressScore),
                FinalStressClassification: stressScore > existing.PeakStressScore ? duress : existing.FinalStressClassification,
                DeEscalationRecommended: existing.DeEscalationRecommended || alertTriggered,
                SessionStartTimeUtc: existing.SessionStartTimeUtc
            )
        );

        return metrics;
    }

    public IReadOnlyList<VocalTremorSessionSummary> GetSessionSummaries()
    {
        return _sessions.Values.OrderByDescending(s => s.SessionStartTimeUtc).ToList();
    }

    public VocalTremorSessionSummary? GetSessionSummary(string callId)
    {
        _sessions.TryGetValue(callId, out var summary);
        return summary;
    }

    private static int _seedCounter = 5000;
    private static int AcousticSeed() => Interlocked.Increment(ref _seedCounter);
}
