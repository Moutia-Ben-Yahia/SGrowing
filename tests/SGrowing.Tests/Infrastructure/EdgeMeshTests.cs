using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Infrastructure.EdgeMesh;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class EdgeMeshTests
{
    private readonly EdgeMeshEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsGlobalEdgePoPsAndFunctions()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.TotalPoPsCount.Should().Be(6);
        overview.HealthyPoPsCount.Should().Be(6);
        overview.GlobalP95LatencyMs.Should().BeLessThan(20.0);
        overview.Nodes.Should().HaveCount(6);
        overview.DeployedFunctions.Should().NotBeEmpty();
    }

    [Fact]
    public void DeployFunction_DeploysNewEdgeIsolate()
    {
        var command = new DeployEdgeFunctionCommand(
            FunctionName: "A-B-Experiment-Edge-Splitter",
            Runtime: "V8-Isolate-ES2026",
            SourceCode: "export default { fetch(req) { return new Response('OK'); } }",
            CanarySplitPct: 25
        );

        var result = _engine.DeployFunction(command);

        result.Should().NotBeNull();
        result.FunctionName.Should().Be("A-B-Experiment-Edge-Splitter");
        result.CanaryTrafficSplitPct.Should().Be(25);
        result.DeploymentStatus.Should().Be("CanaryRolling");
    }

    [Fact]
    public void UpdateCanarySplit_AdjustsTrafficPercentage()
    {
        var command = new UpdateCanarySplitCommand("EDGE-FN-02", 100);
        var result = _engine.UpdateCanarySplit(command);

        result.Should().NotBeNull();
        result.CanaryTrafficSplitPct.Should().Be(100);
        result.DeploymentStatus.Should().Be("PromotedGA");
    }

    [Fact]
    public void TogglePoPHealth_FlipsNodeHealthState()
    {
        var node = _engine.TogglePoPHealth("IAD-01");

        node.Should().NotBeNull();
        node.IsHealthy.Should().BeFalse();

        var restored = _engine.TogglePoPHealth("IAD-01");
        restored.IsHealthy.Should().BeTrue();
    }
}
