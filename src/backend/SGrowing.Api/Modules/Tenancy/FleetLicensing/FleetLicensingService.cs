using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace SGrowing.Api.Modules.Tenancy.FleetLicensing;

public interface IFleetLicensingService
{
    IssuedCryptographicLicense IssueLicense(IssueLicenseRequest request);
    LicenseVerificationResult VerifyLicense(string signedToken, string? currentNodeFingerprint = null);
    string GenerateAirGappedActivationCode(IssuedCryptographicLicense license);
    AirGapActivationResponse ActivateAirGappedCode(AirGapActivationRequest request);
    ClusterFleetStatus IngestNodeHeartbeat(Guid tenantId, ClusterNodeHeartbeat heartbeat);
    ClusterFleetStatus EvaluateFleetStatus(Guid tenantId);
    IReadOnlyList<ClusterNodeHeartbeat> GetActiveNodes(Guid tenantId);
    LicensePayload? GetCurrentActiveLicense(Guid tenantId);
    LicenseVerificationResult SetActiveLicense(Guid tenantId, string signedToken);
    void ResetFleetState(Guid tenantId);
}

public class FleetLicensingService : IFleetLicensingService
{
    public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string DefaultMasterSigningSecret = "SGrowing-Cryptographic-Licensing-MasterSecret-2026-AirGap-HMACSHA256";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly byte[] _signingKeyBytes;
    private readonly ConcurrentDictionary<Guid, LicensePayload> _activeLicenses = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ClusterNodeHeartbeat>> _nodeHeartbeats = new();

    public FleetLicensingService(IConfiguration? configuration = null)
    {
        var secret = configuration?["Licensing:HmacSecret"]
            ?? configuration?["Licensing:SigningKey"]
            ?? DefaultMasterSigningSecret;

        _signingKeyBytes = Encoding.UTF8.GetBytes(secret);

        SeedDefaultTenantFleet();
    }

    private void SeedDefaultTenantFleet()
    {
        var now = DateTimeOffset.UtcNow;
        var defaultPayload = new LicensePayload(
            TenantId: DefaultTenantId,
            OrganizationName: "Apex Sovereign Enterprise",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 250,
            MaxCpuCores: 64,
            MaxClusterNodes: 8,
            EnabledEnterpriseModules: new List<string>
            {
                "AI.SwarmCoordination",
                "Voice.WebRtcSdr",
                "Analytics.ClickHouseBI",
                "Marketplace.PrivateRegistry",
                "Security.AirGappedKeyValidator",
                "Governance.DataSovereigntyMesh"
            },
            IssuedAtUtc: now.AddDays(-15),
            ExpiresAtUtc: now.AddDays(350),
            HardwareClusterFingerprint: "*",
            Metadata: new Dictionary<string, string>
            {
                ["DeploymentMode"] = "Self-Hosted Air-Gap",
                ["ContractReference"] = "SGRW-ENT-2026-8841A"
            }
        );

        _activeLicenses[DefaultTenantId] = defaultPayload;

        var nodes = _nodeHeartbeats.GetOrAdd(DefaultTenantId, _ => new ConcurrentDictionary<string, ClusterNodeHeartbeat>());
        nodes["node-alpha-01"] = new ClusterNodeHeartbeat(
            NodeId: "node-alpha-01",
            NodeName: "Core-Mesh-Alpha",
            MachineFingerprint: "FP-SRV-9901-MAC-A1",
            AllocatedCpuCores: 16,
            ActiveSeats: 48,
            MemoryUsageGb: 32.4,
            CpuUtilizationPercent: 42.5,
            TimestampUtc: now,
            RunningServices: new[] { "ApiGateway", "PostgresRls", "VoiceMesh" }
        );

        nodes["node-beta-02"] = new ClusterNodeHeartbeat(
            NodeId: "node-beta-02",
            NodeName: "Worker-Fleet-Beta",
            MachineFingerprint: "FP-SRV-9902-MAC-B2",
            AllocatedCpuCores: 24,
            ActiveSeats: 82,
            MemoryUsageGb: 48.0,
            CpuUtilizationPercent: 58.2,
            TimestampUtc: now,
            RunningServices: new[] { "AiSwarmExecutor", "AnalyticsIngest" }
        );
    }

    public IssuedCryptographicLicense IssueLicense(IssueLicenseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(Math.Max(1, request.ValidityDays));
        var fingerprint = string.IsNullOrWhiteSpace(request.HardwareClusterFingerprint)
            ? "*"
            : request.HardwareClusterFingerprint.Trim();

        var payload = new LicensePayload(
            TenantId: request.TenantId,
            OrganizationName: request.OrganizationName.Trim(),
            Tier: string.IsNullOrWhiteSpace(request.Tier) ? "SelfHostedEnterprise" : request.Tier.Trim(),
            MaxSeats: Math.Max(1, request.MaxSeats),
            MaxCpuCores: Math.Max(1, request.MaxCpuCores),
            MaxClusterNodes: Math.Max(1, request.MaxClusterNodes),
            EnabledEnterpriseModules: request.EnabledModules?.Distinct().ToList() ?? new List<string>(),
            IssuedAtUtc: now,
            ExpiresAtUtc: expiresAt,
            HardwareClusterFingerprint: fingerprint,
            Metadata: request.Metadata
        );

        var canonical = ComputeCanonicalPayload(payload);
        var signatureBytes = ComputeSignatureBytes(canonical);
        var signatureBase64 = Base64UrlEncode(signatureBytes);

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        var signedToken = $"{payloadBase64}.{signatureBase64}";
        var licenseKey = FormatLicenseKey(payload.TenantId, signatureBytes);

        var issued = new IssuedCryptographicLicense(
            LicenseKey: licenseKey,
            Payload: payload,
            Signature: signatureBase64,
            SignedToken: signedToken,
            AirGappedActivationCode: string.Empty, // populated below
            GeneratedAtUtc: now
        );

        var airGapCode = GenerateAirGappedActivationCode(issued);
        return issued with { AirGappedActivationCode = airGapCode };
    }

    public LicenseVerificationResult VerifyLicense(string signedToken, string? currentNodeFingerprint = null)
    {
        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(signedToken))
        {
            return new LicenseVerificationResult(
                IsValid: false,
                Status: LicenseValidationStatus.MalformedToken,
                Message: "Signed license token is empty.",
                Payload: null,
                DaysRemaining: 0,
                IsAirGapCompliant: false,
                EvaluatedAtUtc: now
            );
        }

        var parts = signedToken.Trim().Split('.');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return new LicenseVerificationResult(
                IsValid: false,
                Status: LicenseValidationStatus.MalformedToken,
                Message: "Signed license token must contain exactly two Base64Url-encoded segments (payload.signature).",
                Payload: null,
                DaysRemaining: 0,
                IsAirGapCompliant: false,
                EvaluatedAtUtc: now
            );
        }

        LicensePayload? payload;
        try
        {
            var payloadBytes = Base64UrlDecode(parts[0]);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            payload = JsonSerializer.Deserialize<LicensePayload>(payloadJson, JsonOptions);
            if (payload == null)
            {
                return new LicenseVerificationResult(
                    IsValid: false,
                    Status: LicenseValidationStatus.MalformedToken,
                    Message: "Deserialized license payload is null.",
                    Payload: null,
                    DaysRemaining: 0,
                    IsAirGapCompliant: false,
                    EvaluatedAtUtc: now
                );
            }
        }
        catch (Exception ex)
        {
            return new LicenseVerificationResult(
                IsValid: false,
                Status: LicenseValidationStatus.MalformedToken,
                Message: $"Failed to decode license payload: {ex.Message}",
                Payload: null,
                DaysRemaining: 0,
                IsAirGapCompliant: false,
                EvaluatedAtUtc: now
            );
        }

        // Cryptographic HMAC-SHA256 signature verification
        byte[] actualSigBytes;
        try
        {
            actualSigBytes = Base64UrlDecode(parts[1]);
        }
        catch (Exception ex)
        {
            return new LicenseVerificationResult(
                IsValid: false,
                Status: LicenseValidationStatus.SignatureMismatch,
                Message: $"Failed to decode signature bytes: {ex.Message}",
                Payload: payload,
                DaysRemaining: 0,
                IsAirGapCompliant: false,
                EvaluatedAtUtc: now
            );
        }

        var canonical = ComputeCanonicalPayload(payload);
        var expectedSigBytes = ComputeSignatureBytes(canonical);

        if (!CryptographicOperations.FixedTimeEquals(expectedSigBytes, actualSigBytes))
        {
            return new LicenseVerificationResult(
                IsValid: false,
                Status: LicenseValidationStatus.TamperedPayload,
                Message: "Cryptographic signature mismatch. The license payload has been modified, tampered with, or forged (tamper detected).",
                Payload: payload,
                DaysRemaining: 0,
                IsAirGapCompliant: false,
                EvaluatedAtUtc: now
            );
        }

        // Validity window evaluation
        if (now > payload.ExpiresAtUtc)
        {
            return new LicenseVerificationResult(
                IsValid: false,
                Status: LicenseValidationStatus.Expired,
                Message: $"License expired at {payload.ExpiresAtUtc:u}. Zero-cloud grace period exceeded.",
                Payload: payload,
                DaysRemaining: 0,
                IsAirGapCompliant: false,
                EvaluatedAtUtc: now
            );
        }

        // Hardware cluster fingerprint matching
        if (!string.IsNullOrWhiteSpace(payload.HardwareClusterFingerprint) && payload.HardwareClusterFingerprint != "*")
        {
            if (string.IsNullOrWhiteSpace(currentNodeFingerprint))
            {
                return new LicenseVerificationResult(
                    IsValid: false,
                    Status: LicenseValidationStatus.HardwareMismatch,
                    Message: "License is bound to specific node hardware fingerprints, but no current node fingerprint was supplied.",
                    Payload: payload,
                    DaysRemaining: Math.Max(0, (int)(payload.ExpiresAtUtc - now).TotalDays),
                    IsAirGapCompliant: false,
                    EvaluatedAtUtc: now
                );
            }

            var allowed = payload.HardwareClusterFingerprint
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var matches = allowed.Any(a => string.Equals(a, currentNodeFingerprint.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!matches)
            {
                return new LicenseVerificationResult(
                    IsValid: false,
                    Status: LicenseValidationStatus.HardwareMismatch,
                    Message: $"Current machine fingerprint '{currentNodeFingerprint}' does not match entitled hardware cluster fingerprints [{payload.HardwareClusterFingerprint}].",
                    Payload: payload,
                    DaysRemaining: Math.Max(0, (int)(payload.ExpiresAtUtc - now).TotalDays),
                    IsAirGapCompliant: false,
                    EvaluatedAtUtc: now
                );
            }
        }

        var remainingDays = Math.Max(0, (int)(payload.ExpiresAtUtc - now).TotalDays);
        return new LicenseVerificationResult(
            IsValid: true,
            Status: LicenseValidationStatus.Valid,
            Message: "Cryptographic license verified successfully. Offline HMAC-SHA256 signature intact.",
            Payload: payload,
            DaysRemaining: remainingDays,
            IsAirGapCompliant: true,
            EvaluatedAtUtc: now
        );
    }

    public string GenerateAirGappedActivationCode(IssuedCryptographicLicense license)
    {
        ArgumentNullException.ThrowIfNull(license);

        var tokenBytes = Encoding.UTF8.GetBytes(license.SignedToken);
        var tokenEncoded = Base64UrlEncode(tokenBytes);

        var hash = SHA256.HashData(tokenBytes);
        var checksum = Convert.ToHexString(hash.AsSpan(0, 4));

        return $"SGA1-{tokenEncoded}-{checksum}";
    }

    public AirGapActivationResponse ActivateAirGappedCode(AirGapActivationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.AirGappedActivationCode))
        {
            return new AirGapActivationResponse(
                Success: false,
                Message: "Air-gapped activation code cannot be empty.",
                Payload: null,
                SignedToken: null
            );
        }

        var cleanCode = request.AirGappedActivationCode.Trim();
        if (!cleanCode.StartsWith("SGA1-", StringComparison.OrdinalIgnoreCase))
        {
            return new AirGapActivationResponse(
                Success: false,
                Message: "Invalid air-gapped activation code format. Must begin with 'SGA1-'.",
                Payload: null,
                SignedToken: null
            );
        }

        var lastHyphen = cleanCode.LastIndexOf('-');
        if (lastHyphen <= 5)
        {
            return new AirGapActivationResponse(
                Success: false,
                Message: "Invalid air-gapped activation code structure.",
                Payload: null,
                SignedToken: null
            );
        }

        var tokenEncoded = cleanCode.Substring(5, lastHyphen - 5);
        var providedChecksum = cleanCode.Substring(lastHyphen + 1);

        byte[] tokenBytes;
        try
        {
            tokenBytes = Base64UrlDecode(tokenEncoded);
        }
        catch
        {
            return new AirGapActivationResponse(
                Success: false,
                Message: "Malformed activation code token payload.",
                Payload: null,
                SignedToken: null
            );
        }

        var hash = SHA256.HashData(tokenBytes);
        var expectedChecksum = Convert.ToHexString(hash.AsSpan(0, 4));
        if (!string.Equals(providedChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
        {
            return new AirGapActivationResponse(
                Success: false,
                Message: "Activation code checksum verification failed. The activation code is corrupted or truncated.",
                Payload: null,
                SignedToken: null
            );
        }

        var signedToken = Encoding.UTF8.GetString(tokenBytes);
        var verification = VerifyLicense(signedToken, request.MachineFingerprint);
        if (!verification.IsValid || verification.Payload == null)
        {
            return new AirGapActivationResponse(
                Success: false,
                Message: $"Air-gapped activation rejected: {verification.Message}",
                Payload: verification.Payload,
                SignedToken: null
            );
        }

        _activeLicenses[verification.Payload.TenantId] = verification.Payload;

        return new AirGapActivationResponse(
            Success: true,
            Message: "Air-gapped enterprise license successfully activated and bound to cluster hardware.",
            Payload: verification.Payload,
            SignedToken: signedToken
        );
    }

    public ClusterFleetStatus IngestNodeHeartbeat(Guid tenantId, ClusterNodeHeartbeat heartbeat)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);

        if (string.IsNullOrWhiteSpace(heartbeat.NodeId))
        {
            throw new ArgumentException("NodeId must be specified in node heartbeat.", nameof(heartbeat));
        }

        var nodesForTenant = _nodeHeartbeats.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, ClusterNodeHeartbeat>());
        nodesForTenant[heartbeat.NodeId] = heartbeat;

        return EvaluateFleetStatus(tenantId);
    }

    public ClusterFleetStatus EvaluateFleetStatus(Guid tenantId)
    {
        var now = DateTimeOffset.UtcNow;
        var nodesForTenant = _nodeHeartbeats.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, ClusterNodeHeartbeat>());
        var heartbeats = nodesForTenant.Values.ToList();

        var activeLicense = GetCurrentActiveLicense(tenantId);
        var maxSeats = activeLicense?.MaxSeats ?? 50;
        var maxCores = activeLicense?.MaxCpuCores ?? 16;
        var maxNodes = activeLicense?.MaxClusterNodes ?? 3;
        var clusterName = activeLicense?.OrganizationName ?? "Self-Hosted Enterprise Cluster";

        var summaries = new List<NodeHealthSummary>();
        var alerts = new List<string>();
        var staleThreshold = TimeSpan.FromSeconds(90);

        foreach (var node in heartbeats)
        {
            var age = now - node.TimestampUtc;
            var isStale = age > staleThreshold;

            ClusterNodeStatus nodeStatus;
            string statusDetail;

            if (isStale)
            {
                nodeStatus = ClusterNodeStatus.Offline;
                statusDetail = $"Offline: No heartbeat received for {(int)age.TotalSeconds} seconds.";
                alerts.Add($"Node '{node.NodeName}' ({node.NodeId}) has timed out and is unreachable.");
            }
            else if (node.CpuUtilizationPercent >= 90.0)
            {
                nodeStatus = ClusterNodeStatus.Degraded;
                statusDetail = $"Degraded: High CPU utilization at {node.CpuUtilizationPercent:F1}%.";
                alerts.Add($"Node '{node.NodeName}' ({node.NodeId}) is experiencing high load ({node.CpuUtilizationPercent:F1}% CPU).");
            }
            else
            {
                nodeStatus = ClusterNodeStatus.Healthy;
                statusDetail = "Healthy: Heartbeat responsive and hardware telemetry verified.";
            }

            summaries.Add(new NodeHealthSummary(
                NodeId: node.NodeId,
                NodeName: node.NodeName,
                MachineFingerprint: node.MachineFingerprint,
                Status: nodeStatus,
                AllocatedCpuCores: node.AllocatedCpuCores,
                ActiveSeats: node.ActiveSeats,
                CpuUtilizationPercent: node.CpuUtilizationPercent,
                LastHeartbeatAtUtc: node.TimestampUtc,
                IsStale: isStale,
                StatusDetails: statusDetail
            ));
        }

        var activeReportingSummaries = summaries.Where(s => !s.IsStale).ToList();
        var totalAllocatedCpuCores = activeReportingSummaries.Sum(s => s.AllocatedCpuCores);
        var totalActiveSeats = activeReportingSummaries.Sum(s => s.ActiveSeats);
        var activeNodesCount = activeReportingSummaries.Count;
        var totalNodesCount = summaries.Count;

        var seatQuotaExceeded = totalActiveSeats > maxSeats;
        var cpuQuotaExceeded = totalAllocatedCpuCores > maxCores;
        var nodeQuotaExceeded = activeNodesCount > maxNodes;

        if (seatQuotaExceeded)
        {
            alerts.Add($"Seat quota exceeded: {totalActiveSeats}/{maxSeats} seats active across nodes.");
        }
        if (cpuQuotaExceeded)
        {
            alerts.Add($"CPU core quota exceeded: {totalAllocatedCpuCores}/{maxCores} cores allocated.");
        }
        if (nodeQuotaExceeded)
        {
            alerts.Add($"Cluster node count exceeded: {activeNodesCount}/{maxNodes} active nodes online.");
        }

        ClusterHealthState overallStatus;
        if (seatQuotaExceeded || cpuQuotaExceeded || nodeQuotaExceeded)
        {
            overallStatus = ClusterHealthState.QuotaExceeded;
        }
        else if (summaries.Any(s => s.Status == ClusterNodeStatus.Degraded || s.Status == ClusterNodeStatus.Offline))
        {
            overallStatus = ClusterHealthState.Degraded;
        }
        else
        {
            overallStatus = ClusterHealthState.Healthy;
        }

        return new ClusterFleetStatus(
            TenantId: tenantId,
            ClusterName: clusterName,
            OverallStatus: overallStatus,
            TotalNodes: totalNodesCount,
            ActiveNodes: activeNodesCount,
            TotalAllocatedCpuCores: totalAllocatedCpuCores,
            MaxAllowedCpuCores: maxCores,
            TotalActiveSeats: totalActiveSeats,
            MaxAllowedSeats: maxSeats,
            MaxAllowedNodes: maxNodes,
            SeatQuotaExceeded: seatQuotaExceeded,
            CpuQuotaExceeded: cpuQuotaExceeded,
            NodeQuotaExceeded: nodeQuotaExceeded,
            Nodes: summaries,
            ActiveAlerts: alerts,
            LastEvaluatedAtUtc: now
        );
    }

    public IReadOnlyList<ClusterNodeHeartbeat> GetActiveNodes(Guid tenantId)
    {
        if (_nodeHeartbeats.TryGetValue(tenantId, out var dict))
        {
            return dict.Values.ToList();
        }

        return Array.Empty<ClusterNodeHeartbeat>();
    }

    public LicensePayload? GetCurrentActiveLicense(Guid tenantId)
    {
        return _activeLicenses.TryGetValue(tenantId, out var payload) ? payload : null;
    }

    public LicenseVerificationResult SetActiveLicense(Guid tenantId, string signedToken)
    {
        var result = VerifyLicense(signedToken);
        if (result.IsValid && result.Payload != null)
        {
            _activeLicenses[tenantId] = result.Payload;
        }

        return result;
    }

    public void ResetFleetState(Guid tenantId)
    {
        _nodeHeartbeats.TryRemove(tenantId, out _);
        _activeLicenses.TryRemove(tenantId, out _);
    }

    public static string ComputeCanonicalPayload(LicensePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var sortedModules = payload.EnabledEnterpriseModules != null
            ? string.Join(",", payload.EnabledEnterpriseModules.OrderBy(m => m, StringComparer.Ordinal))
            : string.Empty;

        var hw = string.IsNullOrWhiteSpace(payload.HardwareClusterFingerprint)
            ? "*"
            : payload.HardwareClusterFingerprint.Trim().ToUpperInvariant();

        return $"{payload.TenantId:D}|{payload.OrganizationName}|{payload.Tier}|{payload.MaxSeats}|{payload.MaxCpuCores}|{payload.MaxClusterNodes}|{sortedModules}|{payload.IssuedAtUtc.ToUnixTimeSeconds()}|{payload.ExpiresAtUtc.ToUnixTimeSeconds()}|{hw}";
    }

    private byte[] ComputeSignatureBytes(string canonicalString)
    {
        using var hmac = new HMACSHA256(_signingKeyBytes);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalString));
    }

    private static string FormatLicenseKey(Guid tenantId, byte[] signatureBytes)
    {
        var hashHex = Convert.ToHexString(signatureBytes.AsSpan(0, 8));
        return $"SG-ENT-{hashHex[..4]}-{hashHex[4..8]}-{tenantId.ToString("N")[..4].ToUpperInvariant()}";
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }

        return Convert.FromBase64String(output);
    }
}
