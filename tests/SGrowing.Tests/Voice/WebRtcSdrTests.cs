using SGrowing.Api.Modules.Voice.WebRtcSdr;

namespace SGrowing.Tests.Voice;

public class WebRtcSdrTests
{
    private readonly WebRtcSdrEngine _engine = new();

    [Fact]
    public void StartSession_InitializesSessionWithActiveStatus()
    {
        var req = new StartVoiceSessionRequest(
            TenantId: "tenant_voice_test",
            LeadId: "lead_enterprise_99",
            PhoneNumber: "+15550198282",
            AgentPersona: "Sarah Sales SDR"
        );

        var session = _engine.StartSession(req);

        Assert.NotNull(session);
        Assert.StartsWith("vcall_", session.SessionId);
        Assert.Equal(VoiceSessionStatus.Active, session.Status);
        Assert.Single(session.Transcript);
        Assert.Equal("AI_SDR", session.Transcript[0].Speaker);
        Assert.True(session.LatencyMs <= 200);
    }

    [Fact]
    public void ProcessAudioPacket_WhenUserSpeaks_DetectsVadAndGeneratesReply()
    {
        var session = _engine.StartSession(new StartVoiceSessionRequest(
            TenantId: "tenant_voice_test",
            LeadId: "lead_123",
            PhoneNumber: "+15550001111",
            AgentPersona: "Alex"
        ));

        var packet = new AudioStreamPacket(
            SessionId: session.SessionId,
            Base64AudioData: "AAABAAEAQB8AAEAfAAABAA==",
            AudioFormat: "pcm_16000",
            IsLeadSpeaking: true
        );

        var response = _engine.ProcessAudioPacket(packet);

        Assert.NotNull(response);
        Assert.Equal(VadState.UserSpeaking, response.NewVadState);
        Assert.NotNull(response.AiReplyText);
        Assert.NotNull(response.Base64SynthesizedAudio);

        var updated = _engine.GetSession(session.SessionId);
        Assert.NotNull(updated);
        Assert.Equal(3, updated.Transcript.Count); // initial + user + ai reply
    }

    [Fact]
    public void EndSession_TransitionsToCompleted()
    {
        var session = _engine.StartSession(new StartVoiceSessionRequest(
            TenantId: "tenant_voice_test",
            LeadId: "lead_456",
            PhoneNumber: "+15550002222",
            AgentPersona: "Jordan"
        ));

        var endReq = new EndVoiceSessionRequest(session.SessionId, "Qualified");
        var ended = _engine.EndSession(endReq);

        Assert.Equal(VoiceSessionStatus.Completed, ended.Status);
        Assert.NotNull(ended.EndedAtUtc);
    }

    [Fact]
    public void ListSessions_ReturnsTenantSessions()
    {
        var tenant = "tenant_list_test";
        _engine.StartSession(new StartVoiceSessionRequest(tenant, "l1", "+1001", "Bot 1"));
        _engine.StartSession(new StartVoiceSessionRequest(tenant, "l2", "+1002", "Bot 2"));

        var list = _engine.ListSessions(tenant);
        Assert.Equal(2, list.Count);
    }
}
