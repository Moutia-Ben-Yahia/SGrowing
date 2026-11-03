using SGrowing.Api.Modules.AI.VideoAvatar;
using Xunit;

namespace SGrowing.Tests.AI;

public sealed class VideoAvatarTests
{
    private readonly VideoAvatarEngine _engine = new();

    [Fact]
    public async Task GetAvailablePersonas_ReturnsDefaultPersonasWithExpertise()
    {
        var personas = await _engine.GetAvailablePersonasAsync();

        Assert.NotNull(personas);
        Assert.True(personas.Count >= 3);
        Assert.Contains(personas, p => p.Id == "persona_elena_sdr");
        Assert.Contains(personas, p => p.Id == "persona_marcus_architect");
    }

    [Fact]
    public async Task InitializeSession_ReturnsConnectedWebRtcSession()
    {
        var request = new AvatarSessionInitRequest(
            PersonaId: "persona_elena_sdr",
            UserId: "usr_buyer_42",
            Mode: "InteractiveWebRTC",
            Resolution: "1080p"
        );

        var session = await _engine.InitializeSessionAsync(request);

        Assert.NotNull(session);
        Assert.Equal("Connected", session.Status);
        Assert.Equal(60, session.Fps);
        Assert.True(session.LatencyMs < 150);
        Assert.Contains("v=0", session.SdpOffer);
        Assert.Contains("v=0", session.SdpAnswer);
    }

    [Fact]
    public async Task SynthesizeSpeechAndVisemes_GeneratesTimedFacialBlendshapes()
    {
        var session = await _engine.InitializeSessionAsync(new AvatarSessionInitRequest("persona_elena_sdr", "usr_1"));
        var speechRequest = new AvatarSpeechRequest("Welcome to Growth OS enterprise platform demonstration.", "Enthusiastic");

        var response = await _engine.SynthesizeSpeechAndVisemesAsync(session.SessionId, speechRequest);

        Assert.NotNull(response);
        Assert.Equal("Enthusiastic", response.Emotion);
        Assert.True(response.DurationMs > 0);
        Assert.NotEmpty(response.Visemes);
        Assert.Contains(response.Visemes, v => v.BlendshapeName == "JawOpen" || v.BlendshapeName == "MouthPucker");
    }

    [Fact]
    public async Task InitializeSession_UnknownPersona_FallsBackToDefaultElenaPersona()
    {
        var request = new AvatarSessionInitRequest(
            PersonaId: "persona_unknown_random",
            UserId: "usr_fallback_test"
        );

        var session = await _engine.InitializeSessionAsync(request);

        Assert.NotNull(session);
        Assert.Equal("persona_elena_sdr", session.PersonaId);
    }

    [Fact]
    public async Task InitializeSession_NullRequest_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _engine.InitializeSessionAsync(null!));
    }

    [Fact]
    public async Task TriggerPresentationSlide_ValidSession_ReturnsSlideEvent()
    {
        var session = await _engine.InitializeSessionAsync(new AvatarSessionInitRequest("persona_marcus_architect", "usr_slide"));

        var slide = await _engine.TriggerPresentationSlideAsync(
            session.SessionId,
            slideIndex: 3,
            slideTitle: "Architecture Blueprint",
            mediaUrl: "https://assets.growth-os.cloud/slides/arch_v4.png",
            caption: "Zero-trust microservices mesh");

        Assert.NotNull(slide);
        Assert.Equal(session.SessionId, slide.SessionId);
        Assert.Equal(3, slide.SlideIndex);
        Assert.Equal("Architecture Blueprint", slide.SlideTitle);
        Assert.Equal("https://assets.growth-os.cloud/slides/arch_v4.png", slide.SlideMediaUrl);
        Assert.Equal("Zero-trust microservices mesh", slide.Caption);
    }

    [Fact]
    public async Task GetSessionState_InvalidSession_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.GetSessionStateAsync("non_existent_session_id"));
    }

    [Fact]
    public async Task SynthesizeSpeech_InvalidSession_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.SynthesizeSpeechAndVisemesAsync("non_existent_session_id", new AvatarSpeechRequest("Hello")));
    }

    [Fact]
    public async Task TriggerPresentationSlide_InvalidSession_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.TriggerPresentationSlideAsync("non_existent_session_id", 1, "Title", "url", "caption"));
    }

    [Fact]
    public async Task TerminateSession_ValidSession_RemovesSessionAndSubsequentLookupThrows()
    {
        var session = await _engine.InitializeSessionAsync(new AvatarSessionInitRequest("persona_sarah_cs", "usr_term"));

        var terminated = await _engine.TerminateSessionAsync(session.SessionId);
        Assert.True(terminated);

        // Terminating again returns false
        var terminatedAgain = await _engine.TerminateSessionAsync(session.SessionId);
        Assert.False(terminatedAgain);

        // Session lookup now throws KeyNotFoundException
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _engine.GetSessionStateAsync(session.SessionId));
    }
}
