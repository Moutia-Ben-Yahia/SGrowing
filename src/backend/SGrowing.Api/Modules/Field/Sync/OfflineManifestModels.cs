using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Field.Sync;

/// <summary>
/// Lifecycle status of a field delivery manifest.
/// </summary>
public enum ManifestStatus
{
    Draft = 0,
    Assigned = 1,
    EnRoute = 2,
    Completed = 3
}

/// <summary>
/// Status of an individual delivery stop.
/// </summary>
public enum StopStatus
{
    Pending = 0,
    Delivered = 1,
    Failed = 2,
    Rescheduled = 3
}

/// <summary>
/// Cryptographic and contextual Proof-of-Delivery (POD) data captured offline.
/// </summary>
public sealed record ProofOfDeliveryData(
    string? SignaturePath = null,
    string? Geocode = null,
    string? PhotoSha256 = null,
    DateTimeOffset? DeliveredAt = null,
    string? SignerName = null,
    string? Notes = null
);

/// <summary>
/// Record representing an individual delivery stop on a driver's route.
/// </summary>
public sealed record DeliveryStopRecord(
    Guid StopId,
    string TrackingNumber,
    string RecipientName,
    string Address,
    decimal CodAmount,
    StopStatus Status = StopStatus.Pending,
    int AttemptCount = 0,
    DateTimeOffset? CompletedAt = null,
    string? FailureReason = null,
    ProofOfDeliveryData? Pod = null
)
{
    public DeliveryStopRecord WithStatus(StopStatus newStatus, DateTimeOffset? completedAt = null, string? failureReason = null) =>
        this with
        {
            Status = newStatus,
            CompletedAt = completedAt ?? (newStatus == StopStatus.Delivered ? DateTimeOffset.UtcNow : CompletedAt),
            FailureReason = failureReason ?? FailureReason
        };

    public DeliveryStopRecord WithAttempt() =>
        this with { AttemptCount = AttemptCount + 1 };

    public DeliveryStopRecord WithPod(ProofOfDeliveryData pod) =>
        this with
        {
            Pod = pod,
            Status = StopStatus.Delivered,
            CompletedAt = pod.DeliveredAt ?? DateTimeOffset.UtcNow
        };
}

/// <summary>
/// Replicated field manifest containing driver stops and state managed by CRDT primitives.
/// </summary>
public sealed record FieldManifestRecord
{
    public Guid ManifestId { get; init; }
    public string DriverId { get; init; } = string.Empty;
    public ManifestStatus Status { get; init; } = ManifestStatus.Draft;
    public VectorClock VectorClock { get; init; } = new();
    public AwOrSet<DeliveryStopRecord> Stops { get; init; } = new();
    public ProofOfDeliveryData? ProofOfDeliveryData { get; init; }
    public DateTimeOffset LastModifiedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string LastModifiedByNodeId { get; init; } = "server";

    public FieldManifestRecord()
    {
    }

    [JsonConstructor]
    public FieldManifestRecord(
        Guid manifestId,
        string driverId,
        ManifestStatus status,
        VectorClock? vectorClock,
        AwOrSet<DeliveryStopRecord>? stops,
        ProofOfDeliveryData? proofOfDeliveryData = null,
        DateTimeOffset? lastModifiedUtc = null,
        string lastModifiedByNodeId = "server")
    {
        ManifestId = manifestId;
        DriverId = driverId;
        Status = status;
        VectorClock = vectorClock ?? new();
        Stops = stops ?? new();
        ProofOfDeliveryData = proofOfDeliveryData;
        LastModifiedUtc = lastModifiedUtc ?? DateTimeOffset.UtcNow;
        LastModifiedByNodeId = lastModifiedByNodeId;
    }

    /// <summary>
    /// Returns a new manifest with updated status and node tracking.
    /// </summary>
    public FieldManifestRecord WithStatus(ManifestStatus newStatus, string nodeId) =>
        this with
        {
            Status = newStatus,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            LastModifiedByNodeId = nodeId,
            VectorClock = VectorClock.WithIncrement(nodeId)
        };

    /// <summary>
    /// Adds or updates a stop in the manifest's AW-ORSet.
    /// </summary>
    public FieldManifestRecord WithStop(DeliveryStopRecord stop, string? tag = null, string nodeId = "edge")
    {
        var newStops = new AwOrSet<DeliveryStopRecord>(Stops.Additions, Stops.Removals);
        // If an existing stop with this StopId exists in active elements, tombstone it
        foreach (var existing in Stops.Elements.Where(s => s.StopId == stop.StopId))
        {
            newStops.Remove(existing);
        }
        if (string.IsNullOrWhiteSpace(tag))
        {
            newStops.Add(stop);
        }
        else
        {
            newStops.Add(stop, tag);
        }

        return this with
        {
            Stops = newStops,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            LastModifiedByNodeId = nodeId,
            VectorClock = VectorClock.WithIncrement(nodeId)
        };
    }

    /// <summary>
    /// Removes a stop from the manifest's AW-ORSet.
    /// </summary>
    public FieldManifestRecord RemoveStop(DeliveryStopRecord stop, string nodeId = "edge")
    {
        var newStops = new AwOrSet<DeliveryStopRecord>(Stops.Additions, Stops.Removals);
        newStops.Remove(stop);

        return this with
        {
            Stops = newStops,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            LastModifiedByNodeId = nodeId,
            VectorClock = VectorClock.WithIncrement(nodeId)
        };
    }

    /// <summary>
    /// Attaches Proof-of-Delivery data to the manifest.
    /// </summary>
    public FieldManifestRecord WithPod(ProofOfDeliveryData pod, string nodeId = "edge") =>
        this with
        {
            ProofOfDeliveryData = pod,
            LastModifiedUtc = DateTimeOffset.UtcNow,
            LastModifiedByNodeId = nodeId,
            VectorClock = VectorClock.WithIncrement(nodeId)
        };
}

/// <summary>
/// The specific operation or transition conveyed in a manifest delta item.
/// </summary>
public enum ManifestDeltaType
{
    ManifestCreated = 0,
    ManifestStatusUpdated = 1,
    StopAdded = 2,
    StopUpdated = 3,
    StopRemoved = 4,
    ProofOfDeliverySubmitted = 5
}

/// <summary>
/// A discreet delta unit capturing a field change along with causality clock and node origin.
/// </summary>
public sealed record ManifestDeltaItem(
    Guid DeltaId,
    Guid ManifestId,
    ManifestDeltaType DeltaType,
    DateTimeOffset Timestamp,
    string NodeId,
    VectorClock VectorClock,
    Guid? StopId = null,
    DeliveryStopRecord? StopData = null,
    ProofOfDeliveryData? PodData = null,
    ManifestStatus? NewStatus = null,
    string? Tag = null,
    string? Details = null
);

/// <summary>
/// Batch payload sent by an edge client (mobile scanner, tablet) to the sync engine.
/// </summary>
public sealed record SyncBatchRequest(
    string DeviceId,
    VectorClock ClientClock,
    IReadOnlyList<ManifestDeltaItem> Deltas,
    string? ResumeToken = null,
    int? BatchSize = null
);

/// <summary>
/// Response payload returned by the sync engine to the edge client.
/// </summary>
public sealed record SyncBatchResponse(
    VectorClock ServerClock,
    int AppliedCount,
    IReadOnlyList<ManifestDeltaItem> ServerDeltas,
    string? ResumeToken = null,
    bool HasMore = false
);

/// <summary>
/// Request payload for submitting proof of delivery.
/// </summary>
public sealed record SubmitPodRequest(
    Guid? StopId,
    ProofOfDeliveryData PodData,
    string? NodeId = null
);

/// <summary>
/// Health check and metrics for the CRDT delta sync engine.
/// </summary>
public sealed record CrdtReplicatorHealth(
    string Status,
    int ActiveManifestsCount,
    int TotalDeltasAccumulated,
    int ActiveSyncSessionsCount,
    DateTimeOffset ServerTimestampUtc,
    string EngineVersion = "1.0.0-crdt"
);
