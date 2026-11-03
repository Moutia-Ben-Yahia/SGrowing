namespace SGrowing.Api.Modules.Infrastructure.BgpBfd;

public record BgpBfdSession(
    string SessionId,
    string EdgePopLocation, // FRA-01, IAD-02, NRT-01, GRU-01, SIN-01
    string PeerTransitProvider, // LEVEL3_LUMEN, TELIA_ARELION, COGENT, AWS_DIRECT_CONNECT
    int LocalAsn, // e.g. 64512
    int PeerAsn,  // e.g. 3356
    double BfdIntervalMs, // e.g. 300.0ms
    int BfdMultiplier, // e.g. 3
    double PacketLossPct,
    double JitterMs,
    string SessionState, // UP, DOWN, DEGRADED, POISONED
    DateTime LastHeartbeat
);

public record AnycastRouteEvent(
    string EventId,
    string EdgePopLocation,
    string ActionTaken, // ROUTE_ANNOUNCED, AS_PATH_PREPEND_3X, ROUTE_WITHDRAWN, BFD_TEARDOWN
    double TriggerLatencyMs,
    string TriggerReason,
    DateTime Timestamp
);

public record BgpBfdOverview(
    int TotalBfdSessionsCount,
    int HealthySessionsCount,
    double GlobalEdgeHealthIndexPct, // e.g. 98.7%
    double SubSecondFailoverLatencyTargetMs, // e.g. 800ms
    List<BgpBfdSession> ActiveSessions,
    List<AnycastRouteEvent> RecentRouteEvents
);

public record SimulateFailoverRequest(
    string SessionId,
    string DegradationReason
);

public record RestoreRouteRequest(
    string SessionId
);
