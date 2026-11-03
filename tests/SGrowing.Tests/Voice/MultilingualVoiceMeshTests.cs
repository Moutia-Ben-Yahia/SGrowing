using FluentAssertions;
using SGrowing.Api.Modules.Voice.MultilingualVoiceMesh;
using Xunit;

namespace SGrowing.Tests.Voice;

public class MultilingualVoiceMeshTests
{
    private readonly MultilingualVoiceMeshEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSessionsAndVoiceClones()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.ActiveTranslationStreamsCount.Should().Be(2);
        overview.SupportedLanguagePairsCount.Should().BeGreaterThan(30);
        overview.AverageTranslationLatencyMs.Should().BeLessThan(200.0);
        overview.VocalTimbreSimilarityScore.Should().BeGreaterThan(0.95);
        overview.ActiveSessions.Should().HaveCount(2);
        overview.RegisteredVoiceClones.Should().HaveCount(2);
    }

    [Fact]
    public void TranslateAudioChunk_TranslatesWithSub200msLatency()
    {
        var request = new TranslateAudioChunkRequest(
            "TRN-SES-401",
            "T1BVUzQ4a0luYm91bmRBdWRpby==",
            "fr-FR",
            "en-US",
            "VCL-ALEX-01"
        );

        var result = _engine.TranslateAudioChunk(request);

        result.Should().NotBeNull();
        result.SessionId.Should().Be("TRN-SES-401");
        result.TranslatedText.Should().Contain("sovereign architecture");
        result.TranslationLatencyMs.Should().BeLessThan(200);
        result.VocalSimilarityPct.Should().BeGreaterThan(95.0);
    }

    [Fact]
    public void RegisterVoiceClone_CreatesTimbreProfileWithDigest()
    {
        var request = new RegisterVoiceCloneRequest(
            "Chiara Rossi (VP Sales Southern Europe)",
            "T1BVU1JFRkVSRU5DRVNBTVBMRT09"
        );

        var receipt = _engine.RegisterVoiceClone(request);

        receipt.Should().NotBeNull();
        receipt.ProfileId.Should().StartWith("VCL-");
        receipt.SpeakerName.Should().Be("Chiara Rossi (VP Sales Southern Europe)");
        receipt.Status.Should().Be("TIMBRE_EMBEDDING_REGISTERED");
        receipt.TimbreEmbeddingDigest.Should().StartWith("0x");
    }
}
