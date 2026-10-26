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

        Assert.Contains("session.update", json);
        Assert.Contains("shimmer", json);
        Assert.Contains("g711_ulaw", json);
        Assert.Contains("server_vad", json);
        Assert.Contains("Assist enterprise inbound", json);
    }

    [Fact]
    public void BuildAudioAppend_StructuresInputBufferPayload()
    {
        var fakeAudioBase64 = "AQIDBAUGBwgJCgsMDQ4PEA==";
        var json = _bridge.BuildAudioAppendCommand(fakeAudioBase64);

        Assert.Contains("input_audio_buffer.append", json);
        Assert.Contains(fakeAudioBase64, json);
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
        Assert.Equal("response.audio.delta", msg1.EventType);
        Assert.Equal("AAAA////AAAA////", msg1.AudioPayloadBase64);
        Assert.False(msg1.IsBargeInInterruption);

        // 2. Transcript delta event
        var transcriptDeltaJson = @"{
            ""type"": ""response.audio_transcript.delta"",
            ""event_id"": ""evt_67890"",
            ""delta"": ""Hello, how can I help you today?""
        }";

        var msg2 = _bridge.ParseUpstreamRealtimeEvent(transcriptDeltaJson);
        Assert.Equal("response.audio_transcript.delta", msg2.EventType);
        Assert.Equal("Hello, how can I help you today?", msg2.TranscriptDelta);

        // 3. Upstream speech started (barge-in)
        var speechStartJson = @"{
            ""type"": ""input_audio_buffer.speech_started"",
            ""event_id"": ""evt_vad_01""
        }";

        var msg3 = _bridge.ParseUpstreamRealtimeEvent(speechStartJson);
        Assert.Equal("input_audio_buffer.speech_started", msg3.EventType);
        Assert.True(msg3.IsBargeInInterruption);
    }

    [Fact]
    public void BrowserSoftphone_GeneratesSecureSessionTokenAndIceServers()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var config = _softphone.CreateSessionConfig(tenantId, userId, "telephony.sgrowing.com");

        Assert.NotEmpty(config.SessionToken);
        Assert.StartsWith("sp_tok_", config.SessionToken);
        Assert.Contains(config.SessionToken, config.WebSocketUrl);
        Assert.Contains(tenantId.ToString(), config.WebSocketUrl);
        Assert.NotEmpty(config.IceServers);
        Assert.Contains("stun:stun.l.google.com:19302", config.IceServers);
        Assert.Equal(16000, config.SampleRate);
    }
}
