namespace SGrowing.Api.Modules.Infrastructure.MultiCloud;

/// <summary>
/// Raft-inspired Byzantine distributed consensus arbitrator enforcing strict majority
/// write quorum and automatic WAN partition split-brain isolation.
/// </summary>
public class SplitBrainQuorumArbitratorService(IMultiCloudMeshGatewayService? gateway = null) : ISplitBrainQuorumArbitratorService
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _explicitVotingNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (QuorumHeartbeat Heartbeat, DateTimeOffset ReceivedUtc)> _latestHeartbeats = new(StringComparer.OrdinalIgnoreCase);
    private long _currentTerm = 1;
    private string? _currentLeaderId;
    private bool _hasWriteLease;
    private DateTimeOffset _lastEvaluationUtc = DateTimeOffset.UtcNow;

    /// <summary>
    /// Heartbeat expiration window. Nodes that fail to report within this window are marked inactive.
    /// </summary>
    public TimeSpan HeartbeatTtl { get; set; } = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public long CurrentTerm
    {
        get
        {
            lock (_lock)
            {
                return _currentTerm;
            }
        }
    }

    /// <inheritdoc />
    public string? CurrentLeaderId
    {
        get
        {
            lock (_lock)
            {
                return _currentLeaderId;
            }
        }
    }

    /// <inheritdoc />
    public bool HasWriteLease
    {
        get
        {
            lock (_lock)
            {
                return _hasWriteLease;
            }
        }
    }

    /// <inheritdoc />
    public void RegisterVotingNode(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        lock (_lock)
        {
            _explicitVotingNodes.Add(nodeId);
        }
    }

    /// <inheritdoc />
    public void UnregisterVotingNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        lock (_lock)
        {
            _explicitVotingNodes.Remove(nodeId);
            _latestHeartbeats.Remove(nodeId);
        }
    }

    /// <inheritdoc />
    public bool ProcessHeartbeat(QuorumHeartbeat heartbeat)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        ArgumentException.ThrowIfNullOrWhiteSpace(heartbeat.NodeId);

        lock (_lock)
        {
            // Reject stale heartbeats with an older term
            if (heartbeat.Term < _currentTerm)
            {
                return false;
            }

            // Advance term if heartbeat introduces a higher epoch term
            if (heartbeat.Term > _currentTerm)
            {
                _currentTerm = heartbeat.Term;
                _currentLeaderId = heartbeat.KnownLeaderId;
                gateway?.SetCurrentTerm(_currentTerm);
            }
            else if (!string.IsNullOrWhiteSpace(heartbeat.KnownLeaderId))
            {
                _currentLeaderId = heartbeat.KnownLeaderId;
            }

            _explicitVotingNodes.Add(heartbeat.NodeId);
            _latestHeartbeats[heartbeat.NodeId] = (heartbeat, DateTimeOffset.UtcNow);

            return true;
        }
    }

    /// <inheritdoc />
    public SplitBrainQuorumDecision EvaluateWriteQuorum(string candidateLeaderId, IEnumerable<string> reachableNodeIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateLeaderId);
        ArgumentNullException.ThrowIfNull(reachableNodeIds);

        lock (_lock)
        {
            var allClusterNodes = GetClusterVotingNodeIds();
            int totalNodes = allClusterNodes.Count;

            // Strict majority formula: N / 2 + 1
            int majorityThreshold = (totalNodes / 2) + 1;

            var reachableSet = reachableNodeIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Byzantine Fault Check 1: Candidate leader must be reachable within its claimed subset
            if (!reachableSet.Contains(candidateLeaderId))
            {
                _hasWriteLease = false;
                var minority = allClusterNodes.Except(reachableSet).ToList();
                return new SplitBrainQuorumDecision(
                    GrantedWriteLease: false,
                    MajorityClusterNodes: [],
                    IsolatedMinorityNodes: minority,
                    QuorumTerm: _currentTerm,
                    PartitionDetected: true,
                    Reason: $"Byzantine fault detected: Candidate leader '{candidateLeaderId}' is not included in the reachable nodes set.");
            }

            // Byzantine Fault Check 2: Candidate node must not be in Offline or PartitionedIsolated state
            if (gateway is not null)
            {
                var candidateNode = gateway.GetNode(candidateLeaderId);
                if (candidateNode is not null && (candidateNode.HealthStatus == RegionNodeStatus.Offline || candidateNode.HealthStatus == RegionNodeStatus.PartitionedIsolated))
                {
                    _hasWriteLease = false;
                    return new SplitBrainQuorumDecision(
                        GrantedWriteLease: false,
                        MajorityClusterNodes: [],
                        IsolatedMinorityNodes: [candidateLeaderId],
                        QuorumTerm: _currentTerm,
                        PartitionDetected: true,
                        Reason: $"Byzantine fault detected: Candidate leader '{candidateLeaderId}' is currently {candidateNode.HealthStatus} and cannot hold a write lease.");
                }
            }

            bool satisfiesMajority = reachableSet.Count >= majorityThreshold;

            if (satisfiesMajority)
            {
                _hasWriteLease = true;
                _currentLeaderId = candidateLeaderId;
                _lastEvaluationUtc = DateTimeOffset.UtcNow;

                var majorityNodes = allClusterNodes.Intersect(reachableSet).OrderBy(x => x).ToList();
                var minorityNodes = allClusterNodes.Except(reachableSet).OrderBy(x => x).ToList();
                bool partitionDetected = minorityNodes.Count > 0;

                // WAN Partition Split-Brain handling:
                // Immediately isolate minority nodes in read-only mode to prevent split-brain corruption
                if (partitionDetected && gateway is not null)
                {
                    foreach (var minorityId in minorityNodes)
                    {
                        gateway.UpdateNodeHealth(minorityId, RegionNodeStatus.PartitionedIsolated, 9999.0);
                    }
                }

                gateway?.SetWriteLeader(candidateLeaderId);

                string reason = partitionDetected
                    ? $"Strict majority quorum achieved ({reachableSet.Count}/{totalNodes} nodes). Write lease granted to leader '{candidateLeaderId}'. {minorityNodes.Count} minority nodes isolated in read-only mode to prevent split-brain database corruption."
                    : $"Full cluster quorum active ({reachableSet.Count}/{totalNodes} nodes). Write lease granted to leader '{candidateLeaderId}'.";

                return new SplitBrainQuorumDecision(
                    GrantedWriteLease: true,
                    MajorityClusterNodes: majorityNodes,
                    IsolatedMinorityNodes: minorityNodes,
                    QuorumTerm: _currentTerm,
                    PartitionDetected: partitionDetected,
                    Reason: reason);
            }
            else
            {
                // Minority partition: revoke write lease and isolate to read-only
                _hasWriteLease = false;
                _lastEvaluationUtc = DateTimeOffset.UtcNow;

                var minorityNodes = allClusterNodes.Intersect(reachableSet).OrderBy(x => x).ToList();

                if (gateway is not null)
                {
                    gateway.ClearWriteLeader();
                    foreach (var minorityId in minorityNodes)
                    {
                        gateway.UpdateNodeHealth(minorityId, RegionNodeStatus.PartitionedIsolated, 9999.0);
                    }
                }

                string reason = $"Split-brain partition detected: Reachable nodes ({reachableSet.Count}/{totalNodes}) do not satisfy strict majority quorum (required: {majorityThreshold}). Write lease denied to prevent split-brain database corruption. Sub-cluster isolated to read-only.";

                return new SplitBrainQuorumDecision(
                    GrantedWriteLease: false,
                    MajorityClusterNodes: [],
                    IsolatedMinorityNodes: minorityNodes,
                    QuorumTerm: _currentTerm,
                    PartitionDetected: true,
                    Reason: reason);
            }
        }
    }

    /// <inheritdoc />
    public QuorumStateResponse GetQuorumState()
    {
        lock (_lock)
        {
            var allClusterNodes = GetClusterVotingNodeIds();
            int total = allClusterNodes.Count;
            int threshold = (total / 2) + 1;
            var now = DateTimeOffset.UtcNow;

            var activeNodes = new List<string>();
            var isolatedNodes = new List<string>();

            foreach (var nodeId in allClusterNodes)
            {
                bool isGatewayHealthy = gateway?.GetNode(nodeId)?.HealthStatus == RegionNodeStatus.Healthy;
                bool hasFreshHeartbeat = _latestHeartbeats.TryGetValue(nodeId, out var record) && (now - record.ReceivedUtc) <= HeartbeatTtl;

                if (isGatewayHealthy || hasFreshHeartbeat)
                {
                    activeNodes.Add(nodeId);
                }
                else
                {
                    isolatedNodes.Add(nodeId);
                }
            }

            return new QuorumStateResponse(
                CurrentTerm: _currentTerm,
                CurrentLeaderId: _currentLeaderId,
                QuorumThreshold: threshold,
                TotalVotingNodes: total,
                HasWriteLease: _hasWriteLease,
                ActiveNodeIds: activeNodes.AsReadOnly(),
                IsolatedNodeIds: isolatedNodes.AsReadOnly(),
                LastEvaluationUtc: _lastEvaluationUtc);
        }
    }

    private HashSet<string> GetClusterVotingNodeIds()
    {
        var set = new HashSet<string>(_explicitVotingNodes, StringComparer.OrdinalIgnoreCase);

        if (gateway is not null)
        {
            foreach (var node in gateway.GetAllNodes())
            {
                set.Add(node.NodeId);
            }
        }

        foreach (var key in _latestHeartbeats.Keys)
        {
            set.Add(key);
        }

        return set;
    }
}
