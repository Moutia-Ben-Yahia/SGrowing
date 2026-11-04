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

    [Fact]
    public void GetOverview_ReflectsNewlyRegisteredVoiceCloneProfiles()
    {
        var initialOverview = _engine.GetOverview();
        var initialCloneCount = initialOverview.RegisteredVoiceClones.Count;

        var request = new RegisterVoiceCloneRequest("Lukas Weber (DACH Field Sales)", "BASE64SAMPLE123");
        var receipt = _engine.RegisterVoiceClone(request);

        var updatedOverview = _engine.GetOverview();
        updatedOverview.RegisteredVoiceClones.Should().HaveCount(initialCloneCount + 1);
        updatedOverview.RegisteredVoiceClones.Should().Contain(p => p.ProfileId == receipt.ProfileId && p.SpeakerName == "Lukas Weber (DACH Field Sales)");
    }

    [Fact]
    public void TranslateAudioChunk_UnknownSessionId_FallsBackToDefaultSession()
    {
        var request = new TranslateAudioChunkRequest(
            "TRN-SES-NONEXISTENT-999",
            "T1BVUzQ4a0luYm91bmRBdWRpby==",
            "es-ES",
            "en-US",
            "VCL-ALEX-01"
        );

        var result = _engine.TranslateAudioChunk(request);

        result.Should().NotBeNull();
        result.SessionId.Should().NotBeNullOrWhiteSpace();
        result.TranslatedText.Should().NotBeNullOrWhiteSpace();
        result.TranslationLatencyMs.Should().BeLessThan(200);
    }

    [Fact]
    public void TranslateAudioChunk_IncrementsChunksTranslatedCounter()
    {
        var request = new TranslateAudioChunkRequest(
            "TRN-SES-401",
            "T1BVUzQ4a0luYm91bmRBdWRpby==",
            "fr-FR",
            "en-US",
            "VCL-ALEX-01"
        );

        var initialOverview = _engine.GetOverview();
        var initialSession = initialOverview.ActiveSessions.First(s => s.SessionId == "TRN-SES-401");
        var initialChunks = initialSession.ChunksTranslated;

        _engine.TranslateAudioChunk(request);
        _engine.TranslateAudioChunk(request);

        var updatedOverview = _engine.GetOverview();
        var updatedSession = updatedOverview.ActiveSessions.First(s => s.SessionId == "TRN-SES-401");
        updatedSession.ChunksTranslated.Should().Be(initialChunks + 2);
    }

    [Fact]
    public void RegisterVoiceClone_GeneratesUniqueTimbreDigestsAcrossSuccessiveCalls()
    {
        var r1 = _engine.RegisterVoiceClone(new RegisterVoiceCloneRequest("Speaker A", "AUDIO1"));
        var r2 = _engine.RegisterVoiceClone(new RegisterVoiceCloneRequest("Speaker B", "AUDIO2"));

        r1.ProfileId.Should().StartWith("VCL-");
        r2.ProfileId.Should().StartWith("VCL-");
        r1.ProfileId.Should().NotBe(r2.ProfileId);
        r1.TimbreEmbeddingDigest.Should().NotBe(r2.TimbreEmbeddingDigest);
    }

    [Fact]
    public void RegisterVoiceClone_SetsTimestampWithinRecentSeconds()
    {
        var now = System.DateTime.UtcNow;
        var receipt = _engine.RegisterVoiceClone(new RegisterVoiceCloneRequest("Speaker Timestamp Check", "AUDIO_TIME"));

        receipt.Timestamp.Should().BeOnOrAfter(now.AddSeconds(-2));
        receipt.Timestamp.Should().BeOnOrBefore(System.DateTime.UtcNow.AddSeconds(2));
    }
}
