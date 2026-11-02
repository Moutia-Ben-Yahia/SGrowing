using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.AI.VideoAvatar;

public class VideoAvatarEngine
{
    private readonly ConcurrentDictionary<string, AvatarSessionState> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AvatarPersona> _personas = new();

    public VideoAvatarEngine()
    {
        SeedPersonas();
    }

    private void SeedPersonas()
    {
        _personas.Add(new AvatarPersona(
            Id: "persona_elena_sdr",
            Name: "Elena Vance",
            Title: "Lead Enterprise SDR Avatar",
            AvatarModelUrl: "https://assets.growth-os.cloud/avatars/elena_enterprise.glb",
            VoiceTone: "Enthusiastic & Confident",
            Expertise: new[] { "SaaS ROI Analysis", "B2B Objection Handling", "Interactive Demo Delivery" }
        ));

        _personas.Add(new AvatarPersona(
            Id: "persona_marcus_architect",
            Name: "Marcus Sterling",
            Title: "Principal Cloud Solutions Architect",
            AvatarModelUrl: "https://assets.growth-os.cloud/avatars/marcus_architect.glb",
            VoiceTone: "Authoritative & Empathetic",
            Expertise: new[] { "Zero-Trust Security", "Multi-Tenant Scalability", "CRM & ERP Integrations" }
        ));

        _personas.Add(new AvatarPersona(
            Id: "persona_sarah_cs",
            Name: "Sarah Chen",
            Title: "VP Customer Success & Retention",
            AvatarModelUrl: "https://assets.growth-os.cloud/avatars/sarah_cs.glb",
            VoiceTone: "Warm & Solution-Oriented",
            Expertise: new[] { "Onboarding Acceleration", "Churn Mitigation", "Executive Account Reviews" }
        ));
    }

    public Task<IReadOnlyList<AvatarPersona>> GetAvailablePersonasAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<AvatarPersona>>(_personas);
    }

    public Task<AvatarSessionState> InitializeSessionAsync(AvatarSessionInitRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var persona = _personas.FirstOrDefault(p => p.Id.Equals(request.PersonaId, StringComparison.OrdinalIgnoreCase))
            ?? _personas[0];

        var sessionId = $"avtr_sess_{Guid.NewGuid():N}";
        var sdpOffer = $"v=0\r\no=- {DateTime.UtcNow.Ticks} 2 IN IP4 127.0.0.1\r\ns=AvatarStream_{sessionId}\r\nt=0 0\r\nm=video 9 UDP/TLS/RTP/SAVPF 96\r\na=rtpmap:96 H264/90000";
        var sdpAnswer = $"v=0\r\no=- {DateTime.UtcNow.Ticks} 2 IN IP4 127.0.0.1\r\ns=AvatarStream_Answer\r\nt=0 0\r\nm=video 9 UDP/TLS/RTP/SAVPF 96\r\na=sendrecv";

        var session = new AvatarSessionState(
            SessionId: sessionId,
            PersonaId: persona.Id,
            Status: "Connected",
            SdpOffer: sdpOffer,
            SdpAnswer: sdpAnswer,
            Fps: 60,
            LatencyMs: 95,
            CreatedAtUtc: DateTime.UtcNow
        );

        _sessions[sessionId] = session;
        return Task.FromResult(session);
    }

    public Task<AvatarSpeechSynthesisResponse> SynthesizeSpeechAndVisemesAsync(string sessionId, AvatarSpeechRequest request, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Avatar session '{sessionId}' not found.");
        }

        var text = request.Text ?? "Hello, welcome to Growth OS.";
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var visemes = new List<VisemeFrame>();

        var blendshapes = new[] { "JawOpen", "MouthPucker", "MouthSmileLeft", "MouthSmileRight", "BrowInnerUp" };
        var offset = 0;

        for (var i = 0; i < words.Length; i++)
        {
            var shape = blendshapes[i % blendshapes.Length];
            var weight = 0.45 + ((i % 5) * 0.1);
            visemes.Add(new VisemeFrame(shape, Math.Round(weight, 2), offset));
            offset += 160; // ~160ms per syllable
        }

        var durationMs = Math.Max(offset, 480);
        var response = new AvatarSpeechSynthesisResponse(
            SessionId: session.SessionId,
            SpokenText: text,
            Emotion: request.Emotion,
            DurationMs: durationMs,
            Visemes: visemes
        );

        return Task.FromResult(response);
    }

    public Task<PresentationSlideEvent> TriggerPresentationSlideAsync(
        string sessionId,
        int slideIndex,
        string slideTitle,
        string mediaUrl,
        string caption,
        CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Avatar session '{sessionId}' not found.");
        }

        var slideEvent = new PresentationSlideEvent(
            SessionId: session.SessionId,
            SlideIndex: slideIndex,
            SlideTitle: slideTitle,
            SlideMediaUrl: mediaUrl,
            Caption: caption,
            TimestampUtc: DateTime.UtcNow
        );

        return Task.FromResult(slideEvent);
    }

    public Task<AvatarSessionState> GetSessionStateAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            throw new KeyNotFoundException($"Avatar session '{sessionId}' not found.");
        }

        return Task.FromResult(session);
    }

    public Task<bool> TerminateSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
