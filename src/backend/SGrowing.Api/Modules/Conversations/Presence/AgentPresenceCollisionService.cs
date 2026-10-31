using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Conversations.Presence;

/// <summary>
/// Presence status of an omnichannel customer service agent.
/// </summary>
public enum AgentPresenceState
{
    Online = 0,
    ActiveOnTicket = 1,
    ViewingTicket = 2,
    ComposingReply = 3,
    Idle = 4,
    Offline = 5
}

/// <summary>
/// Concurrency lock posture on an omnichannel conversation ticket.
/// </summary>
public enum CollisionLockState
{
    Unlocked = 0,
    SoftViewing = 1,
    HardComposingLock = 2
}

/// <summary>
/// Reason why a ticket collision lock was released.
/// </summary>
public enum LockReleaseReason
{
    ReplySubmitted = 0,
    NavigatedAway = 1,
    LeaseExpired = 2,
    AdminOverride = 3,
    AgentOffline = 4,
    ExplicitUnlock = 5
}

public record AgentHeartbeatRequest(
    Guid AgentId,
    string? AgentName,
    string? AgentEmail,
    Guid? TicketId,
    AgentPresenceState? State,
    bool IsTyping = false,
    string? DraftSnippet = null
);

public record AcquireLockRequest(
    Guid AgentId,
    string? AgentName,
    string? DraftSnippet = null
);

public record ReleaseLockRequest(
    Guid AgentId,
    LockReleaseReason Reason = LockReleaseReason.ExplicitUnlock,
    bool IsAdminOverride = false
);

public record AgentPresenceSession
{
    public Guid AgentId { get; init; }
    public string AgentName { get; set; } = string.Empty;
    public string AgentEmail { get; set; } = string.Empty;
    public string Role { get; set; } = "Support Operator";
    public AgentPresenceState State { get; set; } = AgentPresenceState.Online;
    public Guid? CurrentTicketId { get; set; }
    public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
    public DateTime StateChangedUtc { get; set; } = DateTime.UtcNow;
    public int ActiveWorkload { get; set; }
    public bool IsTyping { get; set; }
    public string? DraftSnippet { get; set; }
}

public record TicketViewerInfo(
    Guid AgentId,
    string AgentName,
    DateTime ViewingSinceUtc,
    DateTime LastHeartbeatUtc,
    AgentPresenceState State,
    bool IsTyping,
    string? DraftSnippet
);

public record TypingBeaconDto(
    Guid TicketId,
    Guid AgentId,
    string AgentName,
    bool IsTyping,
    string? DraftSnippetPreview,
    DateTime StartedTypingAtUtc,
    DateTime LastTypingPingUtc,
    int LeaseRemainingSeconds
);

public record TicketCollisionStatusResponse(
    Guid TicketId,
    string TicketTitle,
    CollisionLockState LockState,
    Guid? LockedByAgentId,
    string? LockedByAgentName,
    DateTime? LockAcquiredAtUtc,
    DateTime? LockExpiresAtUtc,
    int RemainingLeaseSeconds,
    bool IsBlocked,
    string? WarningMessage,
    List<TicketViewerInfo> Viewers,
    TypingBeaconDto? TypingBeacon
);

public record LockAcquisitionResult(
    bool Success,
    Guid TicketId,
    CollisionLockState LockState,
    Guid? LockedByAgentId,
    string? LockedByAgentName,
    string? WarningMessage,
    DateTime? LockExpiresAtUtc,
    int RemainingLeaseSeconds,
    List<TicketViewerInfo> Viewers
);

public record LockReleaseResult(
    bool Success,
    Guid TicketId,
    CollisionLockState CurrentLockState,
    Guid? ReleasedByAgentId,
    LockReleaseReason Reason,
    string Message
);

public record TicketHandoffEvent(
    Guid EventId,
    Guid TicketId,
    string TicketTitle,
    string CustomerName,
    Guid? PreviousAgentId,
    string PreviousAgentName,
    Guid NewAgentId,
    string NewAgentName,
    string Reason,
    DateTime TimestampUtc
);

public record TeamStatusRosterDto(
    List<AgentPresenceSession> Agents,
    int TotalAgents,
    int OnlineAgents,
    int ActiveOnTickets,
    int IdleAgents,
    int OfflineAgents,
    int TotalActiveWorkload,
    DateTime GeneratedAtUtc
);

public record PresenceMetricsDto(
    int TotalOnlineAgents,
    int ActiveTicketsUnderCollisionLock,
    int TotalPreventedCollisions,
    int TotalAutoReassignments,
    double AverageReplyTimeSeconds,
    int ActiveViewersCount,
    int TotalMonitoredTickets,
    DateTime GeneratedAtUtc
);

public record AutoHandoffSweepResult(
    int EvaluatedCount,
    int ReassignedCount,
    List<TicketHandoffEvent> HandoffEvents,
    DateTime SweepCompletedAtUtc
);

public class TicketPresenceState
{
    public Guid TicketId { get; init; }
    public string TicketTitle { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public Guid? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public bool CustomerMessagePending { get; set; } = true;
    public DateTime? LastCustomerMessageAtUtc { get; set; } = DateTime.UtcNow;
    public CollisionLockState LockState { get; set; } = CollisionLockState.Unlocked;
    public Guid? LockedByAgentId { get; set; }
    public string? LockedByAgentName { get; set; }
    public DateTime? LockAcquiredAtUtc { get; set; }
    public DateTime? LockExpiresAtUtc { get; set; }
    public string? LockDraftSnippet { get; set; }
    public DateTime? StartedTypingAtUtc { get; set; }
    public DateTime? LastTypingPingUtc { get; set; }
    public ConcurrentDictionary<Guid, TicketViewerInfo> Viewers { get; } = new();
    public object LockSync { get; } = new();
}

public interface IAgentPresenceCollisionService
{
    Task<AgentPresenceSession> PingHeartbeatAsync(AgentHeartbeatRequest request, DateTime? asOfUtc = null, CancellationToken ct = default);
    Task<LockAcquisitionResult> AcquireCollisionLockAsync(Guid ticketId, AcquireLockRequest request, DateTime? asOfUtc = null, CancellationToken ct = default);
    Task<LockReleaseResult> ReleaseCollisionLockAsync(Guid ticketId, ReleaseLockRequest request, DateTime? asOfUtc = null, CancellationToken ct = default);
    Task<TicketCollisionStatusResponse> GetTicketCollisionStatusAsync(Guid ticketId, Guid? queryingAgentId = null, DateTime? asOfUtc = null, CancellationToken ct = default);
    Task<List<TicketViewerInfo>> GetTicketViewersAsync(Guid ticketId, DateTime? asOfUtc = null, CancellationToken ct = default);
    Task<TeamStatusRosterDto> GetTeamStatusRosterAsync(DateTime? asOfUtc = null, CancellationToken ct = default);
    Task<AutoHandoffSweepResult> ExecuteAutoHandoffSweepAsync(TimeSpan? abandonmentThreshold = null, DateTime? asOfUtc = null, CancellationToken ct = default);
    Task<PresenceMetricsDto> GetMetricsAsync(DateTime? asOfUtc = null, CancellationToken ct = default);
    Task<List<TicketHandoffEvent>> GetHandoffAuditEventsAsync(int limit = 50, CancellationToken ct = default);
    Task<List<TicketPresenceState>> GetAllTicketsAsync(CancellationToken ct = default);
    void RefreshAgentStates(DateTime? asOfUtc = null);
    void RecordReplyCompletion(double replyDurationSeconds);
    void ResetToSeedData();
}

public class AgentPresenceCollisionService : IAgentPresenceCollisionService
{
    private readonly ConcurrentDictionary<Guid, AgentPresenceSession> _agents = new();
    private readonly ConcurrentDictionary<Guid, TicketPresenceState> _tickets = new();
    private readonly ConcurrentQueue<TicketHandoffEvent> _handoffEvents = new();
    private readonly ConcurrentBag<double> _replyDurations = new();

    private int _preventedCollisionsCount = 0;
    private int _reassignmentCount = 0;
    private int _roundRobinCounter = 0;

    public const int HeartbeatLeaseSeconds = 15;
    public const int IdleThresholdSeconds = 30;
    public const int OfflineThresholdSeconds = 60;
    public const int LockLeaseSeconds = 60;
    public static readonly TimeSpan DefaultAbandonmentThreshold = TimeSpan.FromMinutes(5);

    public AgentPresenceCollisionService()
    {
        SeedDefaultData();
    }

    public Task<AgentPresenceSession> PingHeartbeatAsync(AgentHeartbeatRequest request, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;

        var session = _agents.AddOrUpdate(
            request.AgentId,
            id => new AgentPresenceSession
            {
                AgentId = id,
                AgentName = request.AgentName ?? $"Operator {id.ToString()[..4]}",
                AgentEmail = request.AgentEmail ?? $"operator.{id.ToString()[..4]}@sgrowing.io",
                State = request.State ?? (request.TicketId.HasValue
                    ? (request.IsTyping ? AgentPresenceState.ComposingReply : AgentPresenceState.ViewingTicket)
                    : AgentPresenceState.Online),
                CurrentTicketId = request.TicketId,
                LastHeartbeatUtc = now,
                StateChangedUtc = now,
                ActiveWorkload = 0,
                IsTyping = request.IsTyping,
                DraftSnippet = request.DraftSnippet
            },
            (id, existing) =>
            {
                var previousState = existing.State;
                var newState = request.State ?? (request.TicketId.HasValue
                    ? (request.IsTyping ? AgentPresenceState.ComposingReply : AgentPresenceState.ViewingTicket)
                    : (existing.State == AgentPresenceState.Idle || existing.State == AgentPresenceState.Offline
                        ? AgentPresenceState.Online
                        : existing.State));

                if (request.AgentName != null) existing.AgentName = request.AgentName;
                if (request.AgentEmail != null) existing.AgentEmail = request.AgentEmail;

                existing.LastHeartbeatUtc = now;
                if (previousState != newState)
                {
                    existing.State = newState;
                    existing.StateChangedUtc = now;
                }

                var previousTicketId = existing.CurrentTicketId;
                existing.CurrentTicketId = request.TicketId;
                existing.IsTyping = request.IsTyping;
                existing.DraftSnippet = request.DraftSnippet;

                // Handle ticket transition if navigating away from previous ticket
                if (previousTicketId.HasValue && previousTicketId != request.TicketId)
                {
                    RemoveViewerFromTicketInternal(previousTicketId.Value, existing.AgentId, LockReleaseReason.NavigatedAway, now);
                }

                return existing;
            });

        // If currently viewing a ticket, register or update viewer in that ticket
        if (request.TicketId.HasValue)
        {
            var ticket = GetOrCreateTicket(request.TicketId.Value);
            lock (ticket.LockSync)
            {
                var viewer = new TicketViewerInfo(
                    session.AgentId,
                    session.AgentName,
                    ticket.Viewers.TryGetValue(session.AgentId, out var existingV) ? existingV.ViewingSinceUtc : now,
                    now,
                    session.State,
                    request.IsTyping,
                    request.DraftSnippet
                );
                ticket.Viewers[session.AgentId] = viewer;

                // Update lock lease if agent is holding hard composing lock
                if (ticket.LockedByAgentId == session.AgentId && ticket.LockState == CollisionLockState.HardComposingLock)
                {
                    ticket.LockExpiresAtUtc = now.AddSeconds(LockLeaseSeconds);
                    ticket.LastTypingPingUtc = now;
                    if (request.DraftSnippet != null)
                    {
                        ticket.LockDraftSnippet = request.DraftSnippet;
                    }
                }
                else if (ticket.LockState == CollisionLockState.Unlocked && ticket.Viewers.Count > 0)
                {
                    ticket.LockState = CollisionLockState.SoftViewing;
                }
            }
        }

        // Run auto-expiration check
        RefreshAgentStates(now);

        return Task.FromResult(session);
    }

    public Task<LockAcquisitionResult> AcquireCollisionLockAsync(Guid ticketId, AcquireLockRequest request, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        var ticket = GetOrCreateTicket(ticketId);

        lock (ticket.LockSync)
        {
            // Prune expired viewers older than OfflineThresholdSeconds
            foreach (var kvp in ticket.Viewers)
            {
                if ((now - kvp.Value.LastHeartbeatUtc).TotalSeconds > OfflineThresholdSeconds)
                {
                    ticket.Viewers.TryRemove(kvp.Key, out _);
                }
            }

            // Expire previous lock if lease elapsed
            if (ticket.LockState == CollisionLockState.HardComposingLock && ticket.LockExpiresAtUtc.HasValue && now >= ticket.LockExpiresAtUtc.Value)
            {
                ticket.LockState = ticket.Viewers.Count > 0 ? CollisionLockState.SoftViewing : CollisionLockState.Unlocked;
                ticket.LockedByAgentId = null;
                ticket.LockedByAgentName = null;
                ticket.LockDraftSnippet = null;
                ticket.LockAcquiredAtUtc = null;
                ticket.LockExpiresAtUtc = null;
            }

            // Case 1: Same agent re-acquiring or renewing their lock
            if (ticket.LockState == CollisionLockState.HardComposingLock && ticket.LockedByAgentId == request.AgentId)
            {
                ticket.LockExpiresAtUtc = now.AddSeconds(LockLeaseSeconds);
                ticket.LastTypingPingUtc = now;
                if (request.DraftSnippet != null)
                {
                    ticket.LockDraftSnippet = request.DraftSnippet;
                }

                UpdateAgentState(request.AgentId, AgentPresenceState.ComposingReply, ticketId, isTyping: true, request.DraftSnippet, now);

                var viewersList = ticket.Viewers.Values.ToList();
                return Task.FromResult(new LockAcquisitionResult(
                    Success: true,
                    TicketId: ticketId,
                    LockState: CollisionLockState.HardComposingLock,
                    LockedByAgentId: request.AgentId,
                    LockedByAgentName: ticket.LockedByAgentName,
                    WarningMessage: null,
                    LockExpiresAtUtc: ticket.LockExpiresAtUtc,
                    RemainingLeaseSeconds: (int)Math.Max(0, (ticket.LockExpiresAtUtc.Value - now).TotalSeconds),
                    Viewers: viewersList
                ));
            }

            // Case 2: Another agent already holds HardComposingLock -> BLOCK COLLISION!
            if (ticket.LockState == CollisionLockState.HardComposingLock && ticket.LockedByAgentId != request.AgentId)
            {
                Interlocked.Increment(ref _preventedCollisionsCount);

                var remainingSec = ticket.LockExpiresAtUtc.HasValue ? (int)Math.Max(0, (ticket.LockExpiresAtUtc.Value - now).TotalSeconds) : 0;
                var blockerName = ticket.LockedByAgentName ?? "Another agent";
                var warning = $"⚠️ {blockerName} is actively replying to this customer. Input is locked to prevent duplicate responses.";

                // Still ensure the requesting agent is in viewers list as soft viewer
                var agentName = request.AgentName ?? (ticket.Viewers.TryGetValue(request.AgentId, out var v) ? v.AgentName : $"Operator {request.AgentId.ToString()[..4]}");
                ticket.Viewers[request.AgentId] = new TicketViewerInfo(
                    request.AgentId,
                    agentName,
                    ticket.Viewers.TryGetValue(request.AgentId, out var existingV) ? existingV.ViewingSinceUtc : now,
                    now,
                    AgentPresenceState.ViewingTicket,
                    IsTyping: false,
                    DraftSnippet: null
                );

                UpdateAgentState(request.AgentId, AgentPresenceState.ViewingTicket, ticketId, isTyping: false, null, now);

                return Task.FromResult(new LockAcquisitionResult(
                    Success: false,
                    TicketId: ticketId,
                    LockState: CollisionLockState.HardComposingLock,
                    LockedByAgentId: ticket.LockedByAgentId,
                    LockedByAgentName: ticket.LockedByAgentName,
                    WarningMessage: warning,
                    LockExpiresAtUtc: ticket.LockExpiresAtUtc,
                    RemainingLeaseSeconds: remainingSec,
                    Viewers: ticket.Viewers.Values.ToList()
                ));
            }

            // Case 3: Lock is Unlocked or SoftViewing -> Grant HardComposingLock to Agent
            var grantingAgentName = request.AgentName
                ?? (_agents.TryGetValue(request.AgentId, out var ag) ? ag.AgentName : $"Operator {request.AgentId.ToString()[..4]}");

            ticket.LockState = CollisionLockState.HardComposingLock;
            ticket.LockedByAgentId = request.AgentId;
            ticket.LockedByAgentName = grantingAgentName;
            ticket.LockAcquiredAtUtc = now;
            ticket.LockExpiresAtUtc = now.AddSeconds(LockLeaseSeconds);
            ticket.LockDraftSnippet = request.DraftSnippet;
            ticket.StartedTypingAtUtc = now;
            ticket.LastTypingPingUtc = now;

            ticket.Viewers[request.AgentId] = new TicketViewerInfo(
                request.AgentId,
                grantingAgentName,
                ticket.Viewers.TryGetValue(request.AgentId, out var exV) ? exV.ViewingSinceUtc : now,
                now,
                AgentPresenceState.ComposingReply,
                IsTyping: true,
                DraftSnippet: request.DraftSnippet
            );

            UpdateAgentState(request.AgentId, AgentPresenceState.ComposingReply, ticketId, isTyping: true, request.DraftSnippet, now);

            return Task.FromResult(new LockAcquisitionResult(
                Success: true,
                TicketId: ticketId,
                LockState: CollisionLockState.HardComposingLock,
                LockedByAgentId: request.AgentId,
                LockedByAgentName: grantingAgentName,
                WarningMessage: null,
                LockExpiresAtUtc: ticket.LockExpiresAtUtc,
                RemainingLeaseSeconds: LockLeaseSeconds,
                Viewers: ticket.Viewers.Values.ToList()
            ));
        }
    }

    public Task<LockReleaseResult> ReleaseCollisionLockAsync(Guid ticketId, ReleaseLockRequest request, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        if (!_tickets.TryGetValue(ticketId, out var ticket))
        {
            return Task.FromResult(new LockReleaseResult(
                Success: false,
                TicketId: ticketId,
                CurrentLockState: CollisionLockState.Unlocked,
                ReleasedByAgentId: request.AgentId,
                Reason: request.Reason,
                Message: "Ticket not found."
            ));
        }

        lock (ticket.LockSync)
        {
            // Verify if caller holds the lock or is an admin override
            if (ticket.LockState == CollisionLockState.HardComposingLock)
            {
                if (ticket.LockedByAgentId != request.AgentId && !request.IsAdminOverride)
                {
                    return Task.FromResult(new LockReleaseResult(
                        Success: false,
                        TicketId: ticketId,
                        CurrentLockState: ticket.LockState,
                        ReleasedByAgentId: request.AgentId,
                        Reason: request.Reason,
                        Message: "Cannot release lock owned by another operator without administrative override."
                    ));
                }

                // If reply was submitted, record simulated reply duration
                if (request.Reason == LockReleaseReason.ReplySubmitted && ticket.LockAcquiredAtUtc.HasValue)
                {
                    var durationSec = (now - ticket.LockAcquiredAtUtc.Value).TotalSeconds;
                    RecordReplyCompletion(Math.Max(1.0, durationSec));
                    ticket.CustomerMessagePending = false;
                }

                // Reset lock details
                ticket.LockedByAgentId = null;
                ticket.LockedByAgentName = null;
                ticket.LockDraftSnippet = null;
                ticket.LockAcquiredAtUtc = null;
                ticket.LockExpiresAtUtc = null;
                ticket.StartedTypingAtUtc = null;
                ticket.LastTypingPingUtc = null;

                // If agent navigated away, remove from viewers
                if (request.Reason == LockReleaseReason.NavigatedAway)
                {
                    ticket.Viewers.TryRemove(request.AgentId, out _);
                }
                else if (ticket.Viewers.TryGetValue(request.AgentId, out var v))
                {
                    ticket.Viewers[request.AgentId] = v with
                    {
                        State = AgentPresenceState.ViewingTicket,
                        IsTyping = false,
                        DraftSnippet = null
                    };
                }

                ticket.LockState = ticket.Viewers.Count > 0 ? CollisionLockState.SoftViewing : CollisionLockState.Unlocked;

                // Update agent state
                if (request.Reason == LockReleaseReason.NavigatedAway)
                {
                    UpdateAgentState(request.AgentId, AgentPresenceState.Online, null, isTyping: false, null, now);
                }
                else
                {
                    UpdateAgentState(request.AgentId, AgentPresenceState.ViewingTicket, ticketId, isTyping: false, null, now);
                }

                return Task.FromResult(new LockReleaseResult(
                    Success: true,
                    TicketId: ticketId,
                    CurrentLockState: ticket.LockState,
                    ReleasedByAgentId: request.AgentId,
                    Reason: request.Reason,
                    Message: $"Lock released successfully via {request.Reason}."
                ));
            }

            // If not HardComposingLock but agent navigated away, remove them from viewers
            if (request.Reason == LockReleaseReason.NavigatedAway)
            {
                ticket.Viewers.TryRemove(request.AgentId, out _);
                ticket.LockState = ticket.Viewers.Count > 0 ? CollisionLockState.SoftViewing : CollisionLockState.Unlocked;
                UpdateAgentState(request.AgentId, AgentPresenceState.Online, null, isTyping: false, null, now);

                return Task.FromResult(new LockReleaseResult(
                    Success: true,
                    TicketId: ticketId,
                    CurrentLockState: ticket.LockState,
                    ReleasedByAgentId: request.AgentId,
                    Reason: request.Reason,
                    Message: "Viewer removed and ticket state updated on navigation away."
                ));
            }

            // Already unlocked
            return Task.FromResult(new LockReleaseResult(
                Success: true,
                TicketId: ticketId,
                CurrentLockState: ticket.LockState,
                ReleasedByAgentId: request.AgentId,
                Reason: request.Reason,
                Message: "Ticket was not under hard composing lock."
            ));
        }
    }

    public Task<TicketCollisionStatusResponse> GetTicketCollisionStatusAsync(Guid ticketId, Guid? queryingAgentId = null, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        var ticket = GetOrCreateTicket(ticketId);

        lock (ticket.LockSync)
        {
            // Prune expired viewers older than OfflineThresholdSeconds
            foreach (var kvp in ticket.Viewers)
            {
                if ((now - kvp.Value.LastHeartbeatUtc).TotalSeconds > OfflineThresholdSeconds)
                {
                    ticket.Viewers.TryRemove(kvp.Key, out _);
                }
            }

            // Check lease expiration
            if (ticket.LockState == CollisionLockState.HardComposingLock && ticket.LockExpiresAtUtc.HasValue && now >= ticket.LockExpiresAtUtc.Value)
            {
                ticket.LockState = ticket.Viewers.Count > 0 ? CollisionLockState.SoftViewing : CollisionLockState.Unlocked;
                ticket.LockedByAgentId = null;
                ticket.LockedByAgentName = null;
                ticket.LockDraftSnippet = null;
                ticket.LockAcquiredAtUtc = null;
                ticket.LockExpiresAtUtc = null;
                ticket.StartedTypingAtUtc = null;
            }

            var viewersList = ticket.Viewers.Values
                .Where(v => (now - v.LastHeartbeatUtc).TotalSeconds <= OfflineThresholdSeconds)
                .OrderBy(v => v.ViewingSinceUtc)
                .ToList();

            var remainingSec = ticket.LockExpiresAtUtc.HasValue ? (int)Math.Max(0, (ticket.LockExpiresAtUtc.Value - now).TotalSeconds) : 0;
            var isBlocked = ticket.LockState == CollisionLockState.HardComposingLock && queryingAgentId.HasValue && ticket.LockedByAgentId != queryingAgentId.Value;

            string? warning = null;
            if (ticket.LockState == CollisionLockState.HardComposingLock && ticket.LockedByAgentName != null)
            {
                warning = isBlocked
                    ? $"⚠️ {ticket.LockedByAgentName} is actively replying to this customer. Input is locked to prevent duplicate responses."
                    : $"You hold the active composing lock ({remainingSec}s remaining lease).";
            }
            else if (viewersList.Count > 1)
            {
                var others = string.Join(", ", viewersList.Where(v => !queryingAgentId.HasValue || v.AgentId != queryingAgentId.Value).Select(v => v.AgentName));
                warning = $"ℹ️ Multiple operators currently viewing this ticket ({others}).";
            }

            TypingBeaconDto? beacon = null;
            if (ticket.LockState == CollisionLockState.HardComposingLock && ticket.LockedByAgentId.HasValue)
            {
                beacon = new TypingBeaconDto(
                    ticketId,
                    ticket.LockedByAgentId.Value,
                    ticket.LockedByAgentName ?? "Operator",
                    IsTyping: true,
                    DraftSnippetPreview: ticket.LockDraftSnippet,
                    StartedTypingAtUtc: ticket.StartedTypingAtUtc ?? now,
                    LastTypingPingUtc: ticket.LastTypingPingUtc ?? now,
                    LeaseRemainingSeconds: remainingSec
                );
            }

            return Task.FromResult(new TicketCollisionStatusResponse(
                ticket.TicketId,
                ticket.TicketTitle,
                ticket.LockState,
                ticket.LockedByAgentId,
                ticket.LockedByAgentName,
                ticket.LockAcquiredAtUtc,
                ticket.LockExpiresAtUtc,
                remainingSec,
                isBlocked,
                warning,
                viewersList,
                beacon
            ));
        }
    }

    public Task<List<TicketViewerInfo>> GetTicketViewersAsync(Guid ticketId, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        if (!_tickets.TryGetValue(ticketId, out var ticket))
        {
            return Task.FromResult(new List<TicketViewerInfo>());
        }

        lock (ticket.LockSync)
        {
            var active = ticket.Viewers.Values
                .Where(v => (now - v.LastHeartbeatUtc).TotalSeconds <= OfflineThresholdSeconds)
                .OrderBy(v => v.ViewingSinceUtc)
                .ToList();
            return Task.FromResult(active);
        }
    }

    public Task<TeamStatusRosterDto> GetTeamStatusRosterAsync(DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        RefreshAgentStates(now);

        var list = _agents.Values
            .OrderByDescending(a => a.State == AgentPresenceState.ComposingReply)
            .ThenByDescending(a => a.State == AgentPresenceState.Online)
            .ThenBy(a => a.AgentName)
            .ToList();

        var total = list.Count;
        var online = list.Count(a => a.State is AgentPresenceState.Online or AgentPresenceState.ActiveOnTicket or AgentPresenceState.ViewingTicket or AgentPresenceState.ComposingReply);
        var activeOnTickets = list.Count(a => a.State is AgentPresenceState.ActiveOnTicket or AgentPresenceState.ViewingTicket or AgentPresenceState.ComposingReply);
        var idle = list.Count(a => a.State == AgentPresenceState.Idle);
        var offline = list.Count(a => a.State == AgentPresenceState.Offline);
        var totalWorkload = list.Sum(a => a.ActiveWorkload);

        return Task.FromResult(new TeamStatusRosterDto(
            list,
            total,
            online,
            activeOnTickets,
            idle,
            offline,
            totalWorkload,
            now
        ));
    }

    public Task<AutoHandoffSweepResult> ExecuteAutoHandoffSweepAsync(TimeSpan? abandonmentThreshold = null, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        var threshold = abandonmentThreshold ?? DefaultAbandonmentThreshold;

        RefreshAgentStates(now);

        var evaluatedCount = 0;
        var reassignedCount = 0;
        var newEvents = new List<TicketHandoffEvent>();

        // Eligible available operators for round-robin workload balancing
        var eligibleOperators = _agents.Values
            .Where(a => a.State is AgentPresenceState.Online or AgentPresenceState.ActiveOnTicket)
            .ToList();

        foreach (var ticket in _tickets.Values)
        {
            if (!ticket.AssignedAgentId.HasValue || !ticket.CustomerMessagePending)
                continue;

            evaluatedCount++;

            if (!_agents.TryGetValue(ticket.AssignedAgentId.Value, out var assignedAgent))
                continue;

            // Check if operator is Idle or Offline for > threshold
            var isAbandonedState = assignedAgent.State is AgentPresenceState.Idle or AgentPresenceState.Offline;
            var elapsedInactivity = now - (assignedAgent.State == AgentPresenceState.Idle ? assignedAgent.StateChangedUtc : assignedAgent.LastHeartbeatUtc);

            if (isAbandonedState && elapsedInactivity >= threshold)
            {
                // Must select next available online operator with lowest active workload
                var candidates = eligibleOperators
                    .Where(a => a.AgentId != assignedAgent.AgentId)
                    .OrderBy(a => a.ActiveWorkload)
                    .ToList();

                if (candidates.Count > 0)
                {
                    // Workload balancing with round-robin tie breaking
                    var minWorkload = candidates[0].ActiveWorkload;
                    var minTied = candidates.Where(c => c.ActiveWorkload == minWorkload).ToList();
                    var selected = minTied[Math.Abs(Interlocked.Increment(ref _roundRobinCounter)) % minTied.Count];

                    lock (ticket.LockSync)
                    {
                        var prevId = assignedAgent.AgentId;
                        var prevName = assignedAgent.AgentName;

                        // Release any previous lock held by abandoned agent
                        if (ticket.LockedByAgentId == prevId)
                        {
                            ticket.LockedByAgentId = null;
                            ticket.LockedByAgentName = null;
                            ticket.LockState = ticket.Viewers.Count > 0 ? CollisionLockState.SoftViewing : CollisionLockState.Unlocked;
                            ticket.LockExpiresAtUtc = null;
                            ticket.LockAcquiredAtUtc = null;
                        }

                        // Reassign
                        ticket.AssignedAgentId = selected.AgentId;
                        ticket.AssignedAgentName = selected.AgentName;

                        // Adjust workloads
                        assignedAgent.ActiveWorkload = Math.Max(0, assignedAgent.ActiveWorkload - 1);
                        selected.ActiveWorkload++;

                        var evt = new TicketHandoffEvent(
                            Guid.NewGuid(),
                            ticket.TicketId,
                            ticket.TicketTitle,
                            ticket.CustomerName,
                            prevId,
                            prevName,
                            selected.AgentId,
                            selected.AgentName,
                            $"Operator {prevName} inactive for {Math.Round(elapsedInactivity.TotalMinutes, 1)}m (> {threshold.TotalMinutes}m SLA handoff threshold). Reassigned to lowest workload online operator {selected.AgentName} (load: {selected.ActiveWorkload}).",
                            now
                        );

                        _handoffEvents.Enqueue(evt);
                        newEvents.Add(evt);
                        reassignedCount++;
                        Interlocked.Increment(ref _reassignmentCount);
                    }
                }
            }
        }

        return Task.FromResult(new AutoHandoffSweepResult(
            evaluatedCount,
            reassignedCount,
            newEvents,
            now
        ));
    }

    public Task<PresenceMetricsDto> GetMetricsAsync(DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var now = asOfUtc ?? DateTime.UtcNow;
        RefreshAgentStates(now);

        var onlineAgents = _agents.Values.Count(a => a.State is AgentPresenceState.Online or AgentPresenceState.ActiveOnTicket or AgentPresenceState.ViewingTicket or AgentPresenceState.ComposingReply);
        
        var lockedTickets = _tickets.Values.Count(t =>
        {
            lock (t.LockSync)
            {
                return t.LockState == CollisionLockState.HardComposingLock &&
                       t.LockExpiresAtUtc.HasValue &&
                       t.LockExpiresAtUtc.Value > now;
            }
        });

        var activeViewers = _tickets.Values.Sum(t =>
        {
            lock (t.LockSync)
            {
                return t.Viewers.Values.Count(v => (now - v.LastHeartbeatUtc).TotalSeconds <= OfflineThresholdSeconds);
            }
        });

        var avgReplyTime = _replyDurations.IsEmpty ? 42.5 : Math.Round(_replyDurations.Average(), 1);

        return Task.FromResult(new PresenceMetricsDto(
            onlineAgents,
            lockedTickets,
            _preventedCollisionsCount,
            _reassignmentCount,
            avgReplyTime,
            activeViewers,
            _tickets.Count,
            now
        ));
    }

    public Task<List<TicketHandoffEvent>> GetHandoffAuditEventsAsync(int limit = 50, CancellationToken ct = default)
    {
        var list = _handoffEvents.OrderByDescending(e => e.TimestampUtc).Take(limit).ToList();
        return Task.FromResult(list);
    }

    public Task<List<TicketPresenceState>> GetAllTicketsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_tickets.Values.OrderBy(t => t.TicketTitle).ToList());
    }

    public void RecordReplyCompletion(double replyDurationSeconds)
    {
        _replyDurations.Add(replyDurationSeconds);
    }

    public void RefreshAgentStates(DateTime? asOfUtc = null)
    {
        var now = asOfUtc ?? DateTime.UtcNow;

        foreach (var agent in _agents.Values)
        {
            var elapsed = (now - agent.LastHeartbeatUtc).TotalSeconds;

            if (elapsed > OfflineThresholdSeconds)
            {
                if (agent.State != AgentPresenceState.Offline)
                {
                    agent.State = AgentPresenceState.Offline;
                    agent.StateChangedUtc = now;
                    agent.IsTyping = false;
                    agent.DraftSnippet = null;

                    // Clear any viewing/locking on tickets
                    if (agent.CurrentTicketId.HasValue)
                    {
                        RemoveViewerFromTicketInternal(agent.CurrentTicketId.Value, agent.AgentId, LockReleaseReason.AgentOffline, now);
                        agent.CurrentTicketId = null;
                    }
                }
            }
            else if (elapsed > IdleThresholdSeconds)
            {
                if (agent.State is AgentPresenceState.Online or AgentPresenceState.ViewingTicket or AgentPresenceState.ComposingReply or AgentPresenceState.ActiveOnTicket)
                {
                    agent.State = AgentPresenceState.Idle;
                    agent.StateChangedUtc = now;
                    agent.IsTyping = false;
                }
            }
        }
    }

    private void RemoveViewerFromTicketInternal(Guid ticketId, Guid agentId, LockReleaseReason reason, DateTime now)
    {
        if (!_tickets.TryGetValue(ticketId, out var ticket)) return;

        lock (ticket.LockSync)
        {
            ticket.Viewers.TryRemove(agentId, out _);

            // Release hard composing lock if held by this agent
            if (ticket.LockedByAgentId == agentId && ticket.LockState == CollisionLockState.HardComposingLock)
            {
                ticket.LockState = ticket.Viewers.Count > 0 ? CollisionLockState.SoftViewing : CollisionLockState.Unlocked;
                ticket.LockedByAgentId = null;
                ticket.LockedByAgentName = null;
                ticket.LockDraftSnippet = null;
                ticket.LockAcquiredAtUtc = null;
                ticket.LockExpiresAtUtc = null;
                ticket.StartedTypingAtUtc = null;
            }
            else if (ticket.LockState == CollisionLockState.SoftViewing && ticket.Viewers.IsEmpty)
            {
                ticket.LockState = CollisionLockState.Unlocked;
            }
        }
    }

    private void UpdateAgentState(Guid agentId, AgentPresenceState newState, Guid? ticketId, bool isTyping, string? draftSnippet, DateTime now)
    {
        _agents.AddOrUpdate(
            agentId,
            id => new AgentPresenceSession
            {
                AgentId = id,
                AgentName = $"Operator {id.ToString()[..4]}",
                AgentEmail = $"operator.{id.ToString()[..4]}@sgrowing.io",
                State = newState,
                CurrentTicketId = ticketId,
                LastHeartbeatUtc = now,
                StateChangedUtc = now,
                ActiveWorkload = 0,
                IsTyping = isTyping,
                DraftSnippet = draftSnippet
            },
            (id, existing) =>
            {
                if (existing.State != newState)
                {
                    existing.State = newState;
                    existing.StateChangedUtc = now;
                }
                existing.CurrentTicketId = ticketId;
                existing.IsTyping = isTyping;
                if (draftSnippet != null) existing.DraftSnippet = draftSnippet;
                return existing;
            });
    }

    private TicketPresenceState GetOrCreateTicket(Guid ticketId)
    {
        return _tickets.GetOrAdd(ticketId, id => new TicketPresenceState
        {
            TicketId = id,
            TicketTitle = $"Ticket #{id.ToString()[..6]}",
            CustomerName = "Enterprise Customer",
            Priority = "High",
            CustomerMessagePending = true,
            LastCustomerMessageAtUtc = DateTime.UtcNow
        });
    }

    public void ResetToSeedData()
    {
        _agents.Clear();
        _tickets.Clear();
        _handoffEvents.Clear();
        while (!_replyDurations.IsEmpty) _replyDurations.TryTake(out _);
        _preventedCollisionsCount = 0;
        _reassignmentCount = 0;
        _roundRobinCounter = 0;

        SeedDefaultData();
    }

    private void SeedDefaultData()
    {
        var now = DateTime.UtcNow;

        // Pre-seeded Operators
        var agentAlex = new AgentPresenceSession
        {
            AgentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AgentName = "Alex Vance",
            AgentEmail = "alex.vance@sgrowing.io",
            Role = "Senior Support Specialist",
            State = AgentPresenceState.Online,
            LastHeartbeatUtc = now,
            StateChangedUtc = now.AddMinutes(-12),
            ActiveWorkload = 2
        };

        var agentElena = new AgentPresenceSession
        {
            AgentId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            AgentName = "Elena Rostova",
            AgentEmail = "elena.rostova@sgrowing.io",
            Role = "Customer Success Lead",
            State = AgentPresenceState.ViewingTicket,
            LastHeartbeatUtc = now.AddSeconds(-5),
            StateChangedUtc = now.AddMinutes(-8),
            ActiveWorkload = 1
        };

        var agentMarcus = new AgentPresenceSession
        {
            AgentId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            AgentName = "Marcus Brody",
            AgentEmail = "marcus.brody@sgrowing.io",
            Role = "Escalation Engineer",
            State = AgentPresenceState.Idle,
            LastHeartbeatUtc = now.AddSeconds(-45), // Idle between 30s and 60s
            StateChangedUtc = now.AddMinutes(-7), // Idle for > 5 min!
            ActiveWorkload = 4
        };

        var agentSarah = new AgentPresenceSession
        {
            AgentId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            AgentName = "Sarah Chen",
            AgentEmail = "sarah.chen@sgrowing.io",
            Role = "Product Specialist",
            State = AgentPresenceState.Offline,
            LastHeartbeatUtc = now.AddMinutes(-25), // Offline for > 5 min!
            StateChangedUtc = now.AddMinutes(-25),
            ActiveWorkload = 1
        };

        var agentLiam = new AgentPresenceSession
        {
            AgentId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            AgentName = "Liam O'Connor",
            AgentEmail = "liam.oconnor@sgrowing.io",
            Role = "Billing & Retention Operator",
            State = AgentPresenceState.Online,
            LastHeartbeatUtc = now.AddSeconds(-2),
            StateChangedUtc = now.AddMinutes(-4),
            ActiveWorkload = 0 // Lowest workload!
        };

        _agents[agentAlex.AgentId] = agentAlex;
        _agents[agentElena.AgentId] = agentElena;
        _agents[agentMarcus.AgentId] = agentMarcus;
        _agents[agentSarah.AgentId] = agentSarah;
        _agents[agentLiam.AgentId] = agentLiam;

        // Pre-seeded Tickets
        var ticket101Id = Guid.Parse("aaaa0101-0000-0000-0000-000000000001");
        var ticket101 = new TicketPresenceState
        {
            TicketId = ticket101Id,
            TicketTitle = "Enterprise SSO Auth Failure - SAML assertion mismatch",
            CustomerName = "Acme Corp (Enterprise)",
            Priority = "High",
            AssignedAgentId = agentMarcus.AgentId,
            AssignedAgentName = agentMarcus.AgentName,
            CustomerMessagePending = true,
            LastCustomerMessageAtUtc = now.AddMinutes(-10),
            LockState = CollisionLockState.Unlocked
        };

        var ticket102Id = Guid.Parse("aaaa0102-0000-0000-0000-000000000002");
        var ticket102 = new TicketPresenceState
        {
            TicketId = ticket102Id,
            TicketTitle = "Payment Gateway Webhook 500 error on checkout",
            CustomerName = "Starlight Retail",
            Priority = "Urgent",
            AssignedAgentId = agentAlex.AgentId,
            AssignedAgentName = agentAlex.AgentName,
            CustomerMessagePending = true,
            LastCustomerMessageAtUtc = now.AddMinutes(-3),
            LockState = CollisionLockState.HardComposingLock,
            LockedByAgentId = agentAlex.AgentId,
            LockedByAgentName = agentAlex.AgentName,
            LockAcquiredAtUtc = now.AddSeconds(-15),
            LockExpiresAtUtc = now.AddSeconds(45),
            LockDraftSnippet = "Hello Starlight team, I am checking the webhook idempotency signatures right now...",
            StartedTypingAtUtc = now.AddSeconds(-15),
            LastTypingPingUtc = now.AddSeconds(-2)
        };
        ticket102.Viewers[agentAlex.AgentId] = new TicketViewerInfo(
            agentAlex.AgentId,
            agentAlex.AgentName,
            now.AddMinutes(-2),
            now,
            AgentPresenceState.ComposingReply,
            IsTyping: true,
            DraftSnippet: ticket102.LockDraftSnippet
        );
        agentAlex.CurrentTicketId = ticket102Id;
        agentAlex.State = AgentPresenceState.ComposingReply;
        agentAlex.IsTyping = true;
        agentAlex.DraftSnippet = ticket102.LockDraftSnippet;

        var ticket103Id = Guid.Parse("aaaa0103-0000-0000-0000-000000000003");
        var ticket103 = new TicketPresenceState
        {
            TicketId = ticket103Id,
            TicketTitle = "Custom Domain SSL Certificate Provisioning Delay",
            CustomerName = "Nordic Tech",
            Priority = "Medium",
            AssignedAgentId = agentElena.AgentId,
            AssignedAgentName = agentElena.AgentName,
            CustomerMessagePending = true,
            LastCustomerMessageAtUtc = now.AddMinutes(-15),
            LockState = CollisionLockState.SoftViewing
        };
        ticket103.Viewers[agentElena.AgentId] = new TicketViewerInfo(
            agentElena.AgentId,
            agentElena.AgentName,
            now.AddMinutes(-5),
            now.AddSeconds(-5),
            AgentPresenceState.ViewingTicket,
            IsTyping: false,
            DraftSnippet: null
        );
        agentElena.CurrentTicketId = ticket103Id;

        var ticket104Id = Guid.Parse("aaaa0104-0000-0000-0000-000000000004");
        var ticket104 = new TicketPresenceState
        {
            TicketId = ticket104Id,
            TicketTitle = "Refund Request for Annual Cloud Tier Upgrade",
            CustomerName = "Apex Global",
            Priority = "Low",
            AssignedAgentId = agentSarah.AgentId,
            AssignedAgentName = agentSarah.AgentName,
            CustomerMessagePending = true,
            LastCustomerMessageAtUtc = now.AddMinutes(-40),
            LockState = CollisionLockState.Unlocked
        };

        _tickets[ticket101Id] = ticket101;
        _tickets[ticket102Id] = ticket102;
        _tickets[ticket103Id] = ticket103;
        _tickets[ticket104Id] = ticket104;

        // Pre-seeded Handoff History
        _handoffEvents.Enqueue(new TicketHandoffEvent(
            Guid.NewGuid(),
            ticket101Id,
            ticket101.TicketTitle,
            ticket101.CustomerName,
            agentMarcus.AgentId,
            agentMarcus.AgentName,
            agentLiam.AgentId,
            agentLiam.AgentName,
            "Initial ticket routing optimization & workload balancing.",
            now.AddMinutes(-45)
        ));

        _preventedCollisionsCount = 14;
        _reassignmentCount = 6;
        _replyDurations.Add(38.0);
        _replyDurations.Add(44.5);
        _replyDurations.Add(32.0);
        _replyDurations.Add(51.2);
    }
}
