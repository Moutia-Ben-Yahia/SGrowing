using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Voice.CallTransferMesh;
using Xunit;

namespace SGrowing.Tests.Voice;

public class CallTransferMeshTests
{
    private readonly CallTransferMeshEngine _engine = new();

    [Fact]
    public void GetActiveCalls_ReturnsCallsWithSentiment()
    {
        var calls = _engine.GetActiveCalls();

        calls.Should().NotBeNull();
        calls.Should().HaveCount(2);
        calls.Select(c => c.CallId).Should().Contain(new[] { "call_vce_801", "call_vce_802" });
    }

    [Fact]
    public void InitiateWarmTransfer_BridgesThreeWayConference()
    {
        var callId = "call_vce_801";
        var request = new InitiateWarmTransferRequest(callId, "Sarah_Director_Solutions");

        var updated = _engine.InitiateWarmTransfer(request);

        updated.TargetHumanSpecialist.Should().Be("Sarah_Director_Solutions");
        updated.State.Should().Be(CallTransferState.ThreeWayBridge);
    }

    [Fact]
    public void CompleteTransfer_FinalizesHandover_ImprovesSentiment()
    {
        var callId = "call_vce_802";
        var beforeSentiment = _engine.GetCall(callId)!.CallerSentimentScore;
        var request = new CompleteTransferRequest(callId);

        var updated = _engine.CompleteTransfer(request);

        updated.State.Should().Be(CallTransferState.TransferCompleted);
        updated.CallerSentimentScore.Should().BeGreaterThan(beforeSentiment);
    }
}
