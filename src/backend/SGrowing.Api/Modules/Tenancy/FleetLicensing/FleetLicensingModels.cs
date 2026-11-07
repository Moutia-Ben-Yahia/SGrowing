using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Tenancy.FleetLicensing;

public enum LicenseValidationStatus
{
    Valid,
    Expired,
    SignatureMismatch,
    HardwareMismatch,
    QuotaExceeded,
    TamperedPayload,
    MalformedToken
}

public enum ClusterNodeStatus
{
    Healthy,
    Degraded,
    QuotaExceeded,
    Offline
}

public enum ClusterHealthState
{
    Healthy,
    Degraded,
    QuotaExceeded,
    Critical
}

public record LicensePayload(
    Guid TenantId,
    string OrganizationName,
    string Tier,
    int MaxSeats,
    int MaxCpuCores,
    int MaxClusterNodes,
    IReadOnlyList<string> EnabledEnterpriseModules,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string HardwareClusterFingerprint,
    IReadOnlyDictionary<string, string>? Metadata = null
);

public record IssuedCryptographicLicense(
    string LicenseKey,
    LicensePayload Payload,
    string Signature,
    string SignedToken,
    string AirGappedActivationCode,
    DateTimeOffset GeneratedAtUtc
);

public record LicenseVerificationResult(
    bool IsValid,
    LicenseValidationStatus Status,
    string Message,
    LicensePayload? Payload,
    int DaysRemaining,
    bool IsAirGapCompliant,
    DateTimeOffset EvaluatedAtUtc
);

public record ClusterNodeHeartbeat(
    string NodeId,
    string NodeName,
    string MachineFingerprint,
    int AllocatedCpuCores,
    int ActiveSeats,
    double MemoryUsageGb,
    double CpuUtilizationPercent,
    DateTimeOffset TimestampUtc,
    IReadOnlyList<string> RunningServices,
    IReadOnlyDictionary<string, string>? NodeMetadata = null
);

public record NodeHealthSummary(
    string NodeId,
    string NodeName,
    string MachineFingerprint,
    ClusterNodeStatus Status,
    int AllocatedCpuCores,
    int ActiveSeats,
    double CpuUtilizationPercent,
    DateTimeOffset LastHeartbeatAtUtc,
    bool IsStale,
    string StatusDetails
);

public record ClusterFleetStatus(
    Guid TenantId,
    string ClusterName,
    ClusterHealthState OverallStatus,
    int TotalNodes,
    int ActiveNodes,
    int TotalAllocatedCpuCores,
    int MaxAllowedCpuCores,
    int TotalActiveSeats,
    int MaxAllowedSeats,
    int MaxAllowedNodes,
    bool SeatQuotaExceeded,
    bool CpuQuotaExceeded,
    bool NodeQuotaExceeded,
    IReadOnlyList<NodeHealthSummary> Nodes,
    IReadOnlyList<string> ActiveAlerts,
    DateTimeOffset LastEvaluatedAtUtc
);

public record IssueLicenseRequest(
    Guid TenantId,
    string OrganizationName,
    string Tier,
    int MaxSeats,
    int MaxCpuCores,
    int MaxClusterNodes,
    List<string> EnabledModules,
    int ValidityDays,
    string? HardwareClusterFingerprint = null,
    Dictionary<string, string>? Metadata = null
);

public record VerifyLicenseRequest(
    string SignedToken,
    string? CurrentNodeFingerprint = null
);

public record AirGapActivationRequest(
    string AirGappedActivationCode,
    string MachineFingerprint
);

public record AirGapActivationResponse(
    bool Success,
    string Message,
    LicensePayload? Payload,
    string? SignedToken
);

public record IngestHeartbeatRequest(
    Guid TenantId,
    ClusterNodeHeartbeat Heartbeat
);

public record ActivateLicensePayloadRequest(
    Guid TenantId,
    string SignedToken
);
