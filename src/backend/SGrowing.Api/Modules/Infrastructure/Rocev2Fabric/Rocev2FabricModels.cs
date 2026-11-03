namespace SGrowing.Api.Modules.Infrastructure.Rocev2Fabric;

public enum CongestionState
{
    NormalLossless,
    EcnMarkingActive,
    PfcPauseWarning,
    PfcDeadlockCritical
}

public record RoceNodeEndpoint(
    string NodeId,
    string Hostname,
    string CloudProvider, // "AWS", "Azure", "GCP", "BareMetal"
    string ClusterRegion,
    string NicModel, // e.g. "Mellanox ConnectX-7 400G"
    int GpuCount,
    int PfcPriorityQueue, // standard is 3
    bool LosslessRoceEnabled,
    DateTime RegisteredAtUtc
);

public record FabricTelemetrySample(
    string SampleId,
    string NodeId,
    double ThroughputGbps,
    double BufferOccupancyPercentage,
    int EcnMarkedPacketsPerSec,
    int PfcPauseFramesTxPerSec,
    int PfcPauseFramesRxPerSec,
    double TailLatencyMicros,
    DateTime CapturedAtUtc
);

public record PfcDeadlockAlert(
    string AlertId,
    string NodeId,
    string SpineSwitchId,
    int StuckQueuePriority,
    int DeadlockDurationMs,
    string AutomatedMitigationAction,
    bool QueueDrainedSuccessfully,
    DateTime DetectedAtUtc
);

public record DcqcnTuningProfile(
    string ProfileId,
    double AlphaIncreaseStep,
    double BetaDecreaseFactor,
    int TargetRateBps,
    int MinRateBps,
    string RecommendationNotes,
    DateTime GeneratedAtUtc
);

public record RoceFabricSummary(
    int TotalGpuNodesConnected,
    int TotalActiveGpus,
    double AggregateThroughputTbps,
    CongestionState GlobalCongestionState,
    int ActiveDeadlockAlerts,
    double AverageFabricLatencyMicros,
    DateTime LastAuditedUtc
);

public record RegisterRoceNodeRequest(
    string Hostname,
    string CloudProvider,
    string ClusterRegion,
    string NicModel,
    int GpuCount,
    int PfcPriorityQueue
);
