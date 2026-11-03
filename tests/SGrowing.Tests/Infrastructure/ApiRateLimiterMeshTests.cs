using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.ApiRateLimiterMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class ApiRateLimiterMeshTests
{
    private readonly ApiRateLimiterMeshEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActiveBucketsAndQuarantinedIps()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalActiveBucketsCount.Should().Be(3);
        overview.QuarantinedIpCount.Should().BeGreaterThanOrEqualTo(1);
        overview.CurrentTarpitDelayMs.Should().BeGreaterThan(0);
        overview.TotalRequestsEvaluated24h.Should().BeGreaterThan(1000000);
        overview.ActiveBuckets.Should().HaveCount(3);
        overview.QuarantinedIps.Should().NotBeEmpty();
    }

    [Fact]
    public void ConsumeToken_EnterpriseClientSucceedsWithAllowVerdict()
    {
        var request = new ConsumeTokenRequest(
            "CLI-ENT-ACME-01",
            "192.0.2.45",
            "/api/v1/enterprise/export",
            10
        );

        var verdict = _engine.ConsumeToken(request);

        verdict.Should().NotBeNull();
        verdict.Verdict.Should().Be("ALLOW");
        verdict.RemainingTokens.Should().Be(48490);
        verdict.TarpitDelayMs.Should().Be(0);
    }

    [Fact]
    public void ConsumeToken_QuarantinedIpGetsRejectedImmediately()
    {
        var request = new ConsumeTokenRequest(
            "CLI-ENT-ACME-01",
            "185.220.101.5", // Known quarantined IP
            "/api/v1/auth/login",
            1
        );

        var verdict = _engine.ConsumeToken(request);

        verdict.Should().NotBeNull();
        verdict.Verdict.Should().Be("REJECT_TOO_MANY_REQUESTS");
        verdict.RetryAfterSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void QuarantineIp_AddsToBlacklistWithMultiCloudSyncHash()
    {
        var request = new QuarantineIpRequest(
            "203.0.113.88",
            "Rapid scraping on pricing endpoints",
            0.94,
            60
        );

        var receipt = _engine.QuarantineIp(request);

        receipt.Should().NotBeNull();
        receipt.ReceiptId.Should().StartWith("QRC-");
        receipt.IpAddress.Should().Be("203.0.113.88");
        receipt.Status.Should().Be("QUARANTINED_ACTIVE");
        receipt.MultiCloudSyncHash.Should().StartWith("0x");
    }
}
