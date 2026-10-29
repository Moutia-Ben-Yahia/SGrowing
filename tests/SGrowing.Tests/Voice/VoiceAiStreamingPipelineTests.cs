using FluentAssertions;
using SGrowing.Api.Modules.Voice;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VoiceAiStreamingPipelineTests
{
    private readonly VoiceAiStreamService _service;
    private readonly BargeInInterruptionService _bargeInService;

    public VoiceAiStreamingPipelineTests()
    {
        _bargeInService = new BargeInInterruptionService();
        _service = new VoiceAiStreamService(_bargeInService);
    }

    [Fact]
    public void AudioChunkIngestion_AndRmsCalculation_DistinguishesSilenceFromSpeech_ForMuLaw()
    {
        // 0xFF in mu-law represents 0 amplitude (silence)
        byte[] silenceMuLaw = new byte[160];
        Array.Fill(silenceMuLaw, (byte)0xFF);

        // Square wave alternating 0x00 and 0x80 in mu-law represents maximum amplitude speech
        byte[] speechMuLaw = new byte[160];
        for (int i = 0; i < speechMuLaw.Length; i++)
        {
            speechMuLaw[i] = (byte)(i % 2 == 0 ? 0x00 : 0x80);
        }

        var silenceRms = VoiceAiStreamService.CalculateMuLawRmsEnergy(silenceMuLaw);
        var speechRms = VoiceAiStreamService.CalculateMuLawRmsEnergy(speechMuLaw);

        silenceRms.Should().BeLessThan(10.0, "Mu-law 0xFF frames should have near-zero RMS energy.");
        speechRms.Should().BeGreaterThan(5000.0, "High-amplitude mu-law frames should have substantial RMS energy.");

        // Ingestion check
        var tracker = new VoiceVadSessionTracker { StreamSid = "MZ_TEST_1" };
        var ingestSilence = _service.IngestAudioFrame(silenceMuLaw, "mulaw", tracker);
        ingestSilence.IsSpeech.Should().BeFalse();
        ingestSilence.VadStatus.Should().Be(VoiceStreamVadStatus.Silence);

        var ingestSpeech = _service.IngestAudioFrame(speechMuLaw, "mulaw", tracker);
        ingestSpeech.IsSpeech.Should().BeTrue();
        ingestSpeech.VadStatus.Should().Be(VoiceStreamVadStatus.SpeechOngoing);
    }

    [Fact]
    public void AudioChunkIngestion_AndRmsCalculation_DistinguishesSilenceFromSpeech_ForPcm16()
    {
        // 16-bit linear PCM: 0 is silence
        byte[] silencePcm = new byte[320]; // 160 16-bit samples
        Array.Fill(silencePcm, (byte)0x00);

        // 16-bit linear PCM high-amplitude sine/square wave (e.g. 15000)
        byte[] speechPcm = new byte[320];
        short sampleVal = 15000;
        byte low = (byte)(sampleVal & 0xFF);
        byte high = (byte)((sampleVal >> 8) & 0xFF);
        for (int i = 0; i < speechPcm.Length; i += 2)
        {
            speechPcm[i] = low;
            speechPcm[i + 1] = high;
        }

        var silenceRms = VoiceAiStreamService.CalculatePcm16RmsEnergy(silencePcm);
        var speechRms = VoiceAiStreamService.CalculatePcm16RmsEnergy(speechPcm);

        silenceRms.Should().Be(0.0);
        speechRms.Should().BeApproximately(15000.0, 50.0);

        var tracker = new VoiceVadSessionTracker { StreamSid = "MZ_PCM_TEST" };
        var result = _service.IngestAudioFrame(speechPcm, "pcm16", tracker);
        result.IsSpeech.Should().BeTrue();
        result.VadStatus.Should().Be(VoiceStreamVadStatus.SpeechOngoing);
    }

    [Fact]
    public void VadStateTransitions_FromOngoingToSpeechEnded_WhenHangoverSilenceExceeded()
    {
        var tracker = new VoiceVadSessionTracker { StreamSid = "MZ_HANGOVER" };

        // Generate speech frame
        byte[] speechMuLaw = new byte[160];
        Array.Fill(speechMuLaw, (byte)0x00);

        // Frame 1: Caller starts speaking
        var f1 = _service.IngestAudioFrame(speechMuLaw, "mulaw", tracker);
        f1.VadStatus.Should().Be(VoiceStreamVadStatus.SpeechOngoing);

        // Silence frames
        byte[] silenceMuLaw = new byte[160];
        Array.Fill(silenceMuLaw, (byte)0xFF);

        // Frame 2: Silence 1 (below hangover threshold of 3)
        var f2 = _service.IngestAudioFrame(silenceMuLaw, "mulaw", tracker, hangoverSilenceFrames: 3);
        f2.VadStatus.Should().Be(VoiceStreamVadStatus.SpeechOngoing, "Brief pause should not end turn prematurely.");

        // Frame 3: Silence 2
        var f3 = _service.IngestAudioFrame(silenceMuLaw, "mulaw", tracker, hangoverSilenceFrames: 3);
        f3.VadStatus.Should().Be(VoiceStreamVadStatus.SpeechOngoing);

        // Frame 4: Silence 3 reaches hangover threshold -> SpeechEnded!
        var f4 = _service.IngestAudioFrame(silenceMuLaw, "mulaw", tracker, hangoverSilenceFrames: 3);
        f4.VadStatus.Should().Be(VoiceStreamVadStatus.SpeechEnded, "Consecutive silence exceeding hangover frames indicates finished speech.");
    }

    [Fact]
    public void BargeInDetection_WhenCallerSpeaksWhileAgentTalking_TriggersBufferClear()
    {
        var tracker = new VoiceVadSessionTracker
        {
            SessionId = Guid.NewGuid(),
            StreamSid = "MZ_BARGE_STREAM_99",
            IsAgentSpeaking = true // Agent is actively speaking
        };

        // Inbound speech audio from caller
        byte[] speechBytes = new byte[160];
        Array.Fill(speechBytes, (byte)0x00); // Loud mu-law frame

        var result = _service.IngestAudioFrame(speechBytes, "mulaw", tracker);

        result.VadStatus.Should().Be(VoiceStreamVadStatus.BargeInInterrupted);
        result.IsSpeech.Should().BeTrue();
        tracker.IsAgentSpeaking.Should().BeFalse("Agent speaking must be immediately cancelled upon barge-in.");
        tracker.InterruptionCount.Should().Be(1);
        result.BufferClearCommand.Should().NotBeNull();
        result.BufferClearCommand.Should().Contain("clear");
        result.BufferClearCommand.Should().Contain("MZ_BARGE_STREAM_99");
    }

    [Fact]
    public void LiveSentimentScoring_CalculatesAccuratePolarityRange()
    {
        // 1. Strongly positive speech
        string positiveSpeech = "I love this product, it's fantastic! The features sound amazing and we are ready to buy.";
        double positiveScore = _service.CalculateSentimentScore(positiveSpeech);
        positiveScore.Should().BeGreaterThan(0.6, "Enthusiastic buyer language should yield high positive sentiment score.");
        positiveScore.Should().BeLessThanOrEqualTo(1.0);

        // 2. Strongly negative speech
        string negativeSpeech = "This is a terrible experience, totally broken and ridiculous. Cancel my account immediately, what a waste of money.";
        double negativeScore = _service.CalculateSentimentScore(negativeSpeech);
        negativeScore.Should().BeLessThan(-0.5, "Angry complaint language should yield negative sentiment score.");
        negativeScore.Should().BeGreaterThanOrEqualTo(-1.0);

        // 3. Neutral speech
        string neutralSpeech = "Hello, I am calling to inquire about how the platform functions.";
        double neutralScore = _service.CalculateSentimentScore(neutralSpeech);
        neutralScore.Should().BeInRange(-0.25, 0.35, "Standard informational statements should hover near neutral.");
    }

    [Fact]
    public void BuyingKeywordExtraction_ExtractsRelevantEnterpriseSignals()
    {
        // Pricing Inquiry
        var signals1 = _service.ExtractBuyingSignals("Could you send me your pricing tiers and how much it costs per user?");
        signals1.Should().Contain("PricingInquiry");

        // Contract Request
        var signals2 = _service.ExtractBuyingSignals("We have reviewed the proposal, please send over the contract and master services agreement.");
        signals2.Should().Contain("ContractRequest");

        // Competitor Objection
        var signals3 = _service.ExtractBuyingSignals("We are currently using HubSpot and Salesforce. Why should we switch to SGrowing?");
        signals3.Should().Contain("CompetitorObjection");

        // Demo Booking
        var signals4 = _service.ExtractBuyingSignals("Can we schedule a 15-minute demo on Zoom for Thursday morning?");
        signals4.Should().Contain("DemoBooking");

        // Urgency and Budget Approved
        var signals5 = _service.ExtractBuyingSignals("Our budget is approved and we need to deploy asap this week.");
        signals5.Should().Contain("BudgetApproved");
        signals5.Should().Contain("UrgencySignal");

        // Decision maker identified
        var signals6 = _service.ExtractBuyingSignals("I am the VP of Sales and I make the final software decision.");
        signals6.Should().Contain("DecisionMakerIdentified");
    }

    [Fact]
    public async Task ConversationalLoop_ExecutesUnderSub600msSla_WithReasoningTokensAndTts()
    {
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tracker = new VoiceVadSessionTracker
        {
            SessionId = sessionId,
            StreamSid = "MZ_LOOP_TEST"
        };

        // Inbound speech text indicating pricing inquiry
        string callerSpeech = "What are your pricing plans and does it include automated dialing?";

        var turnResult = await _service.ExecuteConversationalTurnAsync(
            tenantId,
            sessionId,
            "MZ_LOOP_TEST",
            inboundAudioBytes: null,
            audioEncoding: "mulaw",
            callerTranscriptText: callerSpeech,
            tracker: tracker);

        turnResult.TotalLatencyMs.Should().BeLessThan(600.0, "Conversational loop must execute well within the 600ms sub-second SLA.");
        turnResult.SentimentScore.Should().BeInRange(-1.0, 1.0);
        turnResult.BuyingSignals.Should().Contain("PricingInquiry");
        turnResult.AgentResponse.Should().NotBeNullOrWhiteSpace();
        turnResult.AgentResponse.Should().Contain("forty-nine dollars");
        turnResult.ResponseAudioChunks.Should().NotBeEmpty("Neural TTS should produce synthesized audio chunks.");
        turnResult.IsBargeInInterrupted.Should().BeFalse();
    }

    [Fact]
    public async Task ConversationalLoop_WhenBargeInOccurs_ClearsBufferAndSilencesTts()
    {
        var tenantId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tracker = new VoiceVadSessionTracker
        {
            SessionId = sessionId,
            StreamSid = "MZ_BARGE_LOOP",
            IsAgentSpeaking = true // Agent was speaking when chunk arrived
        };

        // Loud mu-law speech chunk arriving from caller
        byte[] loudSpeech = new byte[160];
        Array.Fill(loudSpeech, (byte)0x00);

        var turnResult = await _service.ExecuteConversationalTurnAsync(
            tenantId,
            sessionId,
            "MZ_BARGE_LOOP",
            inboundAudioBytes: loudSpeech,
            audioEncoding: "mulaw",
            callerTranscriptText: "Wait, stop, I need to ask something else!",
            tracker: tracker);

        turnResult.IsBargeInInterrupted.Should().BeTrue();
        turnResult.ClearBufferCommand.Should().NotBeNull();
        turnResult.ClearBufferCommand.Should().Contain("clear");
        turnResult.ResponseAudioChunks.Should().BeEmpty("When barge-in occurs, pending TTS audio must be silenced immediately.");
    }
}
