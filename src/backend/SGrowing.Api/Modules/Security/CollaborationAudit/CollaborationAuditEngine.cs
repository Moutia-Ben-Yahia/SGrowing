using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Security.CollaborationAudit;

public interface ICollaborationAuditEngine
{
    CollaborativeSession SendHeartbeat(CollaborationHeartbeatRequest request);
    IReadOnlyList<CollaborativeSession> ListActiveCollaborators(string? resourceId = null);
    IReadOnlyList<SecurityAuditLog> ListAuditLogs(int take = 50);
    bool RevokeUserSessions(RevokeUserSessionsRequest request);
    CollaborationAuditSnapshot GetSnapshot();
}

public sealed class CollaborationAuditEngine : ICollaborationAuditEngine
{
    private readonly ConcurrentDictionary<string, CollaborativeSession> _sessions = new();
    private readonly ConcurrentBag<SecurityAuditLog> _logs = new();
    private readonly ConcurrentBag<GeofenceAlert> _geofenceAlerts = new();

    public CollaborationAuditEngine()
    {
        SeedDefaultData();
    }

    public CollaborativeSession SendHeartbeat(CollaborationHeartbeatRequest request)
    {
        var sessionKey = $"{request.ResourceId}:{request.UserId}";
        var session = new CollaborativeSession(
            SessionId: "collab_" + Guid.NewGuid().ToString("N")[..8],
            ResourceId: request.ResourceId,
            ResourceType: request.ResourceType,
            UserId: request.UserId,
            UserName: request.UserName,
            CursorX: request.CursorX,
            CursorY: request.CursorY,
            ActiveField: request.ActiveField,
            LastHeartbeatUtc: DateTime.UtcNow
        );

        _sessions[sessionKey] = session;
        return session;
    }

    public IReadOnlyList<CollaborativeSession> ListActiveCollaborators(string? resourceId = null)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-60);
        var query = _sessions.Values.Where(s => s.LastHeartbeatUtc >= cutoff);

        if (!string.IsNullOrEmpty(resourceId))
        {
            query = query.Where(s => s.ResourceId.Equals(resourceId, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(s => s.LastHeartbeatUtc).ToList();
    }

    public IReadOnlyList<SecurityAuditLog> ListAuditLogs(int take = 50)
    {
        return _logs.OrderByDescending(l => l.TimestampUtc).Take(take).ToList();
    }

    public bool RevokeUserSessions(RevokeUserSessionsRequest request)
    {
        var keysToRemove = _sessions.Where(kvp => kvp.Value.UserId.Equals(request.UserId, StringComparison.OrdinalIgnoreCase))
                                   .Select(kvp => kvp.Key)
                                   .ToList();

        foreach (var key in keysToRemove)
        {
            _sessions.TryRemove(key, out _);
        }

        _logs.Add(new SecurityAuditLog(
            EventId: "evt_" + Guid.NewGuid().ToString("N")[..8],
            UserId: request.UserId,
            UserEmail: "revoked@enterprise.corp",
            Action: "UserSessionsTerminated",
            TargetResource: "AuthSessionManager",
            IpAddress: "127.0.0.1",
            Country: "US",
            Severity: "Critical",
            TimestampUtc: DateTime.UtcNow
        ));

        return true;
    }

    public CollaborationAuditSnapshot GetSnapshot()
    {
        var activeCollabs = ListActiveCollaborators();
        var recentLogs = ListAuditLogs(10);
        var criticalCount = _logs.Count(l => l.Severity == "Critical");
        var geofenceCount = _geofenceAlerts.Count(g => g.Status == "Active");

        return new CollaborationAuditSnapshot(
            ActiveCollaboratorsCount: activeCollabs.Count,
            CriticalAuditEventsCount: criticalCount,
            GeofenceAlertsCount: geofenceCount,
            RecentLogs: recentLogs,
            ActiveCollaborators: activeCollabs
        );
    }

    private void SeedDefaultData()
    {
        _sessions["res_funnel_canvas_1:usr_alice"] = new CollaborativeSession("collab_1", "res_funnel_canvas_1", "FunnelCanvas", "usr_alice", "Alice Chen (Lead Designer)", 420.5, 185.0, "HeroBlock", DateTime.UtcNow);
        _sessions["res_funnel_canvas_1:usr_bob"] = new CollaborativeSession("collab_2", "res_funnel_canvas_1", "FunnelCanvas", "usr_bob", "Bob Miller (RevOps)", 780.0, 310.2, "PricingCards", DateTime.UtcNow);

        _logs.Add(new SecurityAuditLog("evt_01", "usr_admin", "admin@sgrowing.io", "RoleElevated", "SuperAdminPolicy", "192.168.1.10", "US", "Warning", DateTime.UtcNow.AddMinutes(-40)));
        _logs.Add(new SecurityAuditLog("evt_02", "usr_eve", "eve@contractor.ext", "GeofenceViolation", "ApiTokenEndpoint", "45.134.22.9", "RU", "Critical", DateTime.UtcNow.AddMinutes(-15)));

        _geofenceAlerts.Add(new GeofenceAlert("geo_01", "usr_eve", "RU", "45.134.22.9", DateTime.UtcNow.AddMinutes(-15), "Active"));
    }
}
