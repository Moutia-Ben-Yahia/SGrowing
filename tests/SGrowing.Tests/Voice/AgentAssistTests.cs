using SGrowing.Api.Modules.Voice.AgentAssist;
using Xunit;

namespace SGrowing.Tests.Voice;

public class AgentAssistTests
{
    private readonly AgentAssistEngine _engine = new();

    [Fact]
    public void SeededSession_HasTranscriptsAndCompetitorBattlecard()
    {
        var session = _engine.GetSessionById("call_live_501");

        Assert.NotNull(session);
        Assert.Equal("Vanguard Logistics Inc", session.CustomerCompany);
        Assert.Equal(2, session.RecentTranscripts.Count);
        Assert.Contains(session.ActiveBattlecards, b => b.CompetitorName == "HubSpot");
        Assert.False(session.TalkPacingAlertActive);
    }

    [Fact]
    public void StreamChunk_DetectsCompetitorAndAddsBattlecard()
    {
        var request = new StreamChunkRequest(
            "call_live_501",
            "Customer",
            "Our board wants to know how you compare to Salesforce on implementation timeline.",
            15.0
        );

        var updated = _engine.StreamChunk(request);

        Assert.NotNull(updated);
        Assert.Contains(updated.ActiveBattlecards, b => b.CompetitorName == "Salesforce");
    }

    [Fact]
    public void ExcessiveRepTalkTime_TriggersPacingAlertAndCoachingSuggestion()
    {
        // Stream heavy rep speaking
        var request = new StreamChunkRequest(
            "call_live_501",
            "Representative",
            "Let me explain our complete 50-step architecture in extreme detail...",
            300.0 // 300 seconds rep talk time
        );

        var updated = _engine.StreamChunk(request);

        Assert.True(updated.RepTalkPercentage > 65.0);
        Assert.True(updated.TalkPacingAlertActive);
        Assert.Contains(updated.LiveSuggestions, s => s.Category == "PacingAlert");
    }
}
