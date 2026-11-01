namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// Lifecycle orchestrator for session recording, status transitions, timeline reconstruction,
/// and frustration seeking.
/// </summary>
public interface ISessionReplayCoordinator
{
    /// <summary>
    /// Initializes recording lifecycle for a new session.
    /// </summary>
    SessionMetadata StartSession(SessionStartRequest request);

    /// <summary>
    /// Finalizes recording and transitions session to Completed status.
    /// </summary>
    SessionMetadata CompleteSession(string sessionId);

    /// <summary>
    /// Archives an existing session replay.
    /// </summary>
    SessionMetadata ArchiveSession(string sessionId);

    /// <summary>
    /// Reassembles a chronological stream of mutations, pointer interactions, and frustration markers,
    /// scaled by playback speed (0.5x, 1x, 2x, 4x, etc.).
    /// </summary>
    ReplayTimeline GetReplayTimeline(string sessionId, double playbackSpeed = 1.0);

    /// <summary>
    /// Lists all actively recording sessions for a given tenant.
    /// </summary>
    IReadOnlyList<SessionMetadata> ListActiveSessions(string tenantId);

    /// <summary>
    /// Retrieves session metadata.
    /// </summary>
    SessionMetadata? GetSession(string sessionId);

    /// <summary>
    /// Finds the closest timeline frame for seeking to a specific offset in milliseconds.
    /// </summary>
    TimelineFrame? SeekToOffset(ReplayTimeline timeline, double targetOffsetMs);
}
