using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Voice.WebRtcSdr;
using Xunit;

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

        session.Should().NotBeNull();
        session.SessionId.Should().StartWith("vcall_");
        session.Status.Should().Be(VoiceSessionStatus.Active);
        session.Transcript.Should().ContainSingle();
        session.Transcript[0].Speaker.Should().Be("AI_SDR");
        session.LatencyMs.Should().BeLessThanOrEqualTo(200);
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

        response.Should().NotBeNull();
        response.NewVadState.Should().Be(VadState.UserSpeaking);
        response.AiReplyText.Should().NotBeNull();
        response.Base64SynthesizedAudio.Should().NotBeNull();

        var updated = _engine.GetSession(session.SessionId);
        updated.Should().NotBeNull();
        updated!.Transcript.Count.Should().Be(3); // initial + user + ai reply
    }

    [Fact]
    public void ProcessAudioPacket_BargeIn_WhenAgentWasSpeaking_TransitionsToBargeInInterrupted()
    {
        // Arrange
        var session = _engine.StartSession(new StartVoiceSessionRequest(
            TenantId: "tenant_voice_barge",
            LeadId: "lead_barge",
            PhoneNumber: "+15550003333",
            AgentPersona: "Jordan"
        ));

        // Simulate agent currently speaking
        var agentSpeakingSession = session with { CurrentVadState = VadState.AgentSpeaking };
        typeof(WebRtcSdrEngine)
            .GetField("_sessions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(_engine)
            .As<System.Collections.Concurrent.ConcurrentDictionary<string, VoiceSdrSession>>()[session.SessionId] = agentSpeakingSession;

        var packet = new AudioStreamPacket(
            SessionId: session.SessionId,
            Base64AudioData: "AAABAAEAQB8AAEAfAAABAA==",
            AudioFormat: "pcm_16000",
            IsLeadSpeaking: true
        );

        // Act
        var response = _engine.ProcessAudioPacket(packet);

        // Assert
        response.NewVadState.Should().Be(VadState.BargeInInterrupted, "interrupting speaking agent should transition state to BargeInInterrupted");
    }

    [Fact]
    public void ProcessAudioPacket_WhenLeadNotSpeaking_TransitionsToSilence()
    {
        // Arrange
        var session = _engine.StartSession(new StartVoiceSessionRequest(
            TenantId: "tenant_voice_silence",
            LeadId: "lead_silence",
            PhoneNumber: "+15550004444",
            AgentPersona: "Sam"
        ));

        var packet = new AudioStreamPacket(
            SessionId: session.SessionId,
            Base64AudioData: "",
            AudioFormat: "pcm_16000",
            IsLeadSpeaking: false
        );

        // Act
        var response = _engine.ProcessAudioPacket(packet);

        // Assert
        response.NewVadState.Should().Be(VadState.Silence);
        response.AiReplyText.Should().BeNull();
        response.Base64SynthesizedAudio.Should().BeNull();
    }

    [Fact]
    public void ProcessAudioPacket_ThrowsKeyNotFoundException_WhenSessionNotFound()
    {
        // Arrange
        var packet = new AudioStreamPacket(
            SessionId: "nonexistent_session_999",
            Base64AudioData: "AAA=",
            AudioFormat: "pcm_16000",
            IsLeadSpeaking: true
        );

        // Act
        var act = () => _engine.ProcessAudioPacket(packet);

        // Assert
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*nonexistent_session_999*");
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

        ended.Status.Should().Be(VoiceSessionStatus.Completed);
        ended.EndedAtUtc.Should().NotBeNull();
        ended.CurrentVadState.Should().Be(VadState.Silence);
    }

    [Fact]
    public void EndSession_ThrowsKeyNotFoundException_WhenSessionNotFound()
    {
        // Arrange
        var endReq = new EndVoiceSessionRequest("invalid_session_id", "NoAnswer");

        // Act
        var act = () => _engine.EndSession(endReq);

        // Assert
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*invalid_session_id*");
    }

    [Fact]
    public void ListSessions_ReturnsTenantSessions()
    {
        var tenant = "tenant_list_test";
        _engine.StartSession(new StartVoiceSessionRequest(tenant, "l1", "+1001", "Bot 1"));
        _engine.StartSession(new StartVoiceSessionRequest(tenant, "l2", "+1002", "Bot 2"));

        var list = _engine.ListSessions(tenant);
        list.Count.Should().Be(2);
    }
}
