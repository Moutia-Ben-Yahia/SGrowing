using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.VoiceLiveness;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class VoiceDeepfakeDefenseTests
{
    private readonly SpectralAcousticLivenessAnalyzer _analyzer = new();
    private readonly BiometricChallengeResponseService _challengeService = new();
    private readonly SyntheticVoiceRiskScorer _scorer;

    public VoiceDeepfakeDefenseTests()
    {
        _scorer = new SyntheticVoiceRiskScorer(_analyzer, _challengeService);
    }

    private static float[] GenerateSineWave(double frequencyHz, int sampleRateHz, double durationSec)
    {
        var sampleCount = (int)(sampleRateHz * durationSec);
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var t = (double)i / sampleRateHz;
            samples[i] = (float)Math.Sin(2.0 * Math.PI * frequencyHz * t);
        }
        return samples;
    }

    #region Spectral Acoustic Analysis Tests

    [Fact]
    public void SpectralAnalyzer_ShortAudio_ReturnsInconclusiveDefaults()
    {
        var samples = new float[100]; // Too short (< 512)
        var features = _analyzer.AnalyzeAudio(samples, 16000);

        features.DurationSeconds.Should().BeLessThan(0.1);
        features.SyntheticVocoderArtifactScore.Should().Be(0.50);
    }

    [Fact]
    public void SpectralAnalyzer_HarmonicPeriodicSignal_CalculatesValidHnr()
    {
        // 1 second of 220 Hz human pitch harmonic tone
        var samples = GenerateSineWave(220.0, 16000, 1.0);
        var features = _analyzer.AnalyzeAudio(samples, 16000);

        features.DurationSeconds.Should().BeApproximately(1.0, 0.05);
        features.HarmonicToNoiseRatioDb.Should().BeGreaterThan(10.0);
    }

    [Fact]
    public void SpectralAnalyzer_HighPhaseDiscontinuities_IncreasesVocoderScore()
    {
        var samples = GenerateSineWave(220.0, 16000, 1.0);
        // Inject 10 abrupt phase jumps / splicing clicks
        for (var i = 1; i <= 10; i++)
        {
            samples[i * 1000] = 0.95f;
            samples[(i * 1000) + 1] = -0.95f; // Jump > 1.8
        }

        var features = _analyzer.AnalyzeAudio(samples, 16000);
        features.PhaseDiscontinuityCount.Should().BeGreaterThanOrEqualTo(10);
        features.SyntheticVocoderArtifactScore.Should().BeGreaterThan(0.30);
    }

    #endregion

    #region Biometric Challenge Response Tests

    [Fact]
    public void BiometricChallenge_IssueChallenge_GeneratesRandomizedPhoneticPhrase()
    {
        var challenge = _challengeService.IssueChallenge(new IssueChallengeRequest("CALL-SES-001"));

        challenge.ChallengeId.Should().StartWith("CHAL-");
        challenge.RequiredPhoneticPhrase.Should().NotBeNullOrWhiteSpace();
        challenge.RandomNumericSalt.Should().HaveLength(3);
        challenge.MinimumResponseLatencyMs.Should().Be(350);
    }

    [Fact]
    public void BiometricChallenge_EvaluateChallenge_NormalLatency_Passes()
    {
        var challenge = _challengeService.IssueChallenge(new IssueChallengeRequest("CALL-SES-002"));

        var (passed, reason) = _challengeService.EvaluateChallengeResponse(challenge.ChallengeId, 750); // 750ms is typical human vocal response
        passed.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void BiometricChallenge_EvaluateChallenge_InstantSoundboardLatency_Fails()
    {
        var challenge = _challengeService.IssueChallenge(new IssueChallengeRequest("CALL-SES-003"));

        var (passed, reason) = _challengeService.EvaluateChallengeResponse(challenge.ChallengeId, 50); // 50ms is physically impossible for human brain processing
        passed.Should().BeFalse();
        reason.Should().Contain("pre-recorded soundboard");
    }

    [Fact]
    public void BiometricChallenge_EvaluateChallenge_ExcessiveAiPipelineLatency_Fails()
    {
        var challenge = _challengeService.IssueChallenge(new IssueChallengeRequest("CALL-SES-004"));

        var (passed, reason) = _challengeService.EvaluateChallengeResponse(challenge.ChallengeId, 3200); // 3.2s delay from slow speech-to-speech AI model
        passed.Should().BeFalse();
        reason.Should().Contain("Excessive response latency");
    }

    #endregion

    #region Composite Synthetic Voice Risk Scorer Tests

    [Fact]
    public void SyntheticVoiceRiskScorer_NaturalHumanVoiceWithValidChallenge_ClassifiedAsAuthentic()
    {
        var challenge = _challengeService.IssueChallenge(new IssueChallengeRequest("CALL-SAFE-01"));
        var audio = GenerateSineWave(180.0, 16000, 1.0);

        var request = new VerifyVoiceLivenessRequest(
            CallSessionId: "CALL-SAFE-01",
            AudioPcmSamples: audio,
            SampleRateHz: 16000,
            ActiveChallengeId: challenge.ChallengeId,
            CallerResponseLatencyMs: 850
        );

        var result = _scorer.EvaluateLiveness(request);

        result.PassedBiometricChallenge.Should().BeTrue();
        result.Verdict.Should().Be(LivenessVerdict.AuthenticLiveHuman);
        result.SyntheticRiskScore.Should().BeLessThan(0.35);
    }

    [Fact]
    public void SyntheticVoiceRiskScorer_ArtificialVocoderWithFailedChallenge_ClassifiedAsDeepfake()
    {
        var challenge = _challengeService.IssueChallenge(new IssueChallengeRequest("CALL-ATTACK-01"));
        var audio = GenerateSineWave(200.0, 16000, 1.0);
        // Inject splicing discontinuities
        for (var i = 1; i <= 8; i++)
        {
            audio[i * 1200] = 0.95f;
            audio[(i * 1200) + 1] = -0.95f;
        }

        var request = new VerifyVoiceLivenessRequest(
            CallSessionId: "CALL-ATTACK-01",
            AudioPcmSamples: audio,
            SampleRateHz: 16000,
            ActiveChallengeId: challenge.ChallengeId,
            CallerResponseLatencyMs: 3800 // Failed challenge latency
        );

        var result = _scorer.EvaluateLiveness(request);

        result.PassedBiometricChallenge.Should().BeFalse();
        result.Verdict.Should().BeOneOf(LivenessVerdict.DefiniteDeepfakeSynthetic, LivenessVerdict.SuspiciousReplay);
        result.TriggeredRedFlags.Should().NotBeEmpty();
    }

    [Fact]
    public void SyntheticVoiceRiskScorer_ShortDuration_ClassifiedAsInconclusive()
    {
        var audio = new float[100];
        var request = new VerifyVoiceLivenessRequest("CALL-TINY", audio, 16000);

        var result = _scorer.EvaluateLiveness(request);
        result.Verdict.Should().Be(LivenessVerdict.InconclusiveLowAudioQuality);
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void ServiceCollectionExtensions_AddVoiceLivenessServices_RegistersAllDependencies()
    {
        var services = new ServiceCollection();
        services.AddVoiceLivenessServices();

        services.Should().Contain(d => d.ServiceType == typeof(SpectralAcousticLivenessAnalyzer) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(BiometricChallengeResponseService) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(SyntheticVoiceRiskScorer) && d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
