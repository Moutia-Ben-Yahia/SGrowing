namespace SGrowing.Api.Modules.Voice.Dereverberation;

public enum RoomAcousticType
{
    SmallTiledRoomOrKitchen,
    GlassConferenceRoom,
    CarCabinVehicle,
    OpenPlanOffice,
    TreatedStudioBooth
}

public sealed record RoomAcousticProfile(
    string ProfileId,
    string CallSessionId,
    RoomAcousticType RoomType,
    double EstimatedRt60DecaySeconds,
    double DirectToReverberantRatioDb,
    DateTime AnalyzedAtUtc
);

public sealed record DereverberationFilterEvent(
    string FilterEventId,
    string CallSessionId,
    double InputSnrDb,
    double OutputSnrDb,
    double SpectralEchoSuppressionDb,
    double ProcessingDelayMs,
    bool FilterActive
);

public sealed record DereverberationOverview(
    int ActiveProcessedStreamsCount,
    double AverageRt60SuppressedSec,
    double AverageSnrImprovementDb,
    double AsrWordErrorRateReductionPct,
    IReadOnlyList<RoomAcousticProfile> RecentProfiles,
    IReadOnlyList<DereverberationFilterEvent> RecentFilterEvents
);
