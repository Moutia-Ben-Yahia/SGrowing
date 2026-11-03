using System;
using FluentAssertions;
using SGrowing.Api.Modules.Security.MicroSegmentation;
using Xunit;

namespace SGrowing.Tests.Security;

public class MicroSegmentationTests
{
    private readonly MicroSegmentationEngine _engine = new();

    [Fact]
    public void GetTopology_ReturnsRegisteredServiceIdentities()
    {
        var topology = _engine.GetTopology();

        topology.Should().NotBeNull();
        topology.Services.Should().HaveCount(4);
        topology.PolicyRules.Should().HaveCount(3);
        topology.InterceptedRogueRequestsCount.Should().BeGreaterThan(0);
        topology.Services.Should().OnlyContain(s => s.IsMtlsActive && s.SpiffeId.StartsWith("spiffe://"));
    }

    [Fact]
    public void RotateCertificate_ExtendsExpirationDate()
    {
        var service = "api-gateway";
        var before = _engine.GetTopology().Services.Find(s => s.ServiceName == service)!;

        var request = new RotateCertRequest(service);
        var renewed = _engine.RotateCertificate(request);

        renewed.CertExpiresAtUtc.Should().BeAfter(before.CertExpiresAtUtc);
        renewed.IsMtlsActive.Should().BeTrue();
    }

    [Fact]
    public void EvaluateTraffic_AllowsAuthorizedPath()
    {
        var request = new EvaluateTrafficRequest("api-gateway", "billing-service", "/api/v1/billing/invoices");
        var audit = _engine.EvaluateTraffic(request);

        audit.WasAllowed.Should().BeTrue();
        audit.Reason.Should().Contain("Allowed by L7");
    }

    [Fact]
    public void EvaluateTraffic_BlocksUnauthorizedLateralTraversal()
    {
        var request = new EvaluateTrafficRequest("telemetry-agent", "vault-keyring", "/api/v1/security/ephemeral/keys");
        var beforeCount = _engine.GetTopology().InterceptedRogueRequestsCount;

        var audit = _engine.EvaluateTraffic(request);

        audit.WasAllowed.Should().BeFalse();
        audit.Reason.Should().Contain("Zero-Trust policy violation");
        _engine.GetTopology().InterceptedRogueRequestsCount.Should().Be(beforeCount + 1);
    }
}
