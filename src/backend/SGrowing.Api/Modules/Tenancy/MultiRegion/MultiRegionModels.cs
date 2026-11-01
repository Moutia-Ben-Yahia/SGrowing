using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace SGrowing.Api.Modules.Tenancy.MultiRegion;

public enum RegionIdentifier
{
    AfNorth1 = 1,   // Algiers, North Africa ("af-north-1")
    EuWest3 = 2,    // Paris, Europe ("eu-west-3")
    MeCentral1 = 3  // Dubai, Middle East ("me-central-1")
}

public enum SovereignJurisdiction
{
    Algeria_Law1807 = 1, // Algerian Law 18-07 on personal data protection & sovereignty
    Eu_Gdpr = 2,         // European Union General Data Protection Regulation (GDPR)
    Uae_DIFC = 3,        // UAE DIFC / Federal Data Protection
    Global_Default = 4   // Standard cross-border multi-region replication
}

public enum DataLocalityStrictness
{
    AbsoluteSovereign = 1, // Zero cross-border egress permitted. Strict territorial residency.
    RegionalPreferred = 2, // Primary localized; read-only cross-region allowed within trusted zone.
    GlobalFloating = 3     // Fully dynamic latency-based routing & global active-active replication.
}

public enum ReplicationStatus
{
    CommittedLocal = 1,
    ReplicatedCrossRegion = 2,
    ConflictResolved = 3,
    RejectedComplianceViolation = 4
}

public enum ShardHealthState
{
    Healthy = 1,
    Degraded = 2,
    Maintenance = 3,
    Offline = 4
}

public record RegionalShardMetadata(
    string ShardId,
    RegionIdentifier Region,
    SovereignJurisdiction Jurisdiction,
    string PrimaryEndpoint,
    IReadOnlyList<string> ReadReplicas,
    double LatencyScoreMs,
    bool IsActive,
    ShardHealthState HealthState = ShardHealthState.Healthy,
    double Latitude = 0.0,
    double Longitude = 0.0
);

public record TenantDataResidencyPolicy(
    Guid TenantId,
    RegionIdentifier HomeRegion,
    SovereignJurisdiction EnforcedJurisdiction,
    bool AllowCrossRegionReadReplicas,
    DataLocalityStrictness DataLocalityStrictness,
    DateTime ConfiguredAtUtc,
    string ConfiguredBy = "System",
    IReadOnlyList<string>? BlockedExportRegions = null
)
{
    public IReadOnlyList<string> BlockedExportRegions { get; init; } = BlockedExportRegions ?? [];
}

public record HybridLogicalClockRecord(
    long PhysicalTimestampMs,
    int LogicalCounter,
    string NodeId
) : IComparable<HybridLogicalClockRecord>
{
    public int CompareTo(HybridLogicalClockRecord? other)
    {
        if (other is null) return 1;

        var physComp = PhysicalTimestampMs.CompareTo(other.PhysicalTimestampMs);
        if (physComp != 0) return physComp;

        var logicalComp = LogicalCounter.CompareTo(other.LogicalCounter);
        if (logicalComp != 0) return logicalComp;

        return string.CompareOrdinal(NodeId, other.NodeId);
    }

    public static bool operator <(HybridLogicalClockRecord? left, HybridLogicalClockRecord? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(HybridLogicalClockRecord? left, HybridLogicalClockRecord? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(HybridLogicalClockRecord? left, HybridLogicalClockRecord? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(HybridLogicalClockRecord? left, HybridLogicalClockRecord? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    public override string ToString() => $"{PhysicalTimestampMs}:{LogicalCounter}:{NodeId}";

    public static bool TryParse(string input, out HybridLogicalClockRecord? record)
    {
        record = null;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var parts = input.Split(':');
        if (parts.Length != 3) return false;

        if (long.TryParse(parts[0], out var phys) &&
            int.TryParse(parts[1], out var counter))
        {
            record = new HybridLogicalClockRecord(phys, counter, parts[2]);
            return true;
        }

        return false;
    }
}

public record ReplicatedTransactionRecord(
    string TxId,
    RegionIdentifier OriginRegion,
    RegionIdentifier TargetRegion,
    string ShardId,
    HybridLogicalClockRecord HlcTimestamp,
    string EntityType,
    string EntityId,
    string PayloadJson,
    string PayloadSha256,
    ReplicationStatus Status,
    DateTime CreatedAtUtc = default,
    bool IsPii = false
)
{
    public static string ComputeSha256(string rawPayload)
    {
        var bytes = Encoding.UTF8.GetBytes(rawPayload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public record GeoRoutingDecision(
    string ClientIp,
    string DetectedCountry,
    RegionIdentifier TargetRegion,
    string SelectedShardId,
    double EstimatedLatencyMs,
    bool ComplianceVerified,
    string RoutingReason = "",
    bool FallbackApplied = false
);

public record SovereignViolationAuditRecord(
    Guid AuditId,
    Guid TenantId,
    RegionIdentifier AttemptedOrigin,
    RegionIdentifier AttemptedTarget,
    SovereignJurisdiction ViolatedJurisdiction,
    string EntityType,
    string EntityId,
    string Reason,
    DateTime TimestampUtc
);

public record SovereignValidationResult(
    bool IsPermitted,
    string? ViolationReason,
    SovereignJurisdiction Jurisdiction,
    bool AuditLogged
);

public record GeoRouteRequest(
    string? ClientIp,
    string? CountryCode,
    Guid? TenantId
);

public record RegisterResidencyPolicyRequest(
    Guid TenantId,
    RegionIdentifier HomeRegion,
    SovereignJurisdiction EnforcedJurisdiction,
    bool AllowCrossRegionReadReplicas,
    DataLocalityStrictness DataLocalityStrictness,
    string? ConfiguredBy = "Admin"
);

public record ReplicationSyncRequest(
    string? TxId,
    Guid? TenantId,
    RegionIdentifier OriginRegion,
    RegionIdentifier TargetRegion,
    string ShardId,
    HybridLogicalClockRecord? HlcTimestamp,
    string EntityType,
    string EntityId,
    string PayloadJson,
    string? PayloadSha256 = null,
    bool IsPii = false
);

public record ReplicationSyncResponse(
    string TxId,
    ReplicationStatus Status,
    bool Success,
    string Message,
    HybridLogicalClockRecord FinalHlc,
    ReplicatedTransactionRecord? WinnerRecord,
    DateTime SyncedAtUtc,
    bool SovereignViolationDetected
);

public record ShardStatusResponse(
    IReadOnlyList<RegionalShardMetadata> Shards,
    int ActiveShardCount,
    int DegradedShardCount,
    DateTime TimestampUtc
);

public record EngineHealthResponse(
    string Status,
    string Version,
    IReadOnlyDictionary<string, string> Metrics,
    DateTime TimestampUtc
);
