using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.CallStateResumption;

public sealed record ActiveCallSessionSnapshotRecord(
    string SessionId,
    string CallerPhoneNumber,
    string CalleePhoneNumber,
    string CustomerName,
    string LastAcousticUtterance,
    string DialogGoalTopic,
    string CurrentContractPhase,
    int TurnCounter,
    DateTime LastHeartbeatAt
);

public sealed record CallResumptionEventRecord(
    string ResumptionId,
    string OriginalSessionId,
    string ReconnectSessionId,
    string DisconnectReason, // NETWORK_TIMEOUT, SIP_BYE_UNEXPECTED, CELLULAR_TOWER_HANDOFF_DROP
    double ReconnectLatencySeconds,
    string GeneratedAcousticGreeting,
    string ResumptionStatus // AUTO_CALLED_BACK, RESUMED_IN_PROGRESS, ABANDONED
);

public sealed record StateResumptionOverview(
    int MonitoredActiveCallsCount,
    int TotalDisconnectionsHandled,
    double AutoCallbackSuccessRatePct,
    double AverageReconnectLatencySeconds,
    IReadOnlyList<ActiveCallSessionSnapshotRecord> ActiveSnapshots,
    IReadOnlyList<CallResumptionEventRecord> RecentResumptions
);

public sealed record TriggerAutoCallbackRequest(
    string SessionId,
    string DisconnectReason
);

public sealed record SaveCallSnapshotRequest(
    string SessionId,
    string CallerPhoneNumber,
    string CalleePhoneNumber,
    string CustomerName,
    string LastAcousticUtterance,
    string DialogGoalTopic,
    string CurrentContractPhase,
    int TurnCounter
);
