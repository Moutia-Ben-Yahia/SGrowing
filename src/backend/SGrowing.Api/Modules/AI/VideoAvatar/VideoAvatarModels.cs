namespace SGrowing.Api.Modules.AI.VideoAvatar;

public record AvatarPersona(
    string Id,
    string Name,
    string Title,
    string AvatarModelUrl,
    string VoiceTone,
    IReadOnlyList<string> Expertise
);

public record AvatarSessionInitRequest(
    string PersonaId,
    string UserId,
    string Mode = "InteractiveWebRTC",
    string Resolution = "1080p"
);

public record AvatarSessionState(
    string SessionId,
    string PersonaId,
    string Status,
    string SdpOffer,
    string SdpAnswer,
    int Fps,
    int LatencyMs,
    DateTime CreatedAtUtc
);

public record AvatarSpeechRequest(
    string Text,
    string Emotion = "Confident"
);

public record VisemeFrame(
    string BlendshapeName,
    double Weight,
    int OffsetMs
);

public record AvatarSpeechSynthesisResponse(
    string SessionId,
    string SpokenText,
    string Emotion,
    int DurationMs,
    IReadOnlyList<VisemeFrame> Visemes
);

public record PresentationSlideEvent(
    string SessionId,
    int SlideIndex,
    string SlideTitle,
    string SlideMediaUrl,
    string Caption,
    DateTime TimestampUtc
);
