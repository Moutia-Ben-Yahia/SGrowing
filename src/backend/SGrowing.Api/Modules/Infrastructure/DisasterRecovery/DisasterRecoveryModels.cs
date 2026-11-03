using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Infrastructure.DisasterRecovery;

public enum CloudProvider
{
    AwsUsEast1,
    GcpEuropeWest3,
    AzureEastUs2,
    SovereignVpcFrankfurt
}

public enum NodeRole
{
    PrimaryActive,
    HotStandbySynchronous,
    WitnessQuorumNode
}

public enum ClusterHealth
{
    HealthySynchronized,
    DegradedReplicationLag,
    UnreachableOutage
}

public record CloudClusterNode(
    string NodeId,
    CloudProvider Provider,
    string Region,
    NodeRole Role,
    ClusterHealth Health,
    double ReplicationLagMs,
    DateTime LastHeartbeatUtc,
    bool HasQuorumVote
);

public record DisasterRecoveryTopology(
    List<CloudClusterNode> Nodes,
    string ActivePrimaryNodeId,
    bool QuorumEstablished,
    double RpoSeconds, // Recovery Point Objective achieved
    double RtoSeconds, // Recovery Time Objective benchmark
    DateTime LastSimulatedFailoverUtc
);

public record SimulateOutageRequest(
    string TargetNodeId,
    string OutageReason
);

public record FailoverExecutionResult(
    bool Success,
    string PreviousPrimaryNodeId,
    string NewPrimaryNodeId,
    double ElapsedFailoverSeconds,
    string DnsCutoverTransactionId,
    string Message
);
