using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Tenancy.MultiRegion;

public interface IHlcReplicationCoordinator
{
    HybridLogicalClockRecord GetCurrentTime(string nodeId);
    HybridLogicalClockRecord GenerateNextTimestamp(string nodeId, long? physicalOverride = null);
    HybridLogicalClockRecord UpdateOnReceive(string nodeId, HybridLogicalClockRecord remoteTimestamp, long? physicalOverride = null);
    int CompareHlc(HybridLogicalClockRecord a, HybridLogicalClockRecord b);
    ReplicatedTransactionRecord ResolveConflict(ReplicatedTransactionRecord recordA, ReplicatedTransactionRecord recordB);
    Task<ReplicationSyncResponse> ReplicateTransactionAsync(ReplicationSyncRequest request);
    IReadOnlyList<ReplicatedTransactionRecord> GetTransactionJournal(string? entityId = null);
    void ClearJournal();
}

public class HlcReplicationCoordinator : IHlcReplicationCoordinator
{
    private readonly ISovereignDataShardingManager _sovereignManager;

    private class NodeClockState
    {
        public long L;
        public int C;
        public readonly object LockObj = new();
    }

    private readonly ConcurrentDictionary<string, NodeClockState> _clocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ReplicatedTransactionRecord> _latestEntityRecords = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<ReplicatedTransactionRecord> _journal = new();

    public HlcReplicationCoordinator(ISovereignDataShardingManager sovereignManager)
    {
        _sovereignManager = sovereignManager;
        InitializeStandardNodes();
    }

    private void InitializeStandardNodes()
    {
        GetOrAddClock("node-algiers-01");
        GetOrAddClock("node-paris-01");
        GetOrAddClock("node-dubai-01");
    }

    private NodeClockState GetOrAddClock(string nodeId)
    {
        return _clocks.GetOrAdd(nodeId, _ => new NodeClockState
        {
            L = 0,
            C = 0
        });
    }

    public HybridLogicalClockRecord GetCurrentTime(string nodeId)
    {
        var clock = GetOrAddClock(nodeId);
        lock (clock.LockObj)
        {
            var l = clock.L > 0 ? clock.L : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return new HybridLogicalClockRecord(l, clock.C, nodeId);
        }
    }

    public HybridLogicalClockRecord GenerateNextTimestamp(string nodeId, long? physicalOverride = null)
    {
        var clock = GetOrAddClock(nodeId);
        lock (clock.LockObj)
        {
            var pt = physicalOverride ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var lPrime = Math.Max(clock.L, pt);
            var cPrime = (lPrime == clock.L) ? clock.C + 1 : 0;

            clock.L = lPrime;
            clock.C = cPrime;

            return new HybridLogicalClockRecord(lPrime, cPrime, nodeId);
        }
    }

    public HybridLogicalClockRecord UpdateOnReceive(string nodeId, HybridLogicalClockRecord remoteTimestamp, long? physicalOverride = null)
    {
        var clock = GetOrAddClock(nodeId);
        lock (clock.LockObj)
        {
            var pt = physicalOverride ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var lPrime = Math.Max(clock.L, Math.Max(pt, remoteTimestamp.PhysicalTimestampMs));

            int cPrime;
            if (lPrime == clock.L && lPrime == remoteTimestamp.PhysicalTimestampMs)
            {
                cPrime = Math.Max(clock.C, remoteTimestamp.LogicalCounter) + 1;
            }
            else if (lPrime == clock.L)
            {
                cPrime = clock.C + 1;
            }
            else if (lPrime == remoteTimestamp.PhysicalTimestampMs)
            {
                cPrime = remoteTimestamp.LogicalCounter + 1;
            }
            else
            {
                cPrime = 0;
            }

            clock.L = lPrime;
            clock.C = cPrime;

            return new HybridLogicalClockRecord(lPrime, cPrime, nodeId);
        }
    }

    public int CompareHlc(HybridLogicalClockRecord a, HybridLogicalClockRecord b) => a.CompareTo(b);

    public ReplicatedTransactionRecord ResolveConflict(ReplicatedTransactionRecord recordA, ReplicatedTransactionRecord recordB)
    {
        var comparison = recordA.HlcTimestamp.CompareTo(recordB.HlcTimestamp);

        if (comparison > 0)
        {
            return recordA with { Status = ReplicationStatus.ConflictResolved };
        }

        if (comparison < 0)
        {
            return recordB with { Status = ReplicationStatus.ConflictResolved };
        }

        // Tie-breaker: deterministic ordinal string comparison of NodeId
        var nodeComp = string.CompareOrdinal(recordA.HlcTimestamp.NodeId, recordB.HlcTimestamp.NodeId);
        return nodeComp >= 0
            ? recordA with { Status = ReplicationStatus.ConflictResolved }
            : recordB with { Status = ReplicationStatus.ConflictResolved };
    }

    public async Task<ReplicationSyncResponse> ReplicateTransactionAsync(ReplicationSyncRequest request)
    {
        var txId = request.TxId ?? $"tx-{Guid.NewGuid():N}";
        var originNode = $"node-{request.OriginRegion.ToString().ToLowerInvariant()}-01";
        var targetNode = $"node-{request.TargetRegion.ToString().ToLowerInvariant()}-01";

        // 1. Sovereign Compliance Validation Gate
        var validation = await _sovereignManager.ValidateReplicationPermittedAsync(
            request.TenantId,
            request.OriginRegion,
            request.TargetRegion,
            request.IsPii,
            request.EntityType,
            request.EntityId
        );

        if (!validation.IsPermitted)
        {
            var blockedHlc = request.HlcTimestamp ?? GenerateNextTimestamp(originNode);
            return new ReplicationSyncResponse(
                TxId: txId,
                Status: ReplicationStatus.CommittedLocal,
                Success: false,
                Message: $"Sovereign boundary violation detected: {validation.ViolationReason}",
                FinalHlc: blockedHlc,
                WinnerRecord: null,
                SyncedAtUtc: DateTime.UtcNow,
                SovereignViolationDetected: true
            );
        }

        // 2. Advance HLC on target node or generate timestamp
        HybridLogicalClockRecord effectiveHlc;
        if (request.HlcTimestamp is not null)
        {
            effectiveHlc = UpdateOnReceive(targetNode, request.HlcTimestamp);
        }
        else
        {
            effectiveHlc = GenerateNextTimestamp(originNode);
        }

        // 3. Compute SHA256 integrity hash
        var sha256 = request.PayloadSha256 ?? ReplicatedTransactionRecord.ComputeSha256(request.PayloadJson);

        var incomingRecord = new ReplicatedTransactionRecord(
            TxId: txId,
            OriginRegion: request.OriginRegion,
            TargetRegion: request.TargetRegion,
            ShardId: request.ShardId,
            HlcTimestamp: request.HlcTimestamp ?? effectiveHlc,
            EntityType: request.EntityType,
            EntityId: request.EntityId,
            PayloadJson: request.PayloadJson,
            PayloadSha256: sha256,
            Status: request.OriginRegion == request.TargetRegion
                ? ReplicationStatus.CommittedLocal
                : ReplicationStatus.ReplicatedCrossRegion,
            CreatedAtUtc: DateTime.UtcNow,
            IsPii: request.IsPii
        );

        // 4. Active-Active Conflict Reconciliation
        var entityKey = $"{request.EntityType}:{request.EntityId}";
        ReplicatedTransactionRecord finalRecord;
        ReplicationStatus finalStatus;
        string message;

        if (_latestEntityRecords.TryGetValue(entityKey, out var existingRecord))
        {
            // Concurrent write detected on same entity
            var winner = ResolveConflict(incomingRecord, existingRecord);
            var isIncomingWinner = winner.TxId == incomingRecord.TxId;

            finalRecord = winner;
            finalStatus = ReplicationStatus.ConflictResolved;
            message = isIncomingWinner
                ? "Active-active conflict resolved: Incoming transaction won via HLC Last-Write-Wins."
                : "Active-active conflict resolved: Existing transaction won via HLC Last-Write-Wins.";

            _latestEntityRecords[entityKey] = finalRecord;
        }
        else
        {
            finalRecord = incomingRecord;
            finalStatus = incomingRecord.Status;
            message = request.OriginRegion == request.TargetRegion
                ? "Transaction committed locally."
                : "Transaction successfully replicated cross-region with causality preserved.";

            _latestEntityRecords[entityKey] = finalRecord;
        }

        _journal.Add(finalRecord);

        return new ReplicationSyncResponse(
            TxId: txId,
            Status: finalStatus,
            Success: true,
            Message: message,
            FinalHlc: effectiveHlc,
            WinnerRecord: finalRecord,
            SyncedAtUtc: DateTime.UtcNow,
            SovereignViolationDetected: false
        );
    }

    public IReadOnlyList<ReplicatedTransactionRecord> GetTransactionJournal(string? entityId = null)
    {
        if (!string.IsNullOrWhiteSpace(entityId))
        {
            return _journal.Where(r => r.EntityId == entityId).OrderByDescending(r => r.CreatedAtUtc).ToList();
        }
        return _journal.OrderByDescending(r => r.CreatedAtUtc).ToList();
    }

    public void ClearJournal()
    {
        _journal.Clear();
        _latestEntityRecords.Clear();
        _clocks.Clear();
    }
}
