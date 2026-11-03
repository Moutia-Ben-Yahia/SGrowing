using SGrowing.Api.Modules.Infrastructure.DisasterRecovery;
using Xunit;

namespace SGrowing.Tests.Infrastructure;

public class DisasterRecoveryTests
{
    private readonly DisasterRecoveryEngine _engine = new();

    [Fact]
    public void InitialTopology_HasPrimaryAndQuorumEstablished()
    {
        var topology = _engine.GetTopology();

        Assert.NotNull(topology);
        Assert.Equal("node_aws_primary", topology.ActivePrimaryNodeId);
        Assert.True(topology.QuorumEstablished);
        Assert.Equal(4, topology.Nodes.Count);
        Assert.True(topology.RpoSeconds < 1.0);
    }

    [Fact]
    public void SimulateOutage_MarksNodeUnreachable()
    {
        var updated = _engine.SimulateOutage(new SimulateOutageRequest("node_aws_primary", "Simulated AWS us-east-1 power loss"));

        var awsNode = updated.Nodes.Find(n => n.NodeId == "node_aws_primary");
        Assert.NotNull(awsNode);
        Assert.Equal(ClusterHealth.UnreachableOutage, awsNode.Health);
        // Quorum is still maintained by the other 3 nodes
        Assert.True(updated.QuorumEstablished);
    }

    [Fact]
    public void PromoteHotStandby_PromotesGcpStandbyToPrimary()
    {
        var result = _engine.PromoteHotStandby("node_gcp_standby");

        Assert.True(result.Success);
        Assert.Equal("node_gcp_standby", result.NewPrimaryNodeId);
        Assert.Equal("node_aws_primary", result.PreviousPrimaryNodeId);
        Assert.True(result.ElapsedFailoverSeconds <= 30.0);

        var topology = _engine.GetTopology();
        Assert.Equal("node_gcp_standby", topology.ActivePrimaryNodeId);
    }
}
