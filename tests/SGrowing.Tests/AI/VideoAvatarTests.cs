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
}
