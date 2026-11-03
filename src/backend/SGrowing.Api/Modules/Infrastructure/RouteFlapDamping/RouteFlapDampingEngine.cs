using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Infrastructure.RouteFlapDamping;

public interface IRouteFlapDampingEngine
{
    RouteFlapOverview GetOverview();
    FlapPenaltyEvent RecordFlapEvent(string prefixCidr, string originAsn, string peerIp);
    BgpPrefixFlapStateEntity CheckPrefixState(string prefixCidr);
    AnycastPoPEvacuation EvacuatePoP(string targetPoPName, string cloudProvider, string reason);
}

public sealed class RouteFlapDampingEngine : IRouteFlapDampingEngine
{
    private readonly ConcurrentDictionary<string, BgpPrefixFlapStateEntity> _prefixes = new();
    private readonly ConcurrentDictionary<string, FlapPenaltyEvent> _events = new();
    private readonly ConcurrentDictionary<string, AnycastPoPEvacuation> _evacuations = new();

    private const int CutoffThreshold = 3000;
    private const int ReuseThreshold = 1000;
    private const int PenaltyPerFlap = 1000;

    public RouteFlapDampingEngine()
    {
        SeedPrefixes();
        SeedInitialEvents();
    }

    private void SeedPrefixes()
    {
        var p1 = new BgpPrefixFlapStateEntity(
            "198.51.100.0/24",
            "AS398124",
            CurrentFlapPenaltyScore: 0.0,
            FlapCountTotal: 0,
            BgpPrefixFlapState.ActiveHealthy,
            DateTime.UtcNow.AddHours(-12),
            null
        );
        _prefixes.TryAdd(p1.PrefixCidr, p1);

        var p2 = new BgpPrefixFlapStateEntity(
            "203.0.113.0/24",
            "AS13335",
            CurrentFlapPenaltyScore: 3200.0,
            FlapCountTotal: 4,
            BgpPrefixFlapState.SuppressedDamped,
            DateTime.UtcNow.AddMinutes(-4),
            DateTime.UtcNow.AddMinutes(26)
        );
        _prefixes.TryAdd(p2.PrefixCidr, p2);
    }

    private void SeedInitialEvents()
    {
        var evt1 = new FlapPenaltyEvent(
            "FLAP-EVT-001",
            "203.0.113.0/24",
            "AS13335",
            PenaltyPerFlap,
            3200.0,
            "198.51.100.254",
            DateTime.UtcNow.AddMinutes(-4)
        );
        _events.TryAdd(evt1.EventId, evt1);
    }

    public RouteFlapOverview GetOverview()
    {
        var prefixes = _prefixes.Values.ToList();
        var events = _events.Values.OrderByDescending(e => e.EventTimestampUtc).Take(10).ToList();
        int suppressed = prefixes.Count(p => p.State == BgpPrefixFlapState.SuppressedDamped);

        return new RouteFlapOverview(
            TotalMonitoredPrefixesCount: prefixes.Count,
            CurrentlySuppressedPrefixesCount: suppressed,
            AverageFlapPenaltyScore: 1600.0,
            TotalFlapEventsLastHour: 18,
            MonitoredPrefixes: prefixes,
            RecentFlapEvents: events
        );
    }

    public FlapPenaltyEvent RecordFlapEvent(string prefixCidr, string originAsn, string peerIp)
    {
        var existing = _prefixes.GetOrAdd(prefixCidr, cidr => new BgpPrefixFlapStateEntity(
            cidr,
            originAsn,
            0.0,
            0,
            BgpPrefixFlapState.ActiveHealthy,
            DateTime.UtcNow,
            null
        ));

        double newScore = existing.CurrentFlapPenaltyScore + PenaltyPerFlap;
        int newFlapCount = existing.FlapCountTotal + 1;
        BgpPrefixFlapState newState = newScore >= CutoffThreshold ? BgpPrefixFlapState.SuppressedDamped : BgpPrefixFlapState.FlappingMonitored;
        DateTime? suppressedUntil = newState == BgpPrefixFlapState.SuppressedDamped ? DateTime.UtcNow.AddMinutes(30) : null;

        var updated = existing with
        {
            CurrentFlapPenaltyScore = newScore,
            FlapCountTotal = newFlapCount,
            State = newState,
            LastFlapAtUtc = DateTime.UtcNow,
            SuppressedUntilUtc = suppressedUntil
        };
        _prefixes[prefixCidr] = updated;

        var evt = new FlapPenaltyEvent(
            $"FLAP-EVT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            prefixCidr,
            originAsn,
            PenaltyPerFlap,
            newScore,
            peerIp,
            DateTime.UtcNow
        );
        _events.TryAdd(evt.EventId, evt);

        return evt;
    }

    public BgpPrefixFlapStateEntity CheckPrefixState(string prefixCidr)
    {
        return _prefixes.GetOrAdd(prefixCidr, cidr => new BgpPrefixFlapStateEntity(
            cidr,
            "AS-UNKNOWN",
            0.0,
            0,
            BgpPrefixFlapState.ActiveHealthy,
            DateTime.UtcNow,
            null
        ));
    }

    public AnycastPoPEvacuation EvacuatePoP(string targetPoPName, string cloudProvider, string reason)
    {
        var evac = new AnycastPoPEvacuation(
            $"EVAC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            targetPoPName,
            cloudProvider,
            reason,
            DivertedTrafficBps: 45000000000.0, // 45 Gbps
            DateTime.UtcNow,
            "EVACUATED_TO_SECONDARY_POP"
        );
        _evacuations.TryAdd(evac.EvacuationId, evac);
        return evac;
    }
}
