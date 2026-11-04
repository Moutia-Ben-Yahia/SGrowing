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

    [Fact]
    public void InitiateWarmTransfer_CallNotFound_ThrowsKeyNotFoundException()
    {
        var request = new InitiateWarmTransferRequest("call_nonexistent_999", "Specialist_Agent");
        var act = () => _engine.InitiateWarmTransfer(request);

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*call_nonexistent_999*");
    }

    [Fact]
    public void CompleteTransfer_CallNotFound_ThrowsKeyNotFoundException()
    {
        var request = new CompleteTransferRequest("call_missing_000");
        var act = () => _engine.CompleteTransfer(request);

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*call_missing_000*");
    }

    [Fact]
    public void GetCall_NonExistentCall_ReturnsNull()
    {
        var call = _engine.GetCall("call_not_found");
        call.Should().BeNull();
    }

    [Fact]
    public void CompleteTransfer_HighInitialSentiment_CapsAtOne()
    {
        var callId = "call_vce_801"; // Starts at 0.65
        var req = new CompleteTransferRequest(callId);

        var updated1 = _engine.CompleteTransfer(req); // 0.65 + 0.4 = 1.0 (capped)
        updated1.CallerSentimentScore.Should().Be(1.0);

        var updated2 = _engine.CompleteTransfer(req);
        updated2.CallerSentimentScore.Should().Be(1.0);
    }
}
