using SGrowing.Api.Modules.Voice.TelephonyWhisper;
using Xunit;

namespace SGrowing.Tests.Voice;

public class TelephonyWhisperTests
{
    [Fact]
    public void GetOverview_ReportsActiveCallsAndSub100msLatency()
    {
        var engine = new TelephonyWhisperCoachEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveSupervisedCallsCount >= 2);
        Assert.True(overview.AverageDualChannelLatencyMs < 120.0);
        Assert.True(overview.TotalWhispersInjectedCount >= 2);
        Assert.NotEmpty(overview.ActiveSessions);
        Assert.NotEmpty(overview.RecentWhispers);
    }

    [Fact]
    public void InjectWhisper_DispatchesPromptDirectlyToAgentAudioStream()
    {
        var engine = new TelephonyWhisperCoachEngine();
        var request = new InjectWhisperRequest(
            "CALL-WHISP-002",
            "Customer asked about SLA guarantee; confirm 99.99% multi-region uptime commit",
            "COMPLIANCE_WARNING",
            "Elena Rostova (Sales Director)"
        );

        var whisper = engine.InjectWhisper(request);

        Assert.NotNull(whisper);
        Assert.StartsWith("WSP-", whisper.WhisperId);
        Assert.Equal("CALL-WHISP-002", whisper.CallSessionId);

        var overview = engine.GetOverview();
        var session = overview.ActiveSessions.FirstOrDefault(s => s.CallSessionId == "CALL-WHISP-002");
        Assert.NotNull(session);
        Assert.Contains(whisper.PromptText, session.InjectedWhispers);
    }

    [Fact]
    public void TransitionSupervisionMode_ExecutesHotSupervisoryTakeover()
    {
        var engine = new TelephonyWhisperCoachEngine();
        var request = new TakeoverCallRequest(
            "CALL-WHISP-001",
            "FULL_TAKEOVER",
            "Elena Rostova (Sales Director)",
            "Executive pricing concession intervention"
        );

        var session = engine.TransitionSupervisionMode(request);

        Assert.NotNull(session);
        Assert.Equal("FULL_TAKEOVER", session.ActiveSupervisionMode);
        Assert.Equal("ESCALATED", session.ComplianceStatus);

        var overview = engine.GetOverview();
        Assert.True(overview.TakeoverEscalationsCount >= 1);
    }

    [Fact]
    public void TransitionSupervisionMode_BargeIn_SetsModeAndPreservesComplianceStatus()
    {
        var engine = new TelephonyWhisperCoachEngine();
        var request = new TakeoverCallRequest(
            "CALL-WHISP-002",
            "BARGE_IN",
            "Elena Rostova (Sales Director)",
            "Step in to clarify enterprise security architecture"
        );

        var session = engine.TransitionSupervisionMode(request);

        Assert.NotNull(session);
        Assert.Equal("BARGE_IN", session.ActiveSupervisionMode);
        Assert.Equal("CLEAN", session.ComplianceStatus); // Preserves existing "CLEAN" status
    }

    [Fact]
    public void TransitionSupervisionMode_UnknownCallSession_CreatesEscalatedFallbackSession()
    {
        var engine = new TelephonyWhisperCoachEngine();
        var request = new TakeoverCallRequest(
            "CALL-NONEXISTENT-999",
            "FULL_TAKEOVER",
            "Marcus Vance",
            "Emergency customer escalation"
        );

        var session = engine.TransitionSupervisionMode(request);

        Assert.NotNull(session);
        Assert.Equal("CALL-NONEXISTENT-999", session.CallSessionId);
        Assert.Equal("FULL_TAKEOVER", session.ActiveSupervisionMode);
        Assert.Equal("ESCALATED", session.ComplianceStatus);
        Assert.Equal("Marcus Vance", session.SupervisorName);
    }

    [Fact]
    public void InjectWhisper_MultiplePrompts_AppendsSequentiallyAndSetsWhisperingMode()
    {
        var engine = new TelephonyWhisperCoachEngine();
        // CALL-WHISP-002 starts in MONITORING mode with 1 injected whisper
        var req1 = new InjectWhisperRequest("CALL-WHISP-002", "Hint 1: offer SOC2 package", "TECH_SPEC", "Elena Rostova");
        var req2 = new InjectWhisperRequest("CALL-WHISP-002", "Hint 2: mention volume discount", "PRICING_CONCESSION", "Elena Rostova");

        engine.InjectWhisper(req1);
        engine.InjectWhisper(req2);

        var overview = engine.GetOverview();
        var session = overview.ActiveSessions.FirstOrDefault(s => s.CallSessionId == "CALL-WHISP-002");
        Assert.NotNull(session);
        Assert.Equal("WHISPERING", session.ActiveSupervisionMode);
        Assert.Equal(3, session.InjectedWhispers.Count);
        Assert.Contains("Hint 1: offer SOC2 package", session.InjectedWhispers);
        Assert.Contains("Hint 2: mention volume discount", session.InjectedWhispers);
    }
}
