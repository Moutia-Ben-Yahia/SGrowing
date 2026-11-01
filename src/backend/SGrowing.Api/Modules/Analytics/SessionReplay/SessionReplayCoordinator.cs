using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Analytics.SessionReplay;

/// <summary>
/// Thread-safe coordinator for real-time session replay lifecycles,
/// timeline reconstruction, and playback seeking.
/// </summary>
public sealed class SessionReplayCoordinator(
    IDomMutationVectorEngine mutationEngine,
    IUserInteractionHeatmapService heatmapService) : ISessionReplayCoordinator
{
    private sealed class SessionEntry
    {
        public required SessionMetadata Metadata { get; set; }
    }

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.Ordinal);
    private readonly System.Threading.Lock _lock = new();

    /// <inheritdoc />
    public SessionMetadata StartSession(SessionStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantId);

        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var metadata = new SessionMetadata(
                SessionId: request.SessionId,
                TenantId: request.TenantId,
                UserId: request.UserId,
                Device: request.Device ?? "Desktop",
                Browser: request.Browser ?? "Chrome",
                OperatingSystem: request.OperatingSystem ?? "Windows",
                Status: SessionStatus.Recording,
                StartTime: now,
                EndTime: null,
                TotalEvents: 0,
                HasRageClicks: false,
                InitialPageUrl: request.InitialPageUrl);

            _sessions[request.SessionId] = new SessionEntry { Metadata = metadata };
            return metadata;
        }
    }

    /// <inheritdoc />
    public SessionMetadata CompleteSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
            {
                var now = DateTimeOffset.UtcNow;
                var created = new SessionMetadata(
                    SessionId: sessionId,
                    TenantId: "default",
                    UserId: null,
                    Device: "Desktop",
                    Browser: "Chrome",
                    OperatingSystem: "Windows",
                    Status: SessionStatus.Completed,
                    StartTime: now.AddMinutes(-5),
                    EndTime: now,
                    TotalEvents: 0,
                    HasRageClicks: false);

                _sessions[sessionId] = new SessionEntry { Metadata = created };
                return created;
            }

            var current = entry.Metadata;
            var frustrations = heatmapService.DetectFrustrations(sessionId);
            bool hasRageClicks = frustrations.Any(f => f.Type == FrustrationType.RageClick);
            long totalMutations = mutationEngine.GetSessionMutations(sessionId).Count;
            long totalInteractions = heatmapService.GetSessionInteractions(sessionId).Count;

            var updated = current with
            {
                Status = SessionStatus.Completed,
                EndTime = DateTimeOffset.UtcNow,
                HasRageClicks = hasRageClicks,
                TotalEvents = totalMutations + totalInteractions + frustrations.Count
            };

            entry.Metadata = updated;
            return updated;
        }
    }

    /// <inheritdoc />
    public SessionMetadata ArchiveSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
            {
                throw new KeyNotFoundException($"Session '{sessionId}' was not found.");
            }

            var updated = entry.Metadata with
            {
                Status = SessionStatus.Archived
            };

            entry.Metadata = updated;
            return updated;
        }
    }

    /// <inheritdoc />
    public ReplayTimeline GetReplayTimeline(string sessionId, double playbackSpeed = 1.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (playbackSpeed <= 0.0)
        {
            playbackSpeed = 1.0;
        }

        SessionMetadata metadata;
        lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionId, out var entry))
            {
                var now = DateTimeOffset.UtcNow;
                metadata = new SessionMetadata(
                    SessionId: sessionId,
                    TenantId: "default",
                    UserId: null,
                    Device: "Desktop",
                    Browser: "Chrome",
                    OperatingSystem: "Windows",
                    Status: SessionStatus.Recording,
                    StartTime: now,
                    EndTime: null,
                    TotalEvents: 0,
                    HasRageClicks: false);

                _sessions[sessionId] = new SessionEntry { Metadata = metadata };
            }
            else
            {
                metadata = entry.Metadata;
            }
        }

        var mutations = mutationEngine.GetSessionMutations(sessionId);
        var interactions = heatmapService.GetSessionInteractions(sessionId);
        var frustrations = heatmapService.DetectFrustrations(sessionId);

        var startTime = metadata.StartTime;
        if (mutations.Count > 0 && mutations[0].Timestamp < startTime)
        {
            startTime = mutations[0].Timestamp;
        }
        if (interactions.Count > 0 && interactions[0].Timestamp < startTime)
        {
            startTime = interactions[0].Timestamp;
        }

        var rawFrames = new List<(DateTimeOffset Ts, int Priority, TimelineEventType Type, DomMutationOperation? Mutation, InteractionEvent? Interaction, FrustrationEvent? Frustration)>(
            mutations.Count + interactions.Count + frustrations.Count);

        foreach (var m in mutations)
        {
            var type = m.Type == MutationType.ViewportResized ? TimelineEventType.ViewportResize : TimelineEventType.DomMutation;
            rawFrames.Add((m.Timestamp, 1, type, m, null, null));
        }

        foreach (var i in interactions)
        {
            rawFrames.Add((i.Timestamp, 2, TimelineEventType.UserInteraction, null, i, null));
        }

        foreach (var f in frustrations)
        {
            rawFrames.Add((f.StartTime, 3, TimelineEventType.FrustrationMarker, null, null, f));
        }

        // Sort chronologically
        rawFrames.Sort((a, b) =>
        {
            int cmp = a.Ts.CompareTo(b.Ts);
            return cmp != 0 ? cmp : a.Priority.CompareTo(b.Priority);
        });

        var frames = new List<TimelineFrame>(rawFrames.Count);
        long frameIndex = 1;

        foreach (var rf in rawFrames)
        {
            double offsetMs = Math.Max(0.0, (rf.Ts - startTime).TotalMilliseconds);
            frames.Add(new TimelineFrame(
                FrameIndex: frameIndex++,
                Timestamp: rf.Ts,
                OffsetMilliseconds: Math.Round(offsetMs, 2),
                EventType: rf.Type,
                Mutation: rf.Mutation,
                Interaction: rf.Interaction,
                Frustration: rf.Frustration));
        }

        var frustrationMarkers = frustrations.Select(f =>
        {
            double offsetMs = Math.Max(0.0, (f.StartTime - startTime).TotalMilliseconds);
            return new FrustrationMarker(
                OffsetMilliseconds: Math.Round(offsetMs, 2),
                Type: f.Type,
                ElementSelector: f.ElementSelector,
                X: f.X,
                Y: f.Y,
                Description: f.Description);
        }).ToList();

        double originalDurationMs = 0.0;
        if (frames.Count > 0)
        {
            originalDurationMs = frames[^1].OffsetMilliseconds;
        }

        if (metadata.EndTime.HasValue)
        {
            double sessionElapsed = (metadata.EndTime.Value - startTime).TotalMilliseconds;
            if (sessionElapsed > originalDurationMs)
            {
                originalDurationMs = sessionElapsed;
            }
        }

        double scaledDurationMs = Math.Round(originalDurationMs / playbackSpeed, 2);

        bool hasRageClicks = frustrations.Any(f => f.Type == FrustrationType.RageClick);
        var updatedMetadata = metadata with
        {
            TotalEvents = frames.Count,
            HasRageClicks = hasRageClicks
        };

        lock (_lock)
        {
            if (_sessions.TryGetValue(sessionId, out var entry))
            {
                entry.Metadata = updatedMetadata;
            }
        }

        return new ReplayTimeline(
            SessionId: sessionId,
            Status: updatedMetadata.Status,
            PlaybackSpeed: playbackSpeed,
            OriginalDurationMs: Math.Round(originalDurationMs, 2),
            ScaledDurationMs: scaledDurationMs,
            Frames: frames,
            Frustrations: frustrationMarkers,
            Metadata: updatedMetadata);
    }

    /// <inheritdoc />
    public IReadOnlyList<SessionMetadata> ListActiveSessions(string tenantId)
    {
        lock (_lock)
        {
            return _sessions.Values
                .Select(v => v.Metadata)
                .Where(m => (string.IsNullOrWhiteSpace(tenantId) || string.Equals(m.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                            && m.Status == SessionStatus.Recording)
                .OrderByDescending(m => m.StartTime)
                .ToList();
        }
    }

    /// <inheritdoc />
    public SessionMetadata? GetSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        lock (_lock)
        {
            return _sessions.TryGetValue(sessionId, out var entry) ? entry.Metadata : null;
        }
    }

    /// <inheritdoc />
    public TimelineFrame? SeekToOffset(ReplayTimeline timeline, double targetOffsetMs)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        if (timeline.Frames.Count == 0)
        {
            return null;
        }

        if (targetOffsetMs <= 0.0)
        {
            return timeline.Frames[0];
        }

        if (targetOffsetMs >= timeline.OriginalDurationMs)
        {
            return timeline.Frames[^1];
        }

        // Binary search for closest frame
        int low = 0;
        int high = timeline.Frames.Count - 1;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            double midOffset = timeline.Frames[mid].OffsetMilliseconds;

            if (Math.Abs(midOffset - targetOffsetMs) < 0.001)
            {
                return timeline.Frames[mid];
            }

            if (midOffset < targetOffsetMs)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        // low and high are boundaries around targetOffsetMs
        if (low >= timeline.Frames.Count) return timeline.Frames[^1];
        if (high < 0) return timeline.Frames[0];

        double diffLow = Math.Abs(timeline.Frames[low].OffsetMilliseconds - targetOffsetMs);
        double diffHigh = Math.Abs(timeline.Frames[high].OffsetMilliseconds - targetOffsetMs);

        return diffLow < diffHigh ? timeline.Frames[low] : timeline.Frames[high];
    }
}
