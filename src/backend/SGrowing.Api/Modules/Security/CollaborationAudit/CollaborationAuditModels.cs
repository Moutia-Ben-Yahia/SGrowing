using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Security.CollaborationAudit;

public sealed record CollaborativeSession(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("resourceId")] string ResourceId,
    [property: JsonPropertyName("resourceType")] string ResourceType,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("cursorX")] double CursorX,
    [property: JsonPropertyName("cursorY")] double CursorY,
    [property: JsonPropertyName("activeField")] string? ActiveField,
    [property: JsonPropertyName("lastHeartbeatUtc")] DateTime LastHeartbeatUtc
);

public sealed record SecurityAuditLog(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userEmail")] string UserEmail,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("targetResource")] string TargetResource,
    [property: JsonPropertyName("ipAddress")] string IpAddress,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("timestampUtc")] DateTime TimestampUtc
);

public sealed record GeofenceAlert(
    [property: JsonPropertyName("alertId")] string AlertId,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("ipAddress")] string IpAddress,
    [property: JsonPropertyName("triggeredAtUtc")] DateTime TriggeredAtUtc,
    [property: JsonPropertyName("status")] string Status
);

public sealed record CollaborationHeartbeatRequest(
    [property: JsonPropertyName("resourceId")] string ResourceId,
    [property: JsonPropertyName("resourceType")] string ResourceType,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("cursorX")] double CursorX,
    [property: JsonPropertyName("cursorY")] double CursorY,
    [property: JsonPropertyName("activeField")] string? ActiveField
);

public sealed record RevokeUserSessionsRequest(
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("reason")] string Reason
);

public sealed record CollaborationAuditSnapshot(
    [property: JsonPropertyName("activeCollaboratorsCount")] int ActiveCollaboratorsCount,
    [property: JsonPropertyName("criticalAuditEventsCount")] int CriticalAuditEventsCount,
    [property: JsonPropertyName("geofenceAlertsCount")] int GeofenceAlertsCount,
    [property: JsonPropertyName("recentLogs")] IReadOnlyList<SecurityAuditLog> RecentLogs,
    [property: JsonPropertyName("activeCollaborators")] IReadOnlyList<CollaborativeSession> ActiveCollaborators
);
