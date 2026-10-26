using SGrowing.Api.Modules.Voice;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Voice;

public class StreamingVoicePipelineTests
{
    private readonly BargeInInterruptionService _bargeInService;
    private readonly StreamingVoiceWebSocketHandler _handler;

    public StreamingVoicePipelineTests()
    {
        _bargeInService = new BargeInInterruptionService();
        _handler = new StreamingVoiceWebSocketHandler(_bargeInService);
    }

    [Fact]
    public void MuLawDecoding_AndRmsCalculation_DistinguishesSilenceFromSpeech()
    {
        // 0xFF in mu-law represents 0 amplitude (silence)
        byte[] silenceBytes = new byte[160];
        Array.Fill(silenceBytes, (byte)0xFF);

        // High amplitude square wave in mu-law (e.g. 0x80 or 0x00)
        byte[] speechBytes = new byte[160];
        for (int i = 0; i < speechBytes.Length; i++)
        {
            speechBytes[i] = (byte)(i % 2 == 0 ? 0x00 : 0x80);
        }

        var silenceRms = StreamingVoiceWebSocketHandler.CalculateMuLawRmsEnergy(silenceBytes);
        var speechRms = StreamingVoiceWebSocketHandler.CalculateMuLawRmsEnergy(speechBytes);

        Assert.True(silenceRms < 10.0, $"Silence RMS should be near 0, but was {silenceRms}");
        Assert.True(speechRms > 5000.0, $"Speech RMS should be high, but was {speechRms}");
    }

    [Fact]
    public void InboundChunk_WhenCallerSpeaksDuringAgentAudio_TriggersBargeInInterruption()
    {
        var tenantId = Guid.NewGuid();
        var session = StreamingVoiceCallSession.Create(tenantId, "CA12345678", "MZ87654321");
        
        // Agent is currently delivering speech
        session.SetAgentSpeaking(true);
        Assert.True(session.IsAgentSpeaking);

        // Generate high amplitude speech frame
        byte[] speechBytes = new byte[160];
        Array.Fill(speechBytes, (byte)0x00);
        var base64Speech = Convert.ToBase64String(speechBytes);

        var result = _handler.ProcessInboundAudioChunk(session, base64Speech, sequenceNumber: 1);

        Assert.Equal(VadStatus.BargeInInterrupted, result.Status);
        Assert.True(result.IsSpeech);
        Assert.False(session.IsAgentSpeaking); // Agent speaking should be canceled
        Assert.Equal(1, session.InterruptionCount);
        Assert.NotNull(result.ClearBufferCommand);
        Assert.Contains("clear", result.ClearBufferCommand);
        Assert.Contains("MZ87654321", result.ClearBufferCommand);
        Assert.True(result.LatencyMs < 400.0, $"Processing latency {result.LatencyMs}ms must be well below 400ms SLA");
    }

    [Fact]
    public void InboundChunk_WhenSilenceReceived_DoesNotInterruptAgent()
    {
        var tenantId = Guid.NewGuid();
        var session = StreamingVoiceCallSession.Create(tenantId, "CA111", "MZ222");
        session.SetAgentSpeaking(true);

        byte[] silenceBytes = new byte[160];
        Array.Fill(silenceBytes, (byte)0xFF);
        var base64Silence = Convert.ToBase64String(silenceBytes);

        var result = _handler.ProcessInboundAudioChunk(session, base64Silence, sequenceNumber: 1);

        Assert.Equal(VadStatus.Silence, result.Status);
        Assert.False(result.IsSpeech);
        Assert.True(session.IsAgentSpeaking); // Remains speaking
        Assert.Equal(0, session.InterruptionCount);
        Assert.Null(result.ClearBufferCommand);
    }

    [Fact]
    public void SpeculativeSentenceStreaming_SegmentsTokensImmediatelyAtSentenceBoundary()
    {
        string buffer = "";
        
        // Partial token arrives: "We provide"
        var sentences1 = StreamingVoiceWebSocketHandler.SegmentSpeculativeSentences(ref buffer, "We provide");
        Assert.Empty(sentences1);
        Assert.Equal("We provide", buffer);

        // More tokens arrive finishing the sentence: " enterprise CRM."
        var sentences2 = StreamingVoiceWebSocketHandler.SegmentSpeculativeSentences(ref buffer, " enterprise CRM.");
        Assert.Single(sentences2);
        Assert.Equal("We provide enterprise CRM.", sentences2[0]);
        Assert.Empty(buffer);

        // Multiple sentences in one chunk
        var sentences3 = StreamingVoiceWebSocketHandler.SegmentSpeculativeSentences(ref buffer, "How can I help you today? Would you like a demo?");
        Assert.Equal(2, sentences3.Count);
        Assert.Equal("How can I help you today?", sentences3[0]);
        Assert.Equal("Would you like a demo?", sentences3[1]);
        Assert.Empty(buffer);
    }

    [Fact]
    public void WarmSipTransfer_ProducesValidTwiMl_AndEnrichesContextHeaders()
    {
        var session = StreamingVoiceCallSession.Create(Guid.NewGuid(), "CA999", "MZ999");
        session.RecordInboundFrame(160, 8000, true, 1.2);
        session.SetAgentSpeaking(true);
        session.RecordInboundFrame(160, 8000, true, 1.2); // trigger interruption

        var result = _bargeInService.InitiateWarmSipTransfer(
            session,
            "sip:tier2-agent@sgrowing.voice.net",
            reason: "Complex billing dispute",
            conversationSummary: "Customer requested escalation for SLA refund.");

        Assert.True(result.Success);
        Assert.True(session.IsTransferred);
        Assert.Equal("sip:tier2-agent@sgrowing.voice.net", session.TransferTargetSipUri);
        Assert.Contains("<Dial>", result.TwiMlXml);
        Assert.Contains("<Sip>", result.TwiMlXml);
        Assert.Contains("X-Session-Id", result.TwiMlXml);
        Assert.Contains("X-Transfer-Reason", result.TwiMlXml);
        Assert.Equal("Complex billing dispute", result.ContextHeaders["X-Transfer-Reason"]);
        Assert.Equal("1", result.ContextHeaders["X-Interruption-Count"]);
    }
}
