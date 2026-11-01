using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// Type of DOM mutation observed in the user session.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MutationType
{
    NodeAdded,
    NodeRemoved,
    AttributeModified,
    TextContentChanged,
    ViewportResized
}

/// <summary>
/// Type of user interaction captured.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InteractionType
{
    Click,
    PointerMove,
    TouchStart,
    TouchEnd,
    Scroll
}

/// <summary>
/// Detected micro-frustration classification.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrustrationType
{
    RageClick,
    DeadClick,
    ScrollThrashing
}

/// <summary>
/// Target type for visual spatial heatmap reconstruction.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HeatmapType
{
    Click,
    Move,
    Scroll
}

/// <summary>
/// Lifecycle state of a session replay.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionStatus
{
    Recording,
    Completed,
    Archived
}

/// <summary>
/// Category of events represented in the consolidated replay timeline.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimelineEventType
{
    DomMutation,
    UserInteraction,
    FrustrationMarker,
    ViewportResize
}

/// <summary>
/// Scroll coordinate vector.
/// </summary>
public sealed record ScrollPosition(double X, double Y);

/// <summary>
/// Atomic DOM mutation operation captured at sub-millisecond precision.
/// </summary>
public sealed record DomMutationOperation(
    long SequenceNumber,
    long FrameIndex,
    DateTimeOffset Timestamp,
    MutationType Type,
    string? TargetNodePath = null,
    string? TagName = null,
    IReadOnlyDictionary<string, string?>? AttributeDeltas = null,
    string? TextContentDelta = null,
    ScrollPosition? ScrollDelta = null,
    int ViewportWidth = 0,
    int ViewportHeight = 0);

/// <summary>
/// Chronological batch of DOM mutations.
/// </summary>
public sealed record DomMutationBatch(
    string SessionId,
    long BatchSequence,
    DateTimeOffset CreatedAt,
    IReadOnlyList<DomMutationOperation> Mutations,
    int EstimatedRawBytes = 0,
    int CompressedBytes = 0);

/// <summary>
/// Network and throughput metrics for delta vector stream.
/// </summary>
public sealed record ThroughputMetrics(
    string SessionId,
    long TotalMutations,
    long TotalRawBytes,
    long TotalCompressedBytes,
    double CompressionRatio,
    double TotalDurationSeconds,
    double BandwidthKbPerSec,
    bool IsWithinTargetFootprint);

/// <summary>
/// Result of an ingestion operation.
/// </summary>
public sealed record IngestResult(
    bool Success,
    string Message,
    int ProcessedCount = 0,
    double BandwidthKbps = 0.0);

/// <summary>
/// High-frequency user interaction telemetry event.
/// </summary>
public sealed record InteractionEvent(
    long EventId,
    InteractionType Type,
    double X,
    double Y,
    int ViewportWidth,
    int ViewportHeight,
    double ScrollY,
    string? ElementSelector,
    DateTimeOffset Timestamp);

/// <summary>
/// Detected user micro-frustration event.
/// </summary>
public sealed record FrustrationEvent(
    FrustrationType Type,
    string? ElementSelector,
    double X,
    double Y,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    int OccurrenceCount,
    string Description,
    double Severity = 1.0);

/// <summary>
/// Density cell in spatial 2D heatmap.
/// </summary>
public sealed record HeatmapCell(
    int GridX,
    int GridY,
    double NormalizedX,
    double NormalizedY,
    int Count,
    double Intensity);

/// <summary>
/// Reconstructed spatial density heatmap for a specific page and interaction type.
/// </summary>
public sealed record HeatmapResult(
    string PageUrl,
    HeatmapType Type,
    int GridWidth,
    int GridHeight,
    int TotalInteractions,
    int MaxCount,
    IReadOnlyList<HeatmapCell> Cells);

/// <summary>
/// Request to start tracking a new session.
/// </summary>
public sealed record SessionStartRequest(
    string SessionId,
    string TenantId,
    string? UserId = null,
    string? Device = null,
    string? Browser = null,
    string? OperatingSystem = null,
    string? InitialPageUrl = null,
    int InitialViewportWidth = 1920,
    int InitialViewportHeight = 1080);

/// <summary>
/// Full session metadata and status.
/// </summary>
public sealed record SessionMetadata(
    string SessionId,
    string TenantId,
    string? UserId,
    string? Device,
    string? Browser,
    string? OperatingSystem,
    SessionStatus Status,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    long TotalEvents,
    bool HasRageClicks,
    string? InitialPageUrl = null);

/// <summary>
/// Consolidated timeline frame for replay playback.
/// </summary>
public sealed record TimelineFrame(
    long FrameIndex,
    DateTimeOffset Timestamp,
    double OffsetMilliseconds,
    TimelineEventType EventType,
    DomMutationOperation? Mutation = null,
    InteractionEvent? Interaction = null,
    FrustrationEvent? Frustration = null);

/// <summary>
/// Marker for quick-seeking to points of user frustration in the timeline.
/// </summary>
public sealed record FrustrationMarker(
    double OffsetMilliseconds,
    FrustrationType Type,
    string? ElementSelector,
    double X,
    double Y,
    string Description);

/// <summary>
/// Chronological replay stream with playback controls.
/// </summary>
public sealed record ReplayTimeline(
    string SessionId,
    SessionStatus Status,
    double PlaybackSpeed,
    double OriginalDurationMs,
    double ScaledDurationMs,
    IReadOnlyList<TimelineFrame> Frames,
    IReadOnlyList<FrustrationMarker> Frustrations,
    SessionMetadata Metadata);

/// <summary>
/// Unified ingest payload for session replay and telemetry.
/// </summary>
public sealed record SessionIngestPayload(
    DomMutationBatch? MutationBatch = null,
    IReadOnlyList<InteractionEvent>? Interactions = null,
    string? PageUrl = null);
