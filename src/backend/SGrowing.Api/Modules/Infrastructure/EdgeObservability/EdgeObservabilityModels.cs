namespace SGrowing.Api.Modules.Infrastructure.EdgeObservability;

/// <summary>
/// Health and lifecycle states of edge compute nodes (Kubernetes worker or Nomad client).
/// </summary>
public enum NodeHealthState
{
    /// <summary>Node operates nominally with nominal thermals and network stability.</summary>
    Healthy,

    /// <summary>Core or chassis thermal sensors have exceeded safe operating limits (&gt; 80°C).</summary>
    DegradedThermal,

    /// <summary>Kernel packet loss (&gt; 3.0%) or high socket flap frequency detected.</summary>
    DegradedNetworkFlap,

    /// <summary>Node is cordoned and undergoing automated workload/pod evacuation.</summary>
    NodeEvacuating,

    /// <summary>All non-critical tasks stopped, critical tasks migrated, node offline.</summary>
    DrainedOffline
}

/// <summary>
/// Priority tiers for adaptive edge load shedding under compute or network saturation.
/// </summary>
public enum LoadSheddingTier
{
    /// <summary>
    /// Autonomous Mobile Robot (AMR) safety, laser/LiDAR obstacle clearance, barcode scanners, order confirm.
    /// NEVER shed under any distress condition.
    /// </summary>
    Tier1CriticalRoboticsNeverShed,

    /// <summary>Search, inventory reservation, and core customer checkout pathways.</summary>
    Tier2CoreCheckout,

    /// <summary>Real-time edge telemetry ingest, clickstream logging, operational metrics.</summary>
    Tier3AnalyticsTelemetry,

    /// <summary>Periodic edge model retraining, bulk OCR processing, report compilation.</summary>
    Tier4BackgroundBatch
}

/// <summary>
/// eBPF-derived health state for a single socket or connection peer.
/// </summary>
public enum SocketHealthStatus
{
    /// <summary>Nominal round-trip time and zero unrecovered packet drops.</summary>
    Normal,

    /// <summary>High TCP RTT jitter standard deviation (σ_RTT &gt; 45ms).</summary>
    HighRttJitter,

    /// <summary>Excessive SYN retransmit burst during TCP handshake connection setup.</summary>
    SynRetransmitBurst,

    /// <summary>TCP Zero-Window advertisement indicating local or remote receive buffer exhaustion.</summary>
    ZeroWindowThrottled
}

/// <summary>
/// Describes a containerized workload (Kubernetes Pod / Nomad Task) running on an edge node.
/// </summary>
public record EdgeWorkloadPod(
    string PodId,
    string Name,
    LoadSheddingTier Tier,
    double RequiredCpuCores,
    double RequiredMemoryMb,
    bool IsCritical)
{
    /// <summary>
    /// Factory creating an AMR robotics safety pod.
    /// </summary>
    public static EdgeWorkloadPod CreateRoboticsCritical(string podId, string name, double cpu = 1.0, double ram = 512.0) =>
        new(podId, name, LoadSheddingTier.Tier1CriticalRoboticsNeverShed, cpu, ram, true);

    /// <summary>
    /// Factory creating a checkout/search pod.
    /// </summary>
    public static EdgeWorkloadPod CreateCoreCheckout(string podId, string name, double cpu = 0.5, double ram = 256.0) =>
        new(podId, name, LoadSheddingTier.Tier2CoreCheckout, cpu, ram, false);

    /// <summary>
    /// Factory creating an analytics telemetry pod.
    /// </summary>
    public static EdgeWorkloadPod CreateAnalytics(string podId, string name, double cpu = 0.25, double ram = 128.0) =>
        new(podId, name, LoadSheddingTier.Tier3AnalyticsTelemetry, cpu, ram, false);

    /// <summary>
    /// Factory creating a background batch processing pod.
    /// </summary>
    public static EdgeWorkloadPod CreateBackgroundBatch(string podId, string name, double cpu = 0.5, double ram = 512.0) =>
        new(podId, name, LoadSheddingTier.Tier4BackgroundBatch, cpu, ram, false);
}

/// <summary>
/// Telemetry snapshot reported by an edge compute node.
/// </summary>
public record EdgeNodeTelemetry(
    string NodeId,
    string ClusterId,
    string Hostname,
    double CpuUtilizationPct,
    double MemoryUtilizationPct,
    double TemperatureCelsius,
    double PacketLossPct,
    long SocketFlapCount,
    bool IsWanConnected,
    NodeHealthState State,
    DateTimeOffset LastHeartbeatUtc,
    IReadOnlyList<EdgeWorkloadPod> ActivePods);

/// <summary>
/// Kernel-level TCP/UDP socket telemetry captured via eBPF kprobes, kretprobes, or tracepoints.
/// </summary>
public record EbpfSocketMetric(
    string SocketId,
    string SourceAddress,
    int SourcePort,
    string DestinationAddress,
    int DestinationPort,
    string TcpState,
    double SmoothedRttMs,
    double RttVarianceMs,
    long SynRetransmitCount,
    long ZeroWindowProbeCount,
    long BytesSent,
    long BytesReceived,
    SocketHealthStatus Status,
    DateTimeOffset CapturedAtUtc,
    string ProbeType);

/// <summary>
/// Detailed result of an evacuated pod.
/// </summary>
public record EvacuatedPodDetail(
    string PodId,
    string PodName,
    LoadSheddingTier Tier,
    string? TargetNodeId,
    string ActionTaken,
    bool Success);

/// <summary>
/// Orchestrated autonomous pod evacuation plan for a degraded or draining node.
/// </summary>
public record PodEvacuationPlan(
    string PlanId,
    string SourceNodeId,
    string Reason,
    DateTimeOffset InitiatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<EvacuatedPodDetail> EvacuatedPods,
    string Status);

/// <summary>
/// Load shedding enforcement policy currently active across the edge cluster.
/// </summary>
public record LoadSheddingPolicy(
    bool IsActive,
    IReadOnlyList<LoadSheddingTier> ActiveShedTiers,
    double CurrentCpuPct,
    double CurrentPacketLossPct,
    bool IsManualOverride,
    DateTimeOffset LastEvaluatedUtc,
    TimeSpan StableDuration);

// =============================================================================
// API Request and Response DTOs
// =============================================================================

/// <summary>
/// Response payload summarizing edge cluster health, node statuses, and active evacuations.
/// </summary>
public record ClusterHealthResponse(
    string ClusterId,
    bool WanBackhaulConnected,
    int TotalNodes,
    int HealthyNodes,
    int DegradedNodes,
    int EvacuatingNodes,
    int OfflineNodes,
    IReadOnlyList<EdgeNodeTelemetry> Nodes,
    IReadOnlyList<PodEvacuationPlan> RecentEvacuations);

/// <summary>
/// Request payload to initiate manual drain of an edge node.
/// </summary>
public record DrainNodeRequest(
    string? Reason = null,
    bool ForceEvacuate = false);

/// <summary>
/// Response payload for an edge node drain operation.
/// </summary>
public record DrainNodeResponse(
    bool Success,
    string NodeId,
    NodeHealthState FinalState,
    PodEvacuationPlan? Plan,
    string Message);

/// <summary>
/// Response payload containing kernel eBPF socket telemetry and detected network saturation anomalies.
/// </summary>
public record EbpfMetricsResponse(
    int TotalSocketsTracked,
    int SocketsInHighRttJitter,
    int SocketsInSynRetransmitBurst,
    int SocketsInZeroWindowThrottled,
    int RogueSocketLeaksDetected,
    IReadOnlyList<EbpfSocketMetric> Sockets,
    IReadOnlyList<string> MicroBurstSaturationAlerts);

/// <summary>
/// Request payload to manually override load shedding tiers.
/// </summary>
public record LoadSheddingOverrideRequest(
    bool ForceOverride,
    IReadOnlyList<LoadSheddingTier>? ShedTiers = null,
    string? Reason = null);

/// <summary>
/// Response payload confirming load shedding policy override.
/// </summary>
public record LoadSheddingOverrideResponse(
    bool Success,
    string Message,
    LoadSheddingPolicy ActivePolicy);

/// <summary>
/// Response payload detailing active load shedding tiers, hysteresis state, and drop percentage.
/// </summary>
public record LoadSheddingStatusResponse(
    bool IsSheddingActive,
    IReadOnlyList<LoadSheddingTier> ShedTiers,
    double CurrentCpuPct,
    double CurrentPacketLossPct,
    double SheddingDropPercentage,
    bool InHysteresisRecovery,
    double SecondsStable,
    bool IsManualOverride);

/// <summary>
/// Ingestion request for edge node telemetry.
/// </summary>
public record RegisterNodeTelemetryRequest(
    EdgeNodeTelemetry Telemetry);

/// <summary>
/// Ingestion request for eBPF socket metric.
/// </summary>
public record IngestEbpfSocketMetricRequest(
    EbpfSocketMetric Metric);
