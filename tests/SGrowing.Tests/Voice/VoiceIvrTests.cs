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
}
