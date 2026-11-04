using SGrowing.Api.Modules.Voice.IvrTree;
using Xunit;

namespace SGrowing.Tests.Voice;

public sealed class VoiceIvrTests
{
    private readonly VoiceIvrEngine _engine = new();

    [Fact]
    public void ListTrees_ReturnsDefaultIvrTrees()
    {
        var trees = _engine.ListTrees();

        Assert.NotEmpty(trees);
        Assert.Contains(trees, t => t.Id == "tree_enterprise_main");
        Assert.Equal(4, trees[0].Nodes.Count);
    }

    [Fact]
    public void SimulateCall_TraversesDtmfBranchingCorrectly()
    {
        var request = new SimulateIvrCallRequest(
            TreeId: "tree_enterprise_main",
            CallerNumber: "+14155552671",
            Inputs: new[] { "1", "Enterprise" }
        );

        var result = _engine.SimulateCall(request);

        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.Equal("node_transfer_ae", result.FinalNodeId);
        Assert.Contains(result.ExecutionLog, l => l.Contains("[Route Match]"));
    }

    [Fact]
    public void CreateBroadcast_RegistersCampaignSuccessfully()
    {
        var campaign = new VoiceBroadcastCampaign(
            Id: "cmp_test_101",
            Name: "Test Campaign",
            CallerId: "+18005550100",
            TargetCount: 100,
            MaxConcurrentChannels: 10,
            EnableAmd: true,
            Status: "Scheduled",
            DeliveredCalls: 0,
            AmdVoicemailsDetected: 0
        );

        var created = _engine.CreateBroadcast(campaign);
        Assert.Equal("cmp_test_101", created.Id);

        var fetched = _engine.GetBroadcast("cmp_test_101");
        Assert.NotNull(fetched);
        Assert.Equal("Test Campaign", fetched.Name);
    }

    [Fact]
    public void SimulateCall_UnknownTreeId_ReturnsNotCompletedWithErrorLog()
    {
        var request = new SimulateIvrCallRequest(
            TreeId: "tree_nonexistent_xyz",
            CallerNumber: "+14155552671",
            Inputs: new[] { "1" }
        );

        var result = _engine.SimulateCall(request);

        Assert.NotNull(result);
        Assert.False(result.IsCompleted);
        Assert.Equal("none", result.FinalNodeId);
        Assert.Contains(result.ExecutionLog, l => l.Contains("Error: Tree 'tree_nonexistent_xyz' not found."));
    }

    [Fact]
    public void SimulateCall_RouteToSupport_ReachesVoicemailTerminalNode()
    {
        var request = new SimulateIvrCallRequest(
            TreeId: "tree_enterprise_main",
            CallerNumber: "+14155552671",
            Inputs: new[] { "2" } // Press 2 for Support
        );

        var result = _engine.SimulateCall(request);

        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.Equal("node_support", result.FinalNodeId);
        Assert.Contains(result.ExecutionLog, l => l.Contains("[Terminal Action] Terminal node 'Voicemail' reached."));
    }

    [Fact]
    public void SimulateCall_UnmappedInput_TriggersFallbackNode()
    {
        var request = new SimulateIvrCallRequest(
            TreeId: "tree_enterprise_main",
            CallerNumber: "+14155552671",
            Inputs: new[] { "9" } // 9 is unmapped, fallback is node_support
        );

        var result = _engine.SimulateCall(request);

        Assert.NotNull(result);
        Assert.True(result.IsCompleted);
        Assert.Equal("node_support", result.FinalNodeId);
        Assert.Contains(result.ExecutionLog, l => l.Contains("[Fallback] Routing to fallback node"));
    }

    [Fact]
    public void GetTree_NonExistentTreeId_ReturnsNull()
    {
        var tree = _engine.GetTree("missing_tree_id_999");
        Assert.Null(tree);
    }

    [Fact]
    public void GetBroadcast_NonExistentCampaignId_ReturnsNull()
    {
        var cmp = _engine.GetBroadcast("missing_campaign_id_999");
        Assert.Null(cmp);
    }
}
