using FluentAssertions;
using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Voice;

public class BargeInAndSignalingServiceTests
{
    private readonly BargeInInterruptionService _bargeInService = new();
    private readonly BrowserSoftphoneSignalingService _signalingService = new();

    #region BargeInInterruptionService Tests

    [Fact]
    public void CreateClearBufferCommand_ReturnsExpectedJsonEvent()
    {
        // Act
        var command = _bargeInService.CreateClearBufferCommand("MZ123456789");

        // Assert
        command.Should().Be("{\"event\":\"clear\",\"streamSid\":\"MZ123456789\"}");
    }

    [Fact]
    public void CreateMarkCommand_ReturnsExpectedJsonEvent()
    {
        // Act
        var command = _bargeInService.CreateMarkCommand("MZ123456789", "speech_start_mark");

        // Assert
        command.Should().Be("{\"event\":\"mark\",\"streamSid\":\"MZ123456789\",\"mark\":{\"name\":\"speech_start_mark\"}}");
    }

    [Fact]
    public void CreateMediaChunkCommand_ReturnsExpectedJsonEvent()
    {
        // Act
        var command = _bargeInService.CreateMediaChunkCommand("MZ123456789", "AQIDBA==");

        // Assert
        command.Should().Be("{\"event\":\"media\",\"streamSid\":\"MZ123456789\",\"media\":{\"payload\":\"AQIDBA==\"}}");
    }

    [Fact]
    public void InitiateWarmSipTransfer_UpdatesSessionAndReturnsValidTwiMl()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var session = StreamingVoiceCallSession.Create(tenantId, "CA111222333", "MZ999888777");
        session.SetAgentSpeaking(true);

        var sipUri = "sip:specialist@pbx.sgrowing.internal";
        var reason = "Complex escalation requested by customer";
        var summary = "Customer wants to renegotiate enterprise tier";

        // Act
        var result = _bargeInService.InitiateWarmSipTransfer(session, sipUri, reason, summary);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TargetSipUri.Should().Be(sipUri);
        result.ContextHeaders.Should().ContainKey("X-Session-Id");
        result.ContextHeaders.Should().ContainKey("X-Call-Sid");
        result.ContextHeaders["X-Call-Sid"].Should().Be("CA111222333");
        result.ContextHeaders["X-Transfer-Reason"].Should().Be(reason);
        result.ContextHeaders["X-Conversation-Summary"].Should().Be(Uri.EscapeDataString(summary));

        // Session state verification
        session.IsTransferred.Should().BeTrue();
        session.TransferTargetSipUri.Should().Be(sipUri);
        session.TransferReason.Should().Be(reason);
        session.IsAgentSpeaking.Should().BeFalse();
        session.EndedAtUtc.Should().NotBeNull();

        // TwiML verification
        result.TwiMlXml.Should().Contain("<Response>");
        result.TwiMlXml.Should().Contain("<Say voice=\"Polly.Joanna-Neural\">");
        result.TwiMlXml.Should().Contain("<Dial>");
        result.TwiMlXml.Should().Contain("<Sip>");
        result.TwiMlXml.Should().Contain(sipUri);
    }

    #endregion

    #region BrowserSoftphoneSignalingService Tests

    [Fact]
    public void CreateSessionConfig_ReturnsValidSignalingConfiguration()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var config = _signalingService.CreateSessionConfig(tenantId, userId, "sip-gw.sgrowing.io");

        // Assert
        config.Should().NotBeNull();
        config.SessionToken.Should().StartWith("sp_tok_");
        config.WebSocketUrl.Should().Be($"wss://sip-gw.sgrowing.io/ws/voice/softphone?token={config.SessionToken}&tenantId={tenantId}&userId={userId}");
        config.IceServers.Should().Contain("stun:stun.l.google.com:19302");
        config.IceServers.Should().Contain("stun:stun1.l.google.com:19302");
        config.SampleRate.Should().Be(16000);
    }

    #endregion

    #region StreamingVoiceCallSession Entity Tests

    [Fact]
    public void StreamingVoiceCallSession_BargeInInterruption_TriggersCorrectVadAndCount()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var session = StreamingVoiceCallSession.Create(tenantId, "CA12345", "MZ12345");
        session.SetAgentSpeaking(true);

        session.IsAgentSpeaking.Should().BeTrue();
        session.InterruptionCount.Should().Be(0);

        // Act: User speaks while agent is speaking -> BargeInInterrupted
        session.RecordInboundFrame(byteCount: 320, rmsEnergy: 0.85, isSpeech: true, latencyMs: 12.5);

        // Assert
        session.CurrentVadStatus.Should().Be(VadStatus.BargeInInterrupted);
        session.InterruptionCount.Should().Be(1);
        session.IsAgentSpeaking.Should().BeFalse();
        session.TotalFramesReceived.Should().Be(1);
        session.AverageProcessingLatencyMs.Should().Be(12.5);
    }

    [Fact]
    public void StreamingVoiceCallSession_NormalSpeech_TriggersSpeechDetected()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var session = StreamingVoiceCallSession.Create(tenantId, "CA12345", "MZ12345");
        session.SetAgentSpeaking(false);

        // Act: User speaks while agent is silent -> SpeechDetected
        session.RecordInboundFrame(byteCount: 320, rmsEnergy: 0.65, isSpeech: true, latencyMs: 10.0);

        // Assert
        session.CurrentVadStatus.Should().Be(VadStatus.SpeechDetected);
        session.InterruptionCount.Should().Be(0);
        session.TotalFramesReceived.Should().Be(1);
    }

    [Fact]
    public void StreamingVoiceCallSession_SilenceFrame_TriggersSilenceVad()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var session = StreamingVoiceCallSession.Create(tenantId, "CA12345", "MZ12345");

        // Act
        session.RecordInboundFrame(byteCount: 320, rmsEnergy: 0.02, isSpeech: false, latencyMs: 5.0);

        // Assert
        session.CurrentVadStatus.Should().Be(VadStatus.Silence);
        session.TotalFramesReceived.Should().Be(1);
    }

    [Fact]
    public void StreamingVoiceCallSession_CalculatesRunningAverageLatencyCorrectly()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var session = StreamingVoiceCallSession.Create(tenantId, "CA12345", "MZ12345");

        // Act: 3 frames with 10ms, 20ms, 30ms latency
        session.RecordInboundFrame(320, 0.1, false, 10.0);
        session.RecordInboundFrame(320, 0.1, false, 20.0);
        session.RecordInboundFrame(320, 0.1, false, 30.0);

        // Assert
        session.TotalFramesReceived.Should().Be(3);
        session.AverageProcessingLatencyMs.Should().BeApproximately(20.0, 0.001);
    }

    #endregion
}
