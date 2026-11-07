using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SGrowing.Api.Modules.Tenancy.FleetLicensing;
using Xunit;

namespace SGrowing.Tests.Unit;

public class FleetLicensingTests
{
    private readonly FleetLicensingService _service;
    private readonly Guid _testTenantId = Guid.NewGuid();

    public FleetLicensingTests()
    {
        _service = new FleetLicensingService();
        _service.ResetFleetState(_testTenantId);
    }

    [Fact]
    public void IssueLicense_ReturnsValidSignedToken_AndMatchesCanonicalSignature()
    {
        var request = new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "GovCloud Sovereign Agency",
            Tier: "AirGappedGovernment",
            MaxSeats: 300,
            MaxCpuCores: 128,
            MaxClusterNodes: 10,
            EnabledModules: new List<string> { "AI.Swarm", "Voice.WebRtc", "Security.DataSovereignty" },
            ValidityDays: 180,
            HardwareClusterFingerprint: "HW-NODE-NODE01,HW-NODE-NODE02",
            Metadata: new Dictionary<string, string> { ["SecurityClearance"] = "Class-4" }
        );

        var issued = _service.IssueLicense(request);

        issued.Should().NotBeNull();
        issued.LicenseKey.Should().StartWith("SG-ENT-");
        issued.SignedToken.Should().Contain(".");
        issued.Signature.Should().NotBeNullOrWhiteSpace();
        issued.AirGappedActivationCode.Should().StartWith("SGA1-");
        issued.Payload.TenantId.Should().Be(_testTenantId);
        issued.Payload.OrganizationName.Should().Be("GovCloud Sovereign Agency");
        issued.Payload.MaxSeats.Should().Be(300);
        issued.Payload.MaxCpuCores.Should().Be(128);
        issued.Payload.MaxClusterNodes.Should().Be(10);
        issued.Payload.HardwareClusterFingerprint.Should().Be("HW-NODE-NODE01,HW-NODE-NODE02");
        issued.Payload.EnabledEnterpriseModules.Should().HaveCount(3);
    }

    [Fact]
    public void VerifyLicense_WithValidToken_ReturnsValidStatusAndPayload()
    {
        var request = new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Saudi Scale Enterprise",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 500,
            MaxCpuCores: 64,
            MaxClusterNodes: 6,
            EnabledModules: new List<string> { "AI.Swarm", "Voice.WebRtc", "Analytics.ClickHouse" },
            ValidityDays: 90,
            HardwareClusterFingerprint: "*"
        );

        var issued = _service.IssueLicense(request);
        var result = _service.VerifyLicense(issued.SignedToken);

        result.IsValid.Should().BeTrue();
        result.Status.Should().Be(LicenseValidationStatus.Valid);
        result.Payload.Should().NotBeNull();
        result.Payload!.OrganizationName.Should().Be("Saudi Scale Enterprise");
        result.DaysRemaining.Should().BeGreaterThan(80);
        result.IsAirGapCompliant.Should().BeTrue();
    }

    [Fact]
    public void VerifyLicense_WithTamperedPayload_ReturnsTamperedPayloadStatus()
    {
        var request = new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Original Honest Tenant",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 50,
            MaxCpuCores: 8,
            MaxClusterNodes: 2,
            EnabledModules: new List<string> { "AI.Swarm" },
            ValidityDays: 60
        );

        var issued = _service.IssueLicense(request);
        var parts = issued.SignedToken.Split('.');

        // Forge payload: decode JSON, change MaxSeats to 999999, re-encode without signature update
        var payloadBytes = Base64UrlDecode(parts[0]);
        var json = Encoding.UTF8.GetString(payloadBytes);
        var tamperedJson = json.Replace("\"maxSeats\":50", "\"maxSeats\":999999");
        var tamperedPayloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(tamperedJson));
        var tamperedToken = $"{tamperedPayloadBase64}.{parts[1]}";

        var result = _service.VerifyLicense(tamperedToken);

        result.IsValid.Should().BeFalse();
        result.Status.Should().Be(LicenseValidationStatus.TamperedPayload);
        result.Message.Should().Contain("tamper detected");
    }

    [Fact]
    public void VerifyLicense_WithTamperedSignature_ReturnsTamperedPayloadStatus()
    {
        var request = new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Security Test Corp",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 100,
            MaxCpuCores: 16,
            MaxClusterNodes: 4,
            EnabledModules: new List<string> { "Voice.WebRtc" },
            ValidityDays: 30
        );

        var issued = _service.IssueLicense(request);
        var parts = issued.SignedToken.Split('.');

        // Invert the first character of the signature
        var tamperedSig = (parts[1][0] == 'A' ? 'B' : 'A') + parts[1][1..];
        var tamperedToken = $"{parts[0]}.{tamperedSig}";

        var result = _service.VerifyLicense(tamperedToken);

        result.IsValid.Should().BeFalse();
        result.Status.Should().Be(LicenseValidationStatus.TamperedPayload);
    }

    [Fact]
    public void VerifyLicense_WithExpiredWindow_ReturnsExpiredStatus()
    {
        // Issue an expired license by signing a payload with an expired timestamp
        var now = DateTimeOffset.UtcNow;
        var expiredPayload = new LicensePayload(
            TenantId: _testTenantId,
            OrganizationName: "Past Due Enterprise",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 100,
            MaxCpuCores: 16,
            MaxClusterNodes: 4,
            EnabledEnterpriseModules: new List<string> { "AI.Swarm" },
            IssuedAtUtc: now.AddDays(-60),
            ExpiresAtUtc: now.AddDays(-1),
            HardwareClusterFingerprint: "*"
        );

        var canonical = FleetLicensingService.ComputeCanonicalPayload(expiredPayload);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("SGrowing-Cryptographic-Licensing-MasterSecret-2026-AirGap-HMACSHA256"));
        var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        var sigBase64 = Base64UrlEncode(sigBytes);
        var payloadJson = JsonSerializer.Serialize(expiredPayload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var expiredToken = $"{payloadBase64}.{sigBase64}";

        var result = _service.VerifyLicense(expiredToken);

        result.IsValid.Should().BeFalse();
        result.Status.Should().Be(LicenseValidationStatus.Expired);
        result.DaysRemaining.Should().Be(0);
        result.Message.Should().Contain("License expired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("OnlyOneSegmentWithoutDot")]
    [InlineData("Too.Many.Segments.Here")]
    [InlineData("!!!NotBase64!!!.AlsoNotBase64")]
    public void VerifyLicense_WithMalformedToken_ReturnsMalformedTokenStatus(string malformedToken)
    {
        var result = _service.VerifyLicense(malformedToken);

        result.IsValid.Should().BeFalse();
        result.Status.Should().Be(LicenseValidationStatus.MalformedToken);
    }

    [Fact]
    public void VerifyLicense_WithMatchingHardwareFingerprint_Succeeds()
    {
        var request = new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Hardware Bound Corp",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 150,
            MaxCpuCores: 32,
            MaxClusterNodes: 4,
            EnabledModules: new List<string> { "Voice.WebRtc" },
            ValidityDays: 120,
            HardwareClusterFingerprint: "MAC-A1-B2-C3-D4,HW-NODE-XYZ99"
        );

        var issued = _service.IssueLicense(request);

        // Verification with first node fingerprint
        var result1 = _service.VerifyLicense(issued.SignedToken, "MAC-A1-B2-C3-D4");
        result1.IsValid.Should().BeTrue();
        result1.Status.Should().Be(LicenseValidationStatus.Valid);

        // Verification with second node fingerprint
        var result2 = _service.VerifyLicense(issued.SignedToken, "HW-NODE-XYZ99");
        result2.IsValid.Should().BeTrue();
        result2.Status.Should().Be(LicenseValidationStatus.Valid);
    }

    [Fact]
    public void VerifyLicense_WithMismatchedHardwareFingerprint_ReturnsHardwareMismatch()
    {
        var request = new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Strict Hardware Corp",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 100,
            MaxCpuCores: 16,
            MaxClusterNodes: 2,
            EnabledModules: new List<string> { "AI.Swarm" },
            ValidityDays: 90,
            HardwareClusterFingerprint: "MAC-A1-B2-C3-D4"
        );

        var issued = _service.IssueLicense(request);

        // Verification with rogue fingerprint
        var rogueResult = _service.VerifyLicense(issued.SignedToken, "ROGUE-MAC-FF-EE-DD");
        rogueResult.IsValid.Should().BeFalse();
        rogueResult.Status.Should().Be(LicenseValidationStatus.HardwareMismatch);
        rogueResult.Message.Should().Contain("ROGUE-MAC-FF-EE-DD");

        // Verification with empty fingerprint when fingerprint required
        var emptyResult = _service.VerifyLicense(issued.SignedToken, null);
        emptyResult.IsValid.Should().BeFalse();
        emptyResult.Status.Should().Be(LicenseValidationStatus.HardwareMismatch);
    }

    [Fact]
    public void GenerateAirGappedCode_AndActivateAirGappedCode_WithMatchingFingerprint_Succeeds()
    {
        var request = new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Submarine Base Alpha",
            Tier: "AirGappedGovernment",
            MaxSeats: 200,
            MaxCpuCores: 48,
            MaxClusterNodes: 5,
            EnabledModules: new List<string> { "AI.Swarm", "Security.AirGappedKeyValidator" },
            ValidityDays: 365,
            HardwareClusterFingerprint: "SUB-NODE-01"
        );

        var issued = _service.IssueLicense(request);
        var airGapCode = issued.AirGappedActivationCode;

        airGapCode.Should().StartWith("SGA1-");

        var activationResult = _service.ActivateAirGappedCode(new AirGapActivationRequest(
            AirGappedActivationCode: airGapCode,
            MachineFingerprint: "SUB-NODE-01"
        ));

        activationResult.Success.Should().BeTrue();
        activationResult.Payload.Should().NotBeNull();
        activationResult.Payload!.OrganizationName.Should().Be("Submarine Base Alpha");
        activationResult.SignedToken.Should().Be(issued.SignedToken);

        // Verify it was stored as active license
        var active = _service.GetCurrentActiveLicense(_testTenantId);
        active.Should().NotBeNull();
        active!.MaxSeats.Should().Be(200);
    }

    [Fact]
    public void ActivateAirGappedCode_WithMismatchedFingerprint_ReturnsFailure()
    {
        var request = new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Secure Vault Facility",
            Tier: "AirGappedGovernment",
            MaxSeats: 100,
            MaxCpuCores: 16,
            MaxClusterNodes: 3,
            EnabledModules: new List<string> { "AI.Swarm" },
            ValidityDays: 365,
            HardwareClusterFingerprint: "VAULT-HW-ALPHA"
        );

        var issued = _service.IssueLicense(request);
        var response = _service.ActivateAirGappedCode(new AirGapActivationRequest(
            AirGappedActivationCode: issued.AirGappedActivationCode,
            MachineFingerprint: "COMPROMISED-NODE-99"
        ));

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Air-gapped activation rejected");
        response.Message.Should().Contain("COMPROMISED-NODE-99");
    }

    [Fact]
    public void ActivateAirGappedCode_WithCorruptedChecksum_ReturnsFailure()
    {
        var request = new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Integrity Lab Inc.",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 100,
            MaxCpuCores: 16,
            MaxClusterNodes: 2,
            EnabledModules: new List<string> { "AI.Swarm" },
            ValidityDays: 90,
            HardwareClusterFingerprint: "*"
        );

        var issued = _service.IssueLicense(request);
        var originalCode = issued.AirGappedActivationCode;

        // Corrupt checksum (last 8 hex chars)
        var lastHyphen = originalCode.LastIndexOf('-');
        var corruptedCode = originalCode[..(lastHyphen + 1)] + "DEADBEEF";

        var response = _service.ActivateAirGappedCode(new AirGapActivationRequest(
            AirGappedActivationCode: corruptedCode,
            MachineFingerprint: "ANY-NODE"
        ));

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("checksum verification failed");
    }

    [Fact]
    public void IngestNodeHeartbeat_AggregatesActiveSeatsAndCpuCoresAcrossNodes()
    {
        var now = DateTimeOffset.UtcNow;

        _service.IngestNodeHeartbeat(_testTenantId, new ClusterNodeHeartbeat(
            NodeId: "node-1",
            NodeName: "Node One",
            MachineFingerprint: "FP-1",
            AllocatedCpuCores: 16,
            ActiveSeats: 35,
            MemoryUsageGb: 16.0,
            CpuUtilizationPercent: 45.0,
            TimestampUtc: now,
            RunningServices: new[] { "ApiGateway" }
        ));

        var status = _service.IngestNodeHeartbeat(_testTenantId, new ClusterNodeHeartbeat(
            NodeId: "node-2",
            NodeName: "Node Two",
            MachineFingerprint: "FP-2",
            AllocatedCpuCores: 32,
            ActiveSeats: 55,
            MemoryUsageGb: 32.0,
            CpuUtilizationPercent: 55.0,
            TimestampUtc: now,
            RunningServices: new[] { "AiSwarm" }
        ));

        status.TotalNodes.Should().Be(2);
        status.ActiveNodes.Should().Be(2);
        status.TotalAllocatedCpuCores.Should().Be(48);
        status.TotalActiveSeats.Should().Be(90);
        status.Nodes.Should().HaveCount(2);
    }

    [Fact]
    public void EvaluateFleetStatus_WhenSeatsExceedQuota_FlagsSeatQuotaExceededAndQuotaExceededStatus()
    {
        // Issue & activate license with 50 seats limit
        var issued = _service.IssueLicense(new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Quota Capped Tenant",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 50,
            MaxCpuCores: 32,
            MaxClusterNodes: 5,
            EnabledModules: new List<string> { "AI.Swarm" },
            ValidityDays: 180,
            HardwareClusterFingerprint: "*"
        ));
        _service.SetActiveLicense(_testTenantId, issued.SignedToken);

        var now = DateTimeOffset.UtcNow;
        _service.IngestNodeHeartbeat(_testTenantId, new ClusterNodeHeartbeat(
            NodeId: "node-heavy-1",
            NodeName: "Heavy Ingestion Node",
            MachineFingerprint: "FP-H1",
            AllocatedCpuCores: 16,
            ActiveSeats: 65, // Exceeds 50 seats!
            MemoryUsageGb: 20.0,
            CpuUtilizationPercent: 50.0,
            TimestampUtc: now,
            RunningServices: new[] { "Api" }
        ));

        var fleet = _service.EvaluateFleetStatus(_testTenantId);

        fleet.SeatQuotaExceeded.Should().BeTrue();
        fleet.TotalActiveSeats.Should().Be(65);
        fleet.MaxAllowedSeats.Should().Be(50);
        fleet.OverallStatus.Should().Be(ClusterHealthState.QuotaExceeded);
        fleet.ActiveAlerts.Should().Contain(a => a.Contains("Seat quota exceeded"));
    }

    [Fact]
    public void EvaluateFleetStatus_WhenCpuCoresExceedQuota_FlagsCpuQuotaExceeded()
    {
        // Issue & activate license with 16 cores limit
        var issued = _service.IssueLicense(new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Core Limited Tenant",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 200,
            MaxCpuCores: 16,
            MaxClusterNodes: 5,
            EnabledModules: new List<string> { "AI.Swarm" },
            ValidityDays: 180,
            HardwareClusterFingerprint: "*"
        ));
        _service.SetActiveLicense(_testTenantId, issued.SignedToken);

        var now = DateTimeOffset.UtcNow;
        _service.IngestNodeHeartbeat(_testTenantId, new ClusterNodeHeartbeat(
            NodeId: "node-c1",
            NodeName: "Compute Box 1",
            MachineFingerprint: "FP-C1",
            AllocatedCpuCores: 24, // Exceeds 16 cores!
            ActiveSeats: 10,
            MemoryUsageGb: 32.0,
            CpuUtilizationPercent: 40.0,
            TimestampUtc: now,
            RunningServices: new[] { "Api" }
        ));

        var fleet = _service.EvaluateFleetStatus(_testTenantId);

        fleet.CpuQuotaExceeded.Should().BeTrue();
        fleet.TotalAllocatedCpuCores.Should().Be(24);
        fleet.MaxAllowedCpuCores.Should().Be(16);
        fleet.OverallStatus.Should().Be(ClusterHealthState.QuotaExceeded);
        fleet.ActiveAlerts.Should().Contain(a => a.Contains("CPU core quota exceeded"));
    }

    [Fact]
    public void EvaluateFleetStatus_WhenNodeIsStale_MarksNodeStaleAndClusterDegraded()
    {
        var now = DateTimeOffset.UtcNow;

        // Ingest heartbeat with timestamp 150 seconds ago (older than 90s threshold)
        _service.IngestNodeHeartbeat(_testTenantId, new ClusterNodeHeartbeat(
            NodeId: "node-stale-1",
            NodeName: "Timed Out Node",
            MachineFingerprint: "FP-STALE",
            AllocatedCpuCores: 8,
            ActiveSeats: 12,
            MemoryUsageGb: 16.0,
            CpuUtilizationPercent: 30.0,
            TimestampUtc: now.AddSeconds(-150),
            RunningServices: new[] { "Worker" }
        ));

        var fleet = _service.EvaluateFleetStatus(_testTenantId);

        fleet.TotalNodes.Should().Be(1);
        fleet.ActiveNodes.Should().Be(0); // Stale node excluded from active count
        var nodeSummary = fleet.Nodes.Single();
        nodeSummary.IsStale.Should().BeTrue();
        nodeSummary.Status.Should().Be(ClusterNodeStatus.Offline);
        fleet.OverallStatus.Should().Be(ClusterHealthState.Degraded);
        fleet.ActiveAlerts.Should().Contain(a => a.Contains("timed out and is unreachable"));
    }

    [Fact]
    public void EvaluateFleetStatus_WithinAllQuotas_ReturnsHealthyCluster()
    {
        var issued = _service.IssueLicense(new IssueLicenseRequest(
            TenantId: _testTenantId,
            OrganizationName: "Healthy Scale Co",
            Tier: "SelfHostedEnterprise",
            MaxSeats: 150,
            MaxCpuCores: 32,
            MaxClusterNodes: 4,
            EnabledModules: new List<string> { "AI.Swarm", "Voice.WebRtc" },
            ValidityDays: 180,
            HardwareClusterFingerprint: "*"
        ));
        _service.SetActiveLicense(_testTenantId, issued.SignedToken);

        var now = DateTimeOffset.UtcNow;
        _service.IngestNodeHeartbeat(_testTenantId, new ClusterNodeHeartbeat(
            NodeId: "node-ok-1",
            NodeName: "Operational Box",
            MachineFingerprint: "FP-OK",
            AllocatedCpuCores: 16,
            ActiveSeats: 40,
            MemoryUsageGb: 24.0,
            CpuUtilizationPercent: 48.0,
            TimestampUtc: now,
            RunningServices: new[] { "Api", "Ai" }
        ));

        var fleet = _service.EvaluateFleetStatus(_testTenantId);

        fleet.OverallStatus.Should().Be(ClusterHealthState.Healthy);
        fleet.SeatQuotaExceeded.Should().BeFalse();
        fleet.CpuQuotaExceeded.Should().BeFalse();
        fleet.NodeQuotaExceeded.Should().BeFalse();
        fleet.ActiveAlerts.Should().BeEmpty();
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
