using System.Collections.Concurrent;

namespace SGrowing.Api.Modules.Field.Sync;

/// <summary>
/// Service interface for edge-to-cloud CRDT manifest delta synchronization,
/// conflict-free replication, and offline recovery with resume tokens.
/// </summary>
public interface ICrdtStateReplicatorService
{
    VectorClock ServerClock { get; }

    Task<SyncBatchResponse> ReplicateAsync(SyncBatchRequest request, CancellationToken ct = default);

    Task<FieldManifestRecord?> GetManifestSnapshotAsync(Guid manifestId, CancellationToken ct = default);

    Task<IReadOnlyList<FieldManifestRecord>> GetDriverManifestsSnapshotAsync(string driverId, CancellationToken ct = default);

    Task<FieldManifestRecord> SubmitProofOfDeliveryAsync(Guid manifestId, Guid? stopId, ProofOfDeliveryData pod, string nodeId, CancellationToken ct = default);

    Task<FieldManifestRecord> UpsertManifestAsync(FieldManifestRecord manifest, CancellationToken ct = default);

    FieldManifestRecord MergeManifests(FieldManifestRecord a, FieldManifestRecord b);

    bool VerifyInvariants(FieldManifestRecord a, FieldManifestRecord b, FieldManifestRecord c);

    IReadOnlyList<(long FromSeq, long ToSeq)> GetUnacknowledgedRanges(string deviceId);

    void AcknowledgeRange(string deviceId, long upToSequence);

    CrdtReplicatorHealth GetHealth();
}

/// <summary>
/// Session tracking for a connected edge device to handle intermittent network disconnections.
/// </summary>
public sealed class ClientSyncSession
{
    public string DeviceId { get; }
    public long LastAcknowledgedSequence { get; set; }
    public List<(long FromSeq, long ToSeq)> UnacknowledgedRanges { get; } = [];
    public DateTimeOffset LastSyncUtc { get; set; } = DateTimeOffset.UtcNow;

    public ClientSyncSession(string deviceId)
    {
        DeviceId = deviceId;
    }
}

/// <summary>
/// State-based CRDT Replicator implementation providing delta accumulation,
/// bidirectional push/pull sync, resume token pagination, and algebraic join-semilattice properties.
/// </summary>
public sealed class CrdtStateReplicatorService : ICrdtStateReplicatorService
{
    private const string ServerNodeId = "server-replicator";

    private readonly ConcurrentDictionary<Guid, FieldManifestRecord> _manifests = new();
    private readonly List<ManifestDeltaItem> _deltaLog = [];
    private readonly object _deltaLock = new();
    private readonly HashSet<Guid> _processedDeltaIds = [];
    private readonly ConcurrentDictionary<string, ClientSyncSession> _sessions = new(StringComparer.Ordinal);
    private readonly VectorClock _serverClock = new();

    public VectorClock ServerClock => _serverClock.Clone();

    public CrdtStateReplicatorService()
    {
        _serverClock.Increment(ServerNodeId);
    }

    /// <summary>
    /// Performs bidirectional push/pull delta replication with an edge device.
    /// Ingests client deltas idempotently, resolves conflicts via CRDT rules,
    /// and streams unacknowledged server deltas with resume tokens.
    /// </summary>
    public Task<SyncBatchResponse> ReplicateAsync(SyncBatchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeviceId);

        var session = _sessions.GetOrAdd(request.DeviceId, id => new ClientSyncSession(id));
        session.LastSyncUtc = DateTimeOffset.UtcNow;

        // 1. Process resume token if client provided one
        long resumeStartSequence = 0;
        if (!string.IsNullOrWhiteSpace(request.ResumeToken))
        {
            if (TryParseResumeToken(request.ResumeToken, out var tokenDeviceId, out var seq))
            {
                if (string.Equals(tokenDeviceId, request.DeviceId, StringComparison.Ordinal))
                {
                    resumeStartSequence = seq;
                    AcknowledgeRange(request.DeviceId, seq);
                }
            }
        }

        // 2. Ingest client deltas idempotently and merge into manifests
        int appliedCount = 0;
        if (request.Deltas is not null && request.Deltas.Count > 0)
        {
            foreach (var clientDelta in request.Deltas)
            {
                lock (_deltaLock)
                {
                    if (_processedDeltaIds.Contains(clientDelta.DeltaId))
                    {
                        continue;
                    }
                    _processedDeltaIds.Add(clientDelta.DeltaId);
                }

                _serverClock.Increment(ServerNodeId).MergeInPlace(clientDelta.VectorClock);

                var manifest = _manifests.GetOrAdd(clientDelta.ManifestId, id => new FieldManifestRecord
                {
                    ManifestId = id,
                    DriverId = "unassigned",
                    Status = ManifestStatus.Draft,
                    VectorClock = clientDelta.VectorClock.Clone(),
                    LastModifiedUtc = clientDelta.Timestamp,
                    LastModifiedByNodeId = clientDelta.NodeId
                });

                var updated = ApplyDeltaToManifest(manifest, clientDelta);
                _manifests[clientDelta.ManifestId] = updated;

                lock (_deltaLock)
                {
                    _deltaLog.Add(clientDelta);
                }
                appliedCount++;
            }
        }

        // 3. Compute server deltas to stream back to the edge client
        List<ManifestDeltaItem> serverDeltas = [];
        bool hasMore = false;
        string? nextResumeToken = null;
        int batchSize = request.BatchSize.GetValueOrDefault(50);
        if (batchSize <= 0) batchSize = 50;

        lock (_deltaLock)
        {
            int totalDeltas = _deltaLog.Count;
            int startIndex = (int)Math.Min(resumeStartSequence, totalDeltas);

            for (int i = startIndex; i < totalDeltas; i++)
            {
                var d = _deltaLog[i];
                // Do not echo client's own writes back to it
                if (string.Equals(d.NodeId, request.DeviceId, StringComparison.Ordinal))
                {
                    continue;
                }

                // If client clock already dominates or equals this delta's clock, client already saw it
                if (request.ClientClock.CompareTo(d.VectorClock) == CausalityRelation.Dominates ||
                    request.ClientClock.CompareTo(d.VectorClock) == CausalityRelation.Equal)
                {
                    continue;
                }

                serverDeltas.Add(d);
                if (serverDeltas.Count >= batchSize)
                {
                    if (i + 1 < totalDeltas)
                    {
                        hasMore = true;
                        long nextSeq = i + 1;
                        nextResumeToken = FormatResumeToken(request.DeviceId, nextSeq);
                        session.UnacknowledgedRanges.Add((startIndex, nextSeq));
                    }
                    break;
                }
            }
        }

        return Task.FromResult(new SyncBatchResponse(
            ServerClock: _serverClock.Clone(),
            AppliedCount: appliedCount,
            ServerDeltas: serverDeltas,
            ResumeToken: nextResumeToken,
            HasMore: hasMore
        ));
    }

    /// <summary>
    /// Applies an individual delta operation to a manifest using deterministic CRDT rules.
    /// </summary>
    private static FieldManifestRecord ApplyDeltaToManifest(FieldManifestRecord manifest, ManifestDeltaItem delta)
    {
        var mergedClock = manifest.VectorClock.Merge(delta.VectorClock);

        return delta.DeltaType switch
        {
            ManifestDeltaType.ManifestCreated => manifest with
            {
                LastModifiedUtc = delta.Timestamp > manifest.LastModifiedUtc ? delta.Timestamp : manifest.LastModifiedUtc,
                LastModifiedByNodeId = delta.NodeId,
                VectorClock = mergedClock
            },
            ManifestDeltaType.ManifestStatusUpdated when delta.NewStatus.HasValue =>
                (delta.Timestamp >= manifest.LastModifiedUtc ||
                 (delta.Timestamp == manifest.LastModifiedUtc && string.Compare(delta.NodeId, manifest.LastModifiedByNodeId, StringComparison.Ordinal) > 0))
                    ? manifest with
                    {
                        Status = delta.NewStatus.Value,
                        LastModifiedUtc = delta.Timestamp,
                        LastModifiedByNodeId = delta.NodeId,
                        VectorClock = mergedClock
                    }
                    : manifest with { VectorClock = mergedClock },

            ManifestDeltaType.StopAdded when delta.StopData is not null =>
                manifest.WithStop(delta.StopData, delta.Tag, delta.NodeId) with { VectorClock = mergedClock },

            ManifestDeltaType.StopUpdated when delta.StopData is not null =>
                manifest.WithStop(delta.StopData, delta.Tag, delta.NodeId) with { VectorClock = mergedClock },

            ManifestDeltaType.StopRemoved when delta.StopData is not null =>
                manifest.RemoveStop(delta.StopData, delta.NodeId) with { VectorClock = mergedClock },

            ManifestDeltaType.StopRemoved when delta.StopId.HasValue =>
                RemoveStopById(manifest, delta.StopId.Value, delta.NodeId, mergedClock),

            ManifestDeltaType.ProofOfDeliverySubmitted when delta.PodData is not null =>
                ApplyPodToManifest(manifest, delta, mergedClock),

            _ => manifest with { VectorClock = mergedClock }
        };
    }

    private static FieldManifestRecord RemoveStopById(FieldManifestRecord manifest, Guid stopId, string nodeId, VectorClock mergedClock)
    {
        var target = manifest.Stops.Elements.FirstOrDefault(s => s.StopId == stopId);
        if (target is not null)
        {
            return manifest.RemoveStop(target, nodeId) with { VectorClock = mergedClock };
        }
        return manifest with { VectorClock = mergedClock };
    }

    private static FieldManifestRecord ApplyPodToManifest(FieldManifestRecord manifest, ManifestDeltaItem delta, VectorClock mergedClock)
    {
        var updated = manifest.WithPod(delta.PodData!, delta.NodeId) with { VectorClock = mergedClock };
        if (delta.StopId.HasValue)
        {
            var target = updated.Stops.Elements.FirstOrDefault(s => s.StopId == delta.StopId.Value);
            if (target is not null)
            {
                var deliveredStop = target.WithPod(delta.PodData!);
                updated = updated.WithStop(deliveredStop, delta.Tag, delta.NodeId) with { VectorClock = mergedClock };
            }
        }

        // If all stops are now delivered, advance manifest to Completed
        if (updated.Stops.Count > 0 && updated.Stops.Elements.All(s => s.Status == StopStatus.Delivered))
        {
            updated = updated with { Status = ManifestStatus.Completed };
        }

        return updated;
    }

    public Task<FieldManifestRecord?> GetManifestSnapshotAsync(Guid manifestId, CancellationToken ct = default)
    {
        _manifests.TryGetValue(manifestId, out var manifest);
        return Task.FromResult(manifest);
    }

    public Task<IReadOnlyList<FieldManifestRecord>> GetDriverManifestsSnapshotAsync(string driverId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        IReadOnlyList<FieldManifestRecord> results = _manifests.Values
            .Where(m => string.Equals(m.DriverId, driverId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<FieldManifestRecord> SubmitProofOfDeliveryAsync(Guid manifestId, Guid? stopId, ProofOfDeliveryData pod, string nodeId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pod);
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        _serverClock.Increment(ServerNodeId);

        var manifest = _manifests.GetOrAdd(manifestId, id => new FieldManifestRecord
        {
            ManifestId = id,
            DriverId = "unassigned",
            Status = ManifestStatus.EnRoute,
            VectorClock = _serverClock.Clone(),
            LastModifiedUtc = DateTimeOffset.UtcNow,
            LastModifiedByNodeId = nodeId
        });

        var delta = new ManifestDeltaItem(
            DeltaId: Guid.NewGuid(),
            ManifestId: manifestId,
            DeltaType: ManifestDeltaType.ProofOfDeliverySubmitted,
            Timestamp: pod.DeliveredAt ?? DateTimeOffset.UtcNow,
            NodeId: nodeId,
            VectorClock: _serverClock.Clone(),
            StopId: stopId,
            PodData: pod
        );

        var updated = ApplyDeltaToManifest(manifest, delta);
        _manifests[manifestId] = updated;

        lock (_deltaLock)
        {
            _processedDeltaIds.Add(delta.DeltaId);
            _deltaLog.Add(delta);
        }

        return Task.FromResult(updated);
    }

    public Task<FieldManifestRecord> UpsertManifestAsync(FieldManifestRecord manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        _serverClock.Increment(ServerNodeId);
        _manifests.AddOrUpdate(manifest.ManifestId, manifest, (_, existing) => MergeManifests(existing, manifest));
        return Task.FromResult(_manifests[manifest.ManifestId]);
    }

    /// <summary>
    /// Pure, deterministic CRDT merge for two FieldManifestRecord replicas.
    /// Strictly satisfies:
    /// - Commutativity: merge(A, B) == merge(B, A)
    /// - Associativity: merge(merge(A, B), C) == merge(A, merge(B, C))
    /// - Idempotency: merge(A, A) == A
    /// </summary>
    public FieldManifestRecord MergeManifests(FieldManifestRecord a, FieldManifestRecord b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        var targetId = a.ManifestId != Guid.Empty ? a.ManifestId : b.ManifestId;
        var mergedClock = a.VectorClock.Merge(b.VectorClock);
        var mergedStops = a.Stops.Merge(b.Stops);

        // Resolve status, driver and last modified metadata using deterministic LWW total ordering
        int cmp = a.LastModifiedUtc.CompareTo(b.LastModifiedUtc);
        if (cmp == 0)
        {
            cmp = string.Compare(a.LastModifiedByNodeId, b.LastModifiedByNodeId, StringComparison.Ordinal);
        }

        var winner = cmp >= 0 ? a : b;
        var loser = cmp >= 0 ? b : a;

        var effectiveDriverId = !string.IsNullOrWhiteSpace(winner.DriverId) && winner.DriverId != "unassigned"
            ? winner.DriverId
            : loser.DriverId;

        // Resolve POD data deterministically
        ProofOfDeliveryData? mergedPod;
        if (a.ProofOfDeliveryData is null && b.ProofOfDeliveryData is null)
        {
            mergedPod = null;
        }
        else if (a.ProofOfDeliveryData is not null && b.ProofOfDeliveryData is null)
        {
            mergedPod = a.ProofOfDeliveryData;
        }
        else if (a.ProofOfDeliveryData is null && b.ProofOfDeliveryData is not null)
        {
            mergedPod = b.ProofOfDeliveryData;
        }
        else
        {
            var tsA = a.ProofOfDeliveryData!.DeliveredAt ?? a.LastModifiedUtc;
            var tsB = b.ProofOfDeliveryData!.DeliveredAt ?? b.LastModifiedUtc;
            int podCmp = tsA.CompareTo(tsB);
            if (podCmp == 0)
            {
                podCmp = string.Compare(a.LastModifiedByNodeId, b.LastModifiedByNodeId, StringComparison.Ordinal);
            }
            mergedPod = podCmp >= 0 ? a.ProofOfDeliveryData : b.ProofOfDeliveryData;
        }

        return new FieldManifestRecord(
            manifestId: targetId,
            driverId: effectiveDriverId,
            status: winner.Status,
            vectorClock: mergedClock,
            stops: mergedStops,
            proofOfDeliveryData: mergedPod,
            lastModifiedUtc: winner.LastModifiedUtc,
            lastModifiedByNodeId: winner.LastModifiedByNodeId
        );
    }

    /// <summary>
    /// Verifies the three fundamental algebraic laws of CRDT join-semilattices:
    /// Commutativity, Associativity, and Idempotency.
    /// </summary>
    public bool VerifyInvariants(FieldManifestRecord a, FieldManifestRecord b, FieldManifestRecord c)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(c);

        // 1. Commutativity: merge(A, B) == merge(B, A)
        var ab = MergeManifests(a, b);
        var ba = MergeManifests(b, a);
        if (!ab.Equals(ba)) return false;

        // 2. Associativity: merge(merge(A, B), C) == merge(A, merge(B, C))
        var ab_c = MergeManifests(ab, c);
        var bc = MergeManifests(b, c);
        var a_bc = MergeManifests(a, bc);
        if (!ab_c.Equals(a_bc)) return false;

        // 3. Idempotency: merge(A, A) == A
        var aa = MergeManifests(a, a);
        if (!aa.Equals(a)) return false;

        return true;
    }

    public IReadOnlyList<(long FromSeq, long ToSeq)> GetUnacknowledgedRanges(string deviceId)
    {
        if (_sessions.TryGetValue(deviceId, out var session))
        {
            return [.. session.UnacknowledgedRanges];
        }
        return [];
    }

    public void AcknowledgeRange(string deviceId, long upToSequence)
    {
        if (_sessions.TryGetValue(deviceId, out var session))
        {
            session.LastAcknowledgedSequence = Math.Max(session.LastAcknowledgedSequence, upToSequence);
            session.UnacknowledgedRanges.RemoveAll(r => r.ToSeq <= upToSequence);
        }
    }

    public CrdtReplicatorHealth GetHealth()
    {
        int deltaCount;
        lock (_deltaLock)
        {
            deltaCount = _deltaLog.Count;
        }

        return new CrdtReplicatorHealth(
            Status: "Healthy",
            ActiveManifestsCount: _manifests.Count,
            TotalDeltasAccumulated: deltaCount,
            ActiveSyncSessionsCount: _sessions.Count,
            ServerTimestampUtc: DateTimeOffset.UtcNow
        );
    }

    private static string FormatResumeToken(string deviceId, long nextSequence) =>
        $"res:{deviceId}:{nextSequence}:{Guid.NewGuid():N}";

    private static bool TryParseResumeToken(string token, out string deviceId, out long nextSequence)
    {
        deviceId = string.Empty;
        nextSequence = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split(':');
        if (parts.Length >= 3 && parts[0] == "res" && long.TryParse(parts[2], out var seq))
        {
            deviceId = parts[1];
            nextSequence = seq;
            return true;
        }
        return false;
    }
}
