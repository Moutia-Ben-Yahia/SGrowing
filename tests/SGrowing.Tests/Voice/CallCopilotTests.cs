using SGrowing.Api.Modules.Voice.CallCopilot;

namespace SGrowing.Tests.Voice;

public class CallCopilotTests
{
    private readonly ICallCopilotEngine _engine = new CallCopilotEngine();

    [Fact]
    public void ListSessions_ReturnsActiveSeededCall()
    {
        var sessions = _engine.ListSessions();

        Assert.NotEmpty(sessions);
        Assert.Equal("call_demo_live", sessions[0].SessionId);
        Assert.Equal(3, sessions[0].Utterances.Count);
        Assert.NotEmpty(sessions[0].SurfacedBattlecards);
    }

    [Fact]
    public void IngestUtterance_SurfacesBattlecardOnKeywordMatch()
    {
        var session = _engine.StartSession("agent_neo", "+1-212-555-0144", "Matrix Dynamics");

        var utterance = _engine.IngestUtterance(new IngestUtteranceRequest(
            SessionId: session.SessionId,
            Speaker: SpeakerRole.Prospect,
            Text: "We currently use HubSpot and migrating seems like too much hassle."
        ));

        Assert.NotNull(utterance);
        var updated = _engine.GetSession(session.SessionId);
        Assert.NotNull(updated);
        Assert.Single(updated.Utterances);
        Assert.Contains(updated.SurfacedBattlecards, c => c.CardId == "bc_competitor_stack");
    }

    [Fact]
    public void EndSessionAndSummarize_GeneratesExecutiveNotesAndActionItems()
    {
        var session = _engine.StartSession("agent_trinity", "+1-415-555-0988", "Zion Technologies");

        _engine.IngestUtterance(new IngestUtteranceRequest(
            SessionId: session.SessionId,
            Speaker: SpeakerRole.Agent,
            Text: "Can you tell us about your security requirements?"
        ));

        _engine.IngestUtterance(new IngestUtteranceRequest(
            SessionId: session.SessionId,
            Speaker: SpeakerRole.Prospect,
            Text: "We need full SOC2 Type II air-gapped security."
        ));

        var summary = _engine.EndSessionAndSummarize(session.SessionId);

        Assert.NotNull(summary);
        Assert.Contains("Zion Technologies", summary.ExecutiveSummary);
        Assert.NotEmpty(summary.ActionItems);
        Assert.Equal("Qualified Opportunity", summary.OverallDisposition);

        var finalSession = _engine.GetSession(session.SessionId);
        Assert.Equal("Completed", finalSession?.Status);
    }
}
