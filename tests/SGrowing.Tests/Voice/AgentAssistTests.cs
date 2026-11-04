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

    [Fact]
    public void TriggerBattlecard_AddsRequestedCompetitorToActiveSession()
    {
        var session = _engine.TriggerBattlecard("call_live_501", "Gong");

        Assert.NotNull(session);
        Assert.Contains(session.ActiveBattlecards, b => b.CompetitorName == "Gong");
        var gongCard = session.ActiveBattlecards.First(b => b.CompetitorName == "Gong");
        Assert.Equal("28% higher close rate on objection calls", gongCard.ProofMetric);
    }

    [Fact]
    public void TriggerBattlecard_UnknownCompetitor_DoesNotAddCard()
    {
        var before = _engine.GetSessionById("call_live_501")!;
        var beforeCount = before.ActiveBattlecards.Count;

        var after = _engine.TriggerBattlecard("call_live_501", "UnknownLegacyVendor");
        Assert.Equal(beforeCount, after.ActiveBattlecards.Count);
    }

    [Fact]
    public void StreamChunk_CustomerConcern_SetsSkepticalSentiment()
    {
        var request = new StreamChunkRequest(
            "call_live_501",
            "Customer",
            "We have a serious concern that your enterprise pricing might be too expensive.",
            10.0
        );

        var updated = _engine.StreamChunk(request);

        Assert.NotNull(updated);
        var lastChunk = updated.RecentTranscripts.Last();
        Assert.Equal("Skeptical", lastChunk.Sentiment);
    }

    [Fact]
    public void GetSessionById_NonExistentSession_ReturnsNull()
    {
        var session = _engine.GetSessionById("call_missing_99999");
        Assert.Null(session);
    }

    [Fact]
    public void StreamChunk_WhenSessionNotFound_InitializesNewSession()
    {
        var request = new StreamChunkRequest(
            "call_brand_new_999",
            "Customer",
            "Can you tell me more about your system?",
            15.0
        );

        var session = _engine.StreamChunk(request);

        Assert.NotNull(session);
        Assert.Equal("call_brand_new_999", session.CallSessionId);
        Assert.Equal("Prospective Enterprise", session.CustomerCompany);
        Assert.Single(session.RecentTranscripts);
    }
}
