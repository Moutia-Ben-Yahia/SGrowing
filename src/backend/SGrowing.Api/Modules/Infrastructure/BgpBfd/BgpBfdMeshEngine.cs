using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.BgpBfd;

public class BgpBfdMeshEngine
{
    private readonly ConcurrentDictionary<string, BgpBfdSession> _sessions = new();
    private readonly ConcurrentBag<AnycastRouteEvent> _events = new();

    public BgpBfdMeshEngine()
    {
        SeedDemoData();
    }

    private void SeedDemoData()
    {
        var s1 = new BgpBfdSession(
            "BFD-FRA-01",
            "FRA-01 (Frankfurt)",
            "TELIA_ARELION",
            64512,
            1299,
            300.0,
            3,
            0.02,
            1.2,
            "UP",
            DateTime.UtcNow
        );

        var s2 = new BgpBfdSession(
            "BFD-IAD-02",
            "IAD-02 (Ashburn)",
            "LEVEL3_LUMEN",
            64512,
            3356,
            300.0,
            3,
            0.01,
            0.9,
            "UP",
            DateTime.UtcNow
        );

        var s3 = new BgpBfdSession(
            "BFD-NRT-01",
            "NRT-01 (Tokyo)",
            "AWS_DIRECT_CONNECT",
            64512,
            16509,
            300.0,
            3,
            0.04,
            2.1,
            "UP",
            DateTime.UtcNow
        );

        _sessions[s1.SessionId] = s1;
        _sessions[s2.SessionId] = s2;
        _sessions[s3.SessionId] = s3;

        _events.Add(new AnycastRouteEvent(
            "EVT-001",
            "FRA-01",
            "ROUTE_ANNOUNCED",
            12.4,
            "BGP Anycast prefix /24 announced globally across tier-1 carriers",
            DateTime.UtcNow.AddHours(-12)
        ));
    }

    public BgpBfdOverview GetOverview()
    {
        var sessionsList = _sessions.Values.ToList();
        var eventsList = _events.ToList();

        int upCount = sessionsList.Count(s => s.SessionState == "UP");
        double healthIndex = sessionsList.Count > 0
            ? Math.Round(((double)upCount / sessionsList.Count) * 100.0, 1)
            : 100.0;

        return new BgpBfdOverview(
            sessionsList.Count,
            upCount,
            healthIndex,
            800.0, // Sub-second 800ms BFD target
            sessionsList,
            eventsList
        );
    }

    public AnycastRouteEvent SimulateFailover(SimulateFailoverRequest request)
    {
        var evtId = $"EVT-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string location = "UNKNOWN";

        if (_sessions.TryGetValue(request.SessionId, out var session))
        {
            location = session.EdgePopLocation;
            var degraded = session with
            {
                SessionState = "POISONED",
                PacketLossPct = 28.5,
                JitterMs = 45.2,
                LastHeartbeat = DateTime.UtcNow
            };
            _sessions[request.SessionId] = degraded;
        }

        var routeEvent = new AnycastRouteEvent(
            evtId,
            location,
            "ROUTE_WITHDRAWN",
            420.0, // 420ms sub-second fast failover
            $"BFD link degradation detected ({request.DegradationReason}); Anycast route withdrawn to prevent blackholing",
            DateTime.UtcNow
        );

        _events.Add(routeEvent);
        return routeEvent;
    }

    public AnycastRouteEvent RestoreRoute(RestoreRouteRequest request)
    {
        var evtId = $"EVT-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        string location = "UNKNOWN";

        if (_sessions.TryGetValue(request.SessionId, out var session))
        {
            location = session.EdgePopLocation;
            var restored = session with
            {
                SessionState = "UP",
                PacketLossPct = 0.01,
                JitterMs = 1.1,
                LastHeartbeat = DateTime.UtcNow
            };
            _sessions[request.SessionId] = restored;
        }

        var routeEvent = new AnycastRouteEvent(
            evtId,
            location,
            "ROUTE_ANNOUNCED",
            180.0,
            "BFD link stabilized; Anycast route re-advertised with standard MED and local preference",
            DateTime.UtcNow
        );

        _events.Add(routeEvent);
        return routeEvent;
    }
}
