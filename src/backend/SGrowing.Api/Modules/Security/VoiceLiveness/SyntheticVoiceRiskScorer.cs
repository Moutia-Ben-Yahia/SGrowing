namespace SGrowing.Api.Modules.Security.VoiceLiveness;

/// <summary>
/// Composite biometric scorer combining spectral vocoder analysis, acoustic harmonics,
/// and dynamic challenge-response verification to classify call audio legitimacy.
/// </summary>
public sealed class SyntheticVoiceRiskScorer
{
    private readonly SpectralAcousticLivenessAnalyzer _spectralAnalyzer;
    private readonly BiometricChallengeResponseService _challengeService;

    public SyntheticVoiceRiskScorer(
        SpectralAcousticLivenessAnalyzer spectralAnalyzer,
        BiometricChallengeResponseService challengeService)
    {
        _spectralAnalyzer = spectralAnalyzer ?? throw new ArgumentNullException(nameof(spectralAnalyzer));
        _challengeService = challengeService ?? throw new ArgumentNullException(nameof(challengeService));
    }

    /// <summary>
    /// Performs end-to-end synthetic voice detection on audio samples.
    /// </summary>
    public VoiceLivenessEvaluationResult EvaluateLiveness(VerifyVoiceLivenessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var features = _spectralAnalyzer.AnalyzeAudio(request.AudioPcmSamples, request.SampleRateHz);
        var (challengePassed, challengeFailureReason) = _challengeService.EvaluateChallengeResponse(
            request.ActiveChallengeId ?? string.Empty,
            request.CallerResponseLatencyMs
        );

        var redFlags = new List<string>();

        if (features.SyntheticVocoderArtifactScore >= 0.50)
        {
            redFlags.Add($"High Neural Vocoder Score ({features.SyntheticVocoderArtifactScore:F2})");
        }

        if (features.SpectralFluxMean < 0.15 && features.DurationSeconds > 0.5)
        {
            redFlags.Add("Over-smoothed spectrogram transitions (TTS diffusion artifact)");
        }

        if (features.HarmonicToNoiseRatioDb < 8.0 && features.DurationSeconds > 0.5)
        {
            redFlags.Add($"Abnormally low vocal harmonic resonance (HNR: {features.HarmonicToNoiseRatioDb:F1} dB)");
        }

        if (features.PhaseDiscontinuityCount > 5)
        {
            redFlags.Add($"Detected {features.PhaseDiscontinuityCount} phase discontinuity clicks (spliced audio deepfake)");
        }

        if (!challengePassed && challengeFailureReason is not null)
        {
            redFlags.Add($"Biometric Challenge Failure: {challengeFailureReason}");
        }

        // Composite risk score: [0.0 = live human, 1.0 = deepfake synthetic]
        var challengePenalty = challengePassed ? 0.0 : 0.40;
        var compositeRisk = Math.Clamp((features.SyntheticVocoderArtifactScore * 0.60) + challengePenalty, 0.0, 1.0);

        var verdict = compositeRisk switch
        {
            >= 0.65 => LivenessVerdict.DefiniteDeepfakeSynthetic,
            >= 0.30 => LivenessVerdict.SuspiciousReplay,
            _ => LivenessVerdict.AuthenticLiveHuman
        };

        if (features.DurationSeconds < 0.20)
        {
            verdict = LivenessVerdict.InconclusiveLowAudioQuality;
        }

        var evalId = $"EVAL-LIV-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        return new VoiceLivenessEvaluationResult(
            EvaluationId: evalId,
            CallSessionId: request.CallSessionId,
            Verdict: verdict,
            SyntheticRiskScore: Math.Round(compositeRisk, 4),
            SpectralFeatures: features,
            PassedBiometricChallenge: challengePassed,
            TriggeredRedFlags: redFlags,
            EvaluatedAtUtc: DateTime.UtcNow
        );
    }
}
