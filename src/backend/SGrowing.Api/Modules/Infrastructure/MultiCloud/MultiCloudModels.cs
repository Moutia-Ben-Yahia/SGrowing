namespace SGrowing.Api.Modules.Infrastructure.MultiCloud;

/// <summary>
/// Supported public cloud providers and sovereign private regional datacenters.
/// </summary>
public enum CloudProvider
{
    Aws,
    Azure,
    Gcp,
    SovereignPrivateAlgiers,
    SovereignPrivateDubai
}

/// <summary>
/// Operational health and routing availability status of a multi-cloud region node.
/// </summary>
public enum RegionNodeStatus
{
    Healthy,
    DegradedLatency,
    PartitionedIsolated,
    DrainingMaintenance,
    Offline
}

/// <summary>
/// Raft-inspired Byzantine distributed consensus roles in the sovereign mesh cluster.
/// </summary>
public enum QuorumRole
{
    Leader,
    Follower,
    Candidate,
    NonVotingWitness
}

/// <summary>
/// Execution state of an autonomous disaster recovery failover drill.
/// </summary>
public enum DrDrillStatus
{
    Scheduled,
    ExecutingFailover,
    ValidatedSuccess,
    Aborted
}

/// <summary>
/// Represents a sovereign or public cloud cluster node in the Anycast multi-cloud mesh.
/// </summary>
/// <param name="NodeId">Unique alphanumeric identifier for the node.</param>
/// <param name="Provider">Cloud or sovereign datacenter provider.</param>
/// <param name="RegionCode">Regional boundary code (e.g. 'eu-west-3', 'westeurope', 'dz-central-1').</param>
/// <param name="Hostname">Fully qualified domain name or internal Anycast endpoint.</param>
/// <param name="HealthStatus">Current health and availability state.</param>
/// <param name="LatencyP99Ms">P99 round-trip latency in milliseconds.</param>
/// <param name="Weight">Traffic distribution weight factor for Anycast routing.</param>
/// <param name="IsWriteLeader">Indicates whether this node currently holds the distributed write lease.</param>
public record CloudMeshNode(
    string NodeId,
    CloudProvider Provider,
    string RegionCode,
    string Hostname,
    RegionNodeStatus HealthStatus,
    double LatencyP99Ms,
    double Weight,
    bool IsWriteLeader)
{
    /// <summary>
    /// Gets whether this node is eligible to receive routed ingress traffic.
    /// Ingress traffic is diverted away from degraded, partitioned, draining, or offline nodes.
    /// </summary>
    public bool IsRoutable => HealthStatus == RegionNodeStatus.Healthy && LatencyP99Ms < 5000.0;
}

/// <summary>
/// Periodic consensus heartbeat emitted by participating mesh nodes to maintain quorum.
/// </summary>
/// <param name="NodeId">Originating node identifier.</param>
/// <param name="Term">Raft-inspired epoch term number.</param>
/// <param name="SequenceNumber">Monotonically increasing sequence number within the term.</param>
/// <param name="TimestampUtc">Timestamp in UTC when heartbeat was dispatched.</param>
/// <param name="KnownLeaderId">Currently acknowledged leader node ID, if any.</param>
/// <param name="ActivePeersCount">Number of active peers reachable from this node.</param>
public record QuorumHeartbeat(
    string NodeId,
    long Term,
    long SequenceNumber,
    DateTimeOffset TimestampUtc,
    string? KnownLeaderId,
    int ActivePeersCount);

/// <summary>
/// Evaluation result of a split-brain quorum decision, enforcing strict majority consensus.
/// </summary>
/// <param name="GrantedWriteLease">Whether the write lease was granted to the candidate or leader.</param>
/// <param name="MajorityClusterNodes">List of node identifiers forming the consensus majority partition.</param>
/// <param name="IsolatedMinorityNodes">List of node identifiers isolated in the minority partition (read-only).</param>
/// <param name="QuorumTerm">Cluster term in which the quorum was evaluated.</param>
/// <param name="PartitionDetected">Indicates whether a network partition or isolated minority was detected.</param>
/// <param name="Reason">Human-readable audit rationale for consensus lease grant or rejection.</param>
public record SplitBrainQuorumDecision(
    bool GrantedWriteLease,
    IReadOnlyList<string> MajorityClusterNodes,
    IReadOnlyList<string> IsolatedMinorityNodes,
    long QuorumTerm,
    bool PartitionDetected,
    string Reason);

/// <summary>
/// Audit report generated following an autonomous non-destructive disaster recovery drill.
/// </summary>
/// <param name="DrillId">Unique identifier of the disaster recovery drill execution.</param>
/// <param name="TargetNodeId">Identifier of the simulated failure node.</param>
/// <param name="FailoverType">Type of failure simulation ('SimulatedCloudBlackout' or 'WanPartition').</param>
/// <param name="ActualRtoSeconds">Observed Recovery Time Objective in seconds (&lt; 30s target).</param>
/// <param name="ActualRpoSeconds">Observed Recovery Point Objective in seconds (0.0s zero data loss target).</param>
/// <param name="Success">Whether the drill completed successfully meeting all RTO and RPO thresholds.</param>
/// <param name="DrillTimestampUtc">UTC timestamp when drill completed.</param>
/// <param name="Message">Detailed verification outcome and convergence narrative.</param>
public record DisasterRecoveryDrillReport(
    string DrillId,
    string TargetNodeId,
    string FailoverType,
    double ActualRtoSeconds,
    double ActualRpoSeconds,
    bool Success,
    DateTimeOffset DrillTimestampUtc,
    string Message);

/// <summary>
/// Request payload to register a new region node in the multi-cloud mesh.
/// </summary>
public record RegisterNodeRequest(
    string NodeId,
    CloudProvider Provider,
    string RegionCode,
    string Hostname,
    RegionNodeStatus HealthStatus = RegionNodeStatus.Healthy,
    double LatencyP99Ms = 15.0,
    double Weight = 1.0,
    bool IsWriteLeader = false)
{
    /// <summary>
    /// Converts request to a domain CloudMeshNode model.
    /// </summary>
    public CloudMeshNode ToNode() => new(
        NodeId,
        Provider,
        RegionCode,
        Hostname,
        HealthStatus,
        LatencyP99Ms,
        Weight,
        IsWriteLeader);
}

/// <summary>
/// Request payload for node heartbeat submissions.
/// </summary>
public record NodeHeartbeatRequest(
    string NodeId,
    long Term,
    long SequenceNumber,
    DateTimeOffset TimestampUtc,
    string? KnownLeaderId,
    int ActivePeersCount)
{
    /// <summary>
    /// Converts request to a domain QuorumHeartbeat model.
    /// </summary>
    public QuorumHeartbeat ToHeartbeat() => new(
        NodeId,
        Term,
        SequenceNumber,
        TimestampUtc,
        KnownLeaderId,
        ActivePeersCount);
}

/// <summary>
/// Request payload to update an existing region node's health status and observed latency.
/// </summary>
public record UpdateNodeHealthRequest(
    RegionNodeStatus Status,
    double LatencyMs);

/// <summary>
/// Request payload to trigger an automated non-destructive disaster recovery drill.
/// </summary>
public record TriggerDrDrillRequest(
    string TargetNodeId,
    string FailoverType = "SimulatedCloudBlackout",
    double DrainGracePeriodSeconds = 5.0);

/// <summary>
/// Global topology summary of the Anycast sovereign multi-cloud mesh.
/// </summary>
public record MultiCloudTopologyResponse(
    int TotalNodesCount,
    int HealthyNodesCount,
    double TotalCapacityWeight,
    string? CurrentWriteLeaderId,
    long CurrentTerm,
    IReadOnlyList<CloudMeshNode> Nodes,
    DisasterRecoveryDrillReport? LastDrillReport);

/// <summary>
/// Current Byzantine quorum state of the cluster.
/// </summary>
public record QuorumStateResponse(
    long CurrentTerm,
    string? CurrentLeaderId,
    int QuorumThreshold,
    int TotalVotingNodes,
    bool HasWriteLease,
    IReadOnlyList<string> ActiveNodeIds,
    IReadOnlyList<string> IsolatedNodeIds,
    DateTimeOffset LastEvaluationUtc);
