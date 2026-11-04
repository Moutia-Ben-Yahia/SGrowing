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

    [Fact]
    public void PromoteHotStandby_NonExistentNode_ReturnsFailureResult()
    {
        var result = _engine.PromoteHotStandby("node_missing_999");

        Assert.False(result.Success);
        Assert.Contains("Target standby node not found", result.Message);
    }

    [Fact]
    public void PromoteHotStandby_WitnessNode_ReturnsFailureResult()
    {
        var result = _engine.PromoteHotStandby("node_sovereign_witness");

        Assert.False(result.Success);
        Assert.Contains("Witness node cannot be promoted to Primary", result.Message);
    }

    [Fact]
    public void PromoteHotStandby_QuorumLost_AbortsFailoverToPreventSplitBrain()
    {
        // 4 nodes total. To lose quorum, >= 2 nodes must fail (healthy count < 3)
        _engine.SimulateOutage(new SimulateOutageRequest("node_aws_primary", "Datacenter flood"));
        _engine.SimulateOutage(new SimulateOutageRequest("node_azure_standby", "Fiber cut"));

        var topology = _engine.GetTopology();
        Assert.False(topology.QuorumEstablished);

        var result = _engine.PromoteHotStandby("node_gcp_standby");
        Assert.False(result.Success);
        Assert.Contains("Quorum consensus lost", result.Message);
    }

    [Fact]
    public void SimulateOutage_NonExistentNode_DoesNotAffectTopology()
    {
        var initial = _engine.GetTopology();
        var after = _engine.SimulateOutage(new SimulateOutageRequest("node_ghost_unknown", "Unknown failure"));

        Assert.Equal(initial.ActivePrimaryNodeId, after.ActivePrimaryNodeId);
        Assert.Equal(initial.Nodes.Count, after.Nodes.Count);
        Assert.Equal(initial.QuorumEstablished, after.QuorumEstablished);
    }

    [Fact]
    public void PromoteHotStandby_AzureStandby_DemotesPreviousAwsPrimaryToHotStandby()
    {
        var result = _engine.PromoteHotStandby("node_azure_standby");

        Assert.True(result.Success);
        Assert.Equal("node_azure_standby", result.NewPrimaryNodeId);
        Assert.Equal("node_aws_primary", result.PreviousPrimaryNodeId);

        var topology = _engine.GetTopology();
        var formerPrimary = topology.Nodes.Find(n => n.NodeId == "node_aws_primary");
        var newPrimary = topology.Nodes.Find(n => n.NodeId == "node_azure_standby");

        Assert.NotNull(formerPrimary);
        Assert.NotNull(newPrimary);
        Assert.Equal(NodeRole.HotStandbySynchronous, formerPrimary.Role);
        Assert.Equal(NodeRole.PrimaryActive, newPrimary.Role);
        Assert.Equal(0.0, newPrimary.ReplicationLagMs);
    }
}
