using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.BgpGeoSteering;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class BgpGeoSteeringTests
{
    private readonly BgpGeoSteeringEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsPopsAndPolicies()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalPopsCount.Should().Be(4);
        overview.HealthyPopsCount.Should().Be(3);
        overview.AverageGlobalLatencyMs.Should().BeGreaterThan(0);
        overview.Pops.Should().HaveCount(4);
        overview.Policies.Should().HaveCount(3);
    }

    [Fact]
    public void IngestProbe_DetectsDegradedPopAndTriggersFailover()
    {
        var command = new IngestProbeCommand("POP-GRU-01", "LatinAmerica", 185, 12.4, 4.5);
        var result = _engine.IngestProbe(command);

        result.Should().NotBeNull();
        result.TargetPopId.Should().Be("POP-GRU-01");
        result.EvaluatedHealth.Should().Be("Degraded");
        result.TriggeredDnsFailover.Should().BeTrue();
        result.RemediationAction.Should().Contain("Shift DNS traffic");
    }

    [Fact]
    public void IngestProbe_MaintainsHealthyPopWithoutFailover()
    {
        var command = new IngestProbeCommand("POP-IAD-01", "NorthAmerica", 14, 1.2, 0.0);
        var result = _engine.IngestProbe(command);

        result.Should().NotBeNull();
        result.EvaluatedHealth.Should().Be("Healthy");
        result.TriggeredDnsFailover.Should().BeFalse();
    }

    [Fact]
    public void OverrideSteering_AppliesPolicyOverride()
    {
        var command = new OverrideSteeringCommand("POL-DNS-01", "POP-FRA-01", "Maintenance window Ashburn datacenter", "SRE-NETOPS-01");
        var result = _engine.OverrideSteering(command);

        result.Should().NotBeNull();
        result.PolicyId.Should().Be("POL-DNS-01");
        result.PreviousPopId.Should().Be("POP-IAD-01");
        result.ActivePopId.Should().Be("POP-FRA-01");
        result.AuditReceipt.Should().Contain("AUDIT-BGP-DNS");
    }
}
