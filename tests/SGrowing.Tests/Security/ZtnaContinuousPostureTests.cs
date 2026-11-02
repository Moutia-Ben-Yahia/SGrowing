using FluentAssertions;
using SGrowing.Api.Modules.Security.ZtnaContinuousPosture;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class ZtnaContinuousPostureTests
{
    private readonly GeovelocityAnomalyDetectorService _geoDetector = new();
    private readonly ContinuousTrustScoringEngine _ztnaEngine;

    public ZtnaContinuousPostureTests()
    {
        _ztnaEngine = new ContinuousTrustScoringEngine(_geoDetector);
    }

    private static DevicePostureSignal CompliantDevice => new(
        DeviceId: "DEV-MACBOOK-PRO-M3",
        IsCompliantOsVersion: true,
        HasActiveTpmEnclave: true,
        IsJailbrokenOrRooted: false,
        EndpointProtectionActive: true
    );

    #region Continuous Trust Attestation Tests

    [Fact]
    public void EvaluateSessionAccess_CompliantPostureAndStationaryLocation_MaintainsHighTrust()
    {
        var telemetry = new SessionAccessTelemetry(
            SessionId: "SESS-CORP-USER-01",
            UserId: "USER-ALGIERS-ADMIN",
            TenantId: "TENANT-HEADQUARTERS",
            TimestampUtc: DateTime.UtcNow,
            Latitude: 36.7538,
            Longitude: 3.0588, // Algiers
            IpAddress: "197.200.15.42",
            RequestsPerMinute: 25,
            DevicePosture: CompliantDevice
        );

        var result = _ztnaEngine.EvaluateSessionAccess(telemetry);

        result.Should().NotBeNull();
        result.TrustLevel.Should().Be(ZtnaTrustLevel.HighTrust);
        result.IsSessionRevoked.Should().BeFalse();
        result.TrustScore.Should().BeGreaterThan(0.80);
        result.SecurityAnomaliesDetected.Should().BeEmpty();
        result.PolicyVerdict.Should().StartWith("PERMIT");
    }

    [Fact]
    public void EvaluateSessionAccess_ImpossibleGeovelocity_DegradesTrustAndDemandsStepUp()
    {
        var sessionId = "SESS-COMPROMISED-01";
        var t0 = DateTime.UtcNow;

        // Login 1 in Algiers
        var tel1 = new SessionAccessTelemetry(sessionId, "USER-ALICE", "TENANT-001", t0, 36.7538, 3.0588, "197.200.1.1", 10, CompliantDevice);
        var res1 = _ztnaEngine.EvaluateSessionAccess(tel1);
        res1.TrustLevel.Should().Be(ZtnaTrustLevel.HighTrust);

        // Login 2 in Tokyo (9,800 km away) just 15 minutes later (speed > 39,000 km/h >> 900 km/h)
        var tel2 = new SessionAccessTelemetry(sessionId, "USER-ALICE", "TENANT-001", t0.AddMinutes(15), 35.6762, 139.6503, "133.242.1.1", 10, CompliantDevice);
        var res2 = _ztnaEngine.EvaluateSessionAccess(tel2);

        res2.SecurityAnomaliesDetected.Should().ContainMatch("*IMPOSSIBLE TRAVEL*");
        res2.TrustScore.Should().BeLessThan(res1.TrustScore);
        res2.TrustLevel.Should().BeOneOf(ZtnaTrustLevel.ConditionalTrust, ZtnaTrustLevel.ZeroTrustRevoked);
    }

    [Fact]
    public void EvaluateSessionAccess_JailbrokenDeviceAndBotSpeed_RevokesSessionAndBlacklists()
    {
        var sessionId = "SESS-ATTACKER-BOT";
        var compromisedDevice = new DevicePostureSignal(
            DeviceId: "DEV-ROOTED-EMULATOR",
            IsCompliantOsVersion: false,
            HasActiveTpmEnclave: false,
            IsJailbrokenOrRooted: true, // Rooted!
            EndpointProtectionActive: false
        );

        var telemetry = new SessionAccessTelemetry(
            SessionId: sessionId,
            UserId: "USER-BOT",
            TenantId: "TENANT-001",
            TimestampUtc: DateTime.UtcNow,
            Latitude: 48.8566,
            Longitude: 2.3522,
            IpAddress: "185.220.101.5",
            RequestsPerMinute: 650, // Massive bot request spike
            DevicePosture: compromisedDevice
        );

        var result = _ztnaEngine.EvaluateSessionAccess(telemetry);

        result.TrustScore.Should().BeLessThan(0.40);
        result.TrustLevel.Should().Be(ZtnaTrustLevel.ZeroTrustRevoked);
        result.IsSessionRevoked.Should().BeTrue();
        result.PolicyVerdict.Should().StartWith("TERMINATE");

        _ztnaEngine.IsSessionRevoked(sessionId).Should().BeTrue();

        // Any subsequent call with this session ID is immediately rejected
        var nextAttempt = _ztnaEngine.EvaluateSessionAccess(telemetry with { RequestsPerMinute = 1 });
        nextAttempt.IsSessionRevoked.Should().BeTrue();
        nextAttempt.TrustScore.Should().Be(0.0);
        nextAttempt.PolicyVerdict.Should().StartWith("DENY");
    }

    #endregion
}
