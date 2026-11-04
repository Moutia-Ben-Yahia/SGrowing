using System;
using FluentAssertions;
using SGrowing.Api.Modules.Voice;
using Xunit;

namespace SGrowing.Tests.Voice;

public class OpenAiRealtimeVoiceBridgeTests
{
    private readonly OpenAiRealtimeBridgeService _bridge;
    private readonly BrowserSoftphoneSignalingService _softphone;

    public OpenAiRealtimeVoiceBridgeTests()
    {
        _bridge = new OpenAiRealtimeBridgeService();
        _softphone = new BrowserSoftphoneSignalingService();
    }

    [Fact]
    public void BuildSessionUpdate_ProducesValidRealtimeJsonProtocol()
    {
        var config = new RealtimeSessionConfig
        {
            Voice = "shimmer",
            AudioFormat = "g711_ulaw",
            Instructions = "Assist enterprise inbound callers with sales and support."
        };

        var json = _bridge.BuildSessionUpdateCommand(config);

        json.Should().Contain("session.update");
        json.Should().Contain("shimmer");
        json.Should().Contain("g711_ulaw");
        json.Should().Contain("server_vad");
        json.Should().Contain("Assist enterprise inbound");
    }

    [Fact]
    public void BuildSessionUpdate_DefaultConfig_UsesAlloyAndTurnDetection()
    {
        // Arrange
        var defaultConfig = new RealtimeSessionConfig();

        // Act
        var json = _bridge.BuildSessionUpdateCommand(defaultConfig);

        // Assert
        json.Should().Contain("alloy");
        json.Should().Contain("g711_ulaw");
        json.Should().Contain("server_vad");
        json.Should().Contain("0.7"); // temperature
    }

    [Fact]
    public void BuildAudioAppend_StructuresInputBufferPayload()
    {
        var fakeAudioBase64 = "AQIDBAUGBwgJCgsMDQ4PEA==";
        var json = _bridge.BuildAudioAppendCommand(fakeAudioBase64);

        json.Should().Contain("input_audio_buffer.append");
        json.Should().Contain(fakeAudioBase64);
    }

    [Fact]
    public void BuildAudioAppend_GeneratesUniqueEventIds()
    {
        // Act
        var j1 = _bridge.BuildAudioAppendCommand("AAA=");
        var j2 = _bridge.BuildAudioAppendCommand("AAA=");

        // Assert
        j1.Should().NotBe(j2);
        j1.Should().Contain("evt_");
        j2.Should().Contain("evt_");
    }

    [Fact]
    public void ParseUpstreamRealtimeEvent_HandlesDeltasAndBargeIn()
    {
        // 1. Audio delta event
        var audioDeltaJson = @"{
            ""type"": ""response.audio.delta"",
            ""event_id"": ""evt_12345"",
            ""delta"": ""AAAA////AAAA////""
        }";

        var msg1 = _bridge.ParseUpstreamRealtimeEvent(audioDeltaJson);
        msg1.EventType.Should().Be("response.audio.delta");
        msg1.AudioPayloadBase64.Should().Be("AAAA////AAAA////");
        msg1.IsBargeInInterruption.Should().BeFalse();

        // 2. Transcript delta event
        var transcriptDeltaJson = @"{
            ""type"": ""response.audio_transcript.delta"",
            ""event_id"": ""evt_67890"",
            ""delta"": ""Hello, how can I help you today?""
        }";

        var msg2 = _bridge.ParseUpstreamRealtimeEvent(transcriptDeltaJson);
        msg2.EventType.Should().Be("response.audio_transcript.delta");
        msg2.TranscriptDelta.Should().Be("Hello, how can I help you today?");

        // 3. Upstream speech started (barge-in)
        var speechStartJson = @"{
            ""type"": ""input_audio_buffer.speech_started"",
            ""event_id"": ""evt_vad_01""
        }";

        var msg3 = _bridge.ParseUpstreamRealtimeEvent(speechStartJson);
        msg3.EventType.Should().Be("input_audio_buffer.speech_started");
        msg3.IsBargeInInterruption.Should().BeTrue();
    }

    [Fact]
    public void ParseUpstreamRealtimeEvent_UnknownEvent_ReturnsEventTypeWithoutCrashing()
    {
        // Arrange
        var unknownJson = @"{
            ""type"": ""rate_limits.updated"",
            ""event_id"": ""evt_rate_99""
        }";

        // Act
        var msg = _bridge.ParseUpstreamRealtimeEvent(unknownJson);

        // Assert
        msg.EventType.Should().Be("rate_limits.updated");
        msg.EventId.Should().Be("evt_rate_99");
        msg.AudioPayloadBase64.Should().BeNull();
        msg.TranscriptDelta.Should().BeNull();
        msg.IsBargeInInterruption.Should().BeFalse();
    }

    [Fact]
    public void BrowserSoftphone_GeneratesSecureSessionTokenAndIceServers()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var config = _softphone.CreateSessionConfig(tenantId, userId, "telephony.sgrowing.com");

        config.SessionToken.Should().NotBeEmpty();
        config.SessionToken.Should().StartWith("sp_tok_");
        config.WebSocketUrl.Should().Contain(config.SessionToken);
        config.WebSocketUrl.Should().Contain(tenantId.ToString());
        config.IceServers.Should().NotBeEmpty();
        config.IceServers.Should().Contain("stun:stun.l.google.com:19302");
        config.SampleRate.Should().Be(16000);
    }

    [Fact]
    public void BrowserSoftphone_DifferentUsers_GenerateUniqueTokens()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();

        // Act
        var c1 = _softphone.CreateSessionConfig(tenantId, u1);
        var c2 = _softphone.CreateSessionConfig(tenantId, u2);

        // Assert
        c1.SessionToken.Should().NotBe(c2.SessionToken);
        c1.WebSocketUrl.Should().NotBe(c2.WebSocketUrl);
    }
}
