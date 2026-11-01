namespace SGrowing.Api.Modules.Infrastructure.MultiCloud;

/// <summary>
/// Raft-inspired Byzantine distributed consensus arbitrator enforcing strict majority
/// write quorum and automatic WAN partition split-brain isolation.
/// </summary>
public interface ISplitBrainQuorumArbitratorService
{
    /// <summary>
    /// Current Raft-inspired cluster consensus epoch term.
    /// </summary>
    long CurrentTerm { get; }

    /// <summary>
    /// Current recognized write leader node identifier.
    /// </summary>
    string? CurrentLeaderId { get; }

    /// <summary>
    /// Indicates whether a valid write lease is currently active in the cluster.
    /// </summary>
    bool HasWriteLease { get; }

    /// <summary>
    /// Processes a heartbeat from a mesh node, evaluating term progression and freshness.
    /// Stale heartbeats from older terms are rejected.
    /// </summary>
    bool ProcessHeartbeat(QuorumHeartbeat heartbeat);

    /// <summary>
    /// Evaluates whether a candidate leader and reachable node subset satisfy strict majority quorum (N / 2 + 1).
    /// If a WAN partition is detected, isolated minority nodes are immediately revoked and marked PartitionedIsolated (read-only).
    /// </summary>
    SplitBrainQuorumDecision EvaluateWriteQuorum(string candidateLeaderId, IEnumerable<string> reachableNodeIds);

    /// <summary>
    /// Retrieves current quorum and consensus state snapshot.
    /// </summary>
    QuorumStateResponse GetQuorumState();

    /// <summary>
    /// Registers a voting node identifier in the consensus membership set.
    /// </summary>
    void RegisterVotingNode(string nodeId);

    /// <summary>
    /// Unregisters a voting node identifier from the consensus membership set.
    /// </summary>
    void UnregisterVotingNode(string nodeId);
}
