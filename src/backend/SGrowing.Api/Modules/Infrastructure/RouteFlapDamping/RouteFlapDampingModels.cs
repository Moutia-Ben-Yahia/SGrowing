namespace SGrowing.Api.Modules.Infrastructure.RouteFlapDamping;

public enum BgpPrefixFlapState
{
    ActiveHealthy,
    FlappingMonitored,
    SuppressedDamped,
    DecayingRecovery
}

public sealed record FlapDampingRule(
    string RuleId,
    string PrefixCidr,
    int CutoffSuppressThresholdPenalty,
    int ReuseThresholdPenalty,
    int HalfLifeDecayMinutes,
    int MaxSuppressDurationMinutes
);

public sealed record BgpPrefixFlapStateEntity(
    string PrefixCidr,
    string OriginAsn,
    double CurrentFlapPenaltyScore,
    int FlapCountTotal,
    BgpPrefixFlapState State,
    DateTime LastFlapAtUtc,
    DateTime? SuppressedUntilUtc
);

public sealed record FlapPenaltyEvent(
    string EventId,
    string PrefixCidr,
    string OriginAsn,
    int PenaltyIncrement,
    double NewCumulativePenalty,
    string TriggerPeerIp,
    DateTime EventTimestampUtc
);

public sealed record AnycastPoPEvacuation(
    string EvacuationId,
    string TargetPoPName,
    string CloudProvider,
    string EvacuationReason,
    double DivertedTrafficBps,
    DateTime EvacuatedAtUtc,
    string Status
);

public sealed record RouteFlapOverview(
    int TotalMonitoredPrefixesCount,
    int CurrentlySuppressedPrefixesCount,
    double AverageFlapPenaltyScore,
    int TotalFlapEventsLastHour,
    IReadOnlyList<BgpPrefixFlapStateEntity> MonitoredPrefixes,
    IReadOnlyList<FlapPenaltyEvent> RecentFlapEvents
);
