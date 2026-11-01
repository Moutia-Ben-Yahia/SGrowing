using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SGrowing.Api.Modules.Voice.Streaming;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VoiceAiStreamingAndBargeInTests
{
    private readonly LowLatencyAudioStreamingEngine _streamingEngine;
    private readonly BargeInInterruptionManager _bargeInManager;
    private readonly NeuralAnsweringMachineDetector _amdDetector;
    private readonly LiveCallToolExecutor _toolExecutor;

    public VoiceAiStreamingAndBargeInTests()
    {
        _streamingEngine = new LowLatencyAudioStreamingEngine(NullLogger<LowLatencyAudioStreamingEngine>.Instance);
        _bargeInManager = new BargeInInterruptionManager(_streamingEngine, NullLogger<BargeInInterruptionManager>.Instance);
        _amdDetector = new NeuralAnsweringMachineDetector(NullLogger<NeuralAnsweringMachineDetector>.Instance);
        _toolExecutor = new LiveCallToolExecutor(NullLogger<LiveCallToolExecutor>.Instance);
    }

    #region 1. Low-Latency Audio Streaming Engine & Frame Timing Tests

    [Fact]
    public void AudioFrameConstants_ReturnsExpectedSizesAndSampleRates_ForSupportedFormats()
    {
        // 20ms frame constants
        AudioFrameConstants.DurationMsPerFrame.Should().Be(20);

        // PCM-16 16kHz mono: 16000 * 0.02 * 2 = 640 bytes
        AudioFrameConstants.GetExpectedFrameSize(AudioEncodingFormat.Pcm16_16kHz).Should().Be(640);
        AudioFrameConstants.GetSampleRate(AudioEncodingFormat.Pcm16_16kHz).Should().Be(16000);

        // G.711u 8kHz mono: 8000 * 0.02 * 1 = 160 bytes
        AudioFrameConstants.GetExpectedFrameSize(AudioEncodingFormat.G711u).Should().Be(160);
        AudioFrameConstants.GetSampleRate(AudioEncodingFormat.G711u).Should().Be(8000);

        // PCM-16 8kHz mono: 8000 * 0.02 * 2 = 320 bytes
        AudioFrameConstants.GetExpectedFrameSize(AudioEncodingFormat.Pcm16_8kHz).Should().Be(320);
        AudioFrameConstants.GetSampleRate(AudioEncodingFormat.Pcm16_8kHz).Should().Be(8000);
    }

    [Fact]
    public void AudioRingBuffer_WritesAndReadsSequentialData_PreservingByteIntegrity()
    {
        var ringBuffer = new AudioRingBuffer(capacityBytes: 1024);
        byte[] inputData = [10, 20, 30, 40, 50, 60, 70, 80];

        int written = ringBuffer.Write(inputData);
        written.Should().Be(inputData.Length);
        ringBuffer.AvailableBytes.Should().Be(inputData.Length);

        byte[] readBuffer = new byte[inputData.Length];
        int readCount = ringBuffer.Read(readBuffer);

        readCount.Should().Be(inputData.Length);
        readBuffer.Should().Equal(inputData);
        ringBuffer.AvailableBytes.Should().Be(0);
    }

    [Fact]
    public void AudioRingBuffer_HandlesCircularWrapAround_Correctly()
    {
        var ringBuffer = new AudioRingBuffer(capacityBytes: 100);

        // Fill 80 bytes
        byte[] chunk1 = new byte[80];
        Array.Fill(chunk1, (byte)1);
        ringBuffer.Write(chunk1);

        // Read 50 bytes (30 left)
        byte[] read1 = new byte[50];
        ringBuffer.Read(read1);
        ringBuffer.AvailableBytes.Should().Be(30);

        // Write 60 bytes (this wraps around past index 100)
        byte[] chunk2 = new byte[60];
        for (int i = 0; i < 60; i++) chunk2[i] = (byte)(i + 10);
        int written = ringBuffer.Write(chunk2);
        written.Should().Be(60);
        ringBuffer.AvailableBytes.Should().Be(90);

        // Read all 90 bytes
        byte[] readAll = new byte[90];
        int totalRead = ringBuffer.Read(readAll);

        totalRead.Should().Be(90);
        // First 30 should be 1s
        readAll.Take(30).Should().AllBeEquivalentTo((byte)1);
        // Next 60 should match chunk2
        readAll.Skip(30).Take(60).Should().Equal(chunk2);
    }

    [Fact]
    public void AudioRingBuffer_Clear_ResetsHeadTailAndAvailableCount()
    {
        var ringBuffer = new AudioRingBuffer(capacityBytes: 500);
        ringBuffer.Write(new byte[200]);
        ringBuffer.AvailableBytes.Should().Be(200);

        ringBuffer.Clear();

        ringBuffer.AvailableBytes.Should().Be(0);
        byte[] dest = new byte[50];
        ringBuffer.Read(dest).Should().Be(0);
    }

    [Fact]
    public void StreamingEngine_InboundFrameEnqueueDequeue_OperatesFIFO()
    {
        var sessionId = Guid.NewGuid();
        byte[] frame1 = new byte[640];
        frame1[0] = 0xAA;
        byte[] frame2 = new byte[640];
        frame2[0] = 0xBB;

        _streamingEngine.EnqueueInboundFrame(sessionId, frame1);
        _streamingEngine.EnqueueInboundFrame(sessionId, frame2);

        _streamingEngine.TryDequeueInboundFrame(sessionId, out var dequeued1).Should().BeTrue();
        dequeued1.Span[0].Should().Be(0xAA);

        _streamingEngine.TryDequeueInboundFrame(sessionId, out var dequeued2).Should().BeTrue();
        dequeued2.Span[0].Should().Be(0xBB);

        _streamingEngine.TryDequeueInboundFrame(sessionId, out _).Should().BeFalse();
    }

    [Fact]
    public void StreamingEngine_OutboundQueueBufferingAndFlushing_CalculatesSavedAudioMsAccurately()
    {
        var sessionId = Guid.NewGuid();
        var session = _streamingEngine.GetOrCreateSession(sessionId, AudioEncodingFormat.Pcm16_16kHz);

        // Enqueue 10 outbound chunks of 20ms (640 bytes each = 200ms total audio)
        for (int i = 0; i < 10; i++)
        {
            _streamingEngine.EnqueueOutboundChunk(sessionId, new byte[640]);
        }

        session.OutboundQueueLength.Should().Be(10);

        var (flushedChunks, savedAudioMs) = _streamingEngine.FlushOutboundQueue(sessionId);

        flushedChunks.Should().Be(10);
        savedAudioMs.Should().Be(200, "10 frames of 20ms equals exactly 200ms of saved audio output.");
        session.OutboundQueueLength.Should().Be(0);
    }

    [Fact]
    public void StreamingEngine_TurnaroundLatencyBreakdown_AccuratelyInstrumentsStagesAndGradesSub500ms()
    {
        var sessionId = Guid.NewGuid();
        var turnId = Guid.NewGuid();
        var userSpeechEnded = DateTime.UtcNow;
        var vadDetected = userSpeechEnded.AddMilliseconds(35);
        var llmToken = vadDetected.AddMilliseconds(195);
        var ttsFirstByte = llmToken.AddMilliseconds(90);

        var breakdown = _streamingEngine.RecordTurnaroundTurn(
            sessionId,
            turnId,
            userSpeechEnded,
            vadDetected,
            llmToken,
            ttsFirstByte);

        breakdown.TurnId.Should().Be(turnId);
        breakdown.VadLatencyMs.Should().BeApproximately(35.0, 1.0);
        breakdown.LlmLatencyMs.Should().BeApproximately(195.0, 1.0);
        breakdown.TtsLatencyMs.Should().BeApproximately(90.0, 1.0);
        breakdown.TotalTurnaroundMs.Should().BeApproximately(320.0, 2.0);
        breakdown.IsSub500MsCompliant.Should().BeTrue("320ms turnaround is well under 500ms SLA.");
        breakdown.LatencyGrade.Should().Be("Sub500msCompliant");
    }

    [Fact]
    public void StreamingEngine_TurnaroundLatencyBreakdown_MarksHighLatency_WhenTurnExceedsThreshold()
    {
        var sessionId = Guid.NewGuid();
        var userSpeechEnded = DateTime.UtcNow;
        var vadDetected = userSpeechEnded.AddMilliseconds(120);
        var llmToken = vadDetected.AddMilliseconds(450);
        var ttsFirstByte = llmToken.AddMilliseconds(250); // Total = 820ms

        var breakdown = _streamingEngine.RecordTurnaroundTurn(
            sessionId,
            Guid.NewGuid(),
            userSpeechEnded,
            vadDetected,
            llmToken,
            ttsFirstByte);

        breakdown.TotalTurnaroundMs.Should().BeApproximately(820.0, 2.0);
        breakdown.IsSub500MsCompliant.Should().BeFalse();
        breakdown.LatencyGrade.Should().Be("HighLatency");
    }

    [Fact]
    public void StreamingEngine_Transcoding_ConvertsG711uToPcm16_AndPcm16ToG711u()
    {
        // 0xFF in mu-law represents digital silence (sample 0)
        byte muSilence = 0xFF;
        short pcmZero = LowLatencyAudioStreamingEngine.MuLawToPcm16Sample(muSilence);
        pcmZero.Should().Be(0);

        // Convert PCM 0 back to mu-law
        byte roundTripMu = LowLatencyAudioStreamingEngine.Pcm16ToMuLawSample(0);
        roundTripMu.Should().Be(0xFF);

        // Test array transcoding
        byte[] g711uFrame = new byte[160];
        Array.Fill(g711uFrame, (byte)0xFF);

        byte[] pcm16Out = _streamingEngine.Transcode(g711uFrame, AudioEncodingFormat.G711u, AudioEncodingFormat.Pcm16_8kHz);
        pcm16Out.Length.Should().Be(320); // 160 samples * 2 bytes

        byte[] backToG711u = _streamingEngine.Transcode(pcm16Out, AudioEncodingFormat.Pcm16_8kHz, AudioEncodingFormat.G711u);
        backToG711u.Length.Should().Be(160);
        backToG711u[0].Should().Be(0xFF);
    }

    [Fact]
    public void StreamingEngine_CalculateRmsEnergy_DistinguishesSilenceFromAudibleSignal()
    {
        // 16kHz PCM-16 silence
        byte[] silencePcm = new byte[640];
        double silenceRms = _streamingEngine.CalculateRmsEnergy(silencePcm, AudioEncodingFormat.Pcm16_16kHz);
        silenceRms.Should().Be(0.0);

        // 16kHz PCM-16 high-amplitude square wave (+12000, -12000)
        byte[] speechPcm = new byte[640];
        for (int i = 0; i < 320; i++)
        {
            short val = (short)(i % 2 == 0 ? 12000 : -12000);
            BitConverter.TryWriteBytes(speechPcm.AsSpan(i * 2, 2), val);
        }

        double speechRms = _streamingEngine.CalculateRmsEnergy(speechPcm, AudioEncodingFormat.Pcm16_16kHz);
        speechRms.Should().BeApproximately(12000.0, 10.0);
    }

    #endregion

    #region 2. Barge-In Interruption Manager Tests

    [Fact]
    public void BargeIn_DoesNotTrigger_WhenAcousticEnergyBelowThreshold()
    {
        var sessionId = Guid.NewGuid();
        _bargeInManager.SetSessionState(sessionId, StreamingPipelineState.AgentSpeaking);

        // RMS 200 is below default threshold 400
        var result = _bargeInManager.ProcessIncomingAudioEnergy(sessionId, rmsEnergy: 200.0, frameDurationMs: 20);

        result.InterruptionTriggered.Should().BeFalse();
        result.PreviousState.Should().Be(StreamingPipelineState.AgentSpeaking);
        result.NewState.Should().Be(StreamingPipelineState.AgentSpeaking);
        _bargeInManager.GetSessionState(sessionId).Should().Be(StreamingPipelineState.AgentSpeaking);
    }

    [Fact]
    public void BargeIn_DoesNotTrigger_WhenAgentIsNotSpeaking()
    {
        var sessionId = Guid.NewGuid();
        // Set state to Listening instead of AgentSpeaking
        _bargeInManager.SetSessionState(sessionId, StreamingPipelineState.Listening);

        // Even with high energy (1500), barge-in monitor should be inactive
        var result = _bargeInManager.ProcessIncomingAudioEnergy(sessionId, rmsEnergy: 1500.0, frameDurationMs: 20);

        result.InterruptionTriggered.Should().BeFalse();
        result.NewState.Should().Be(StreamingPipelineState.Listening);
    }

    [Fact]
    public void BargeIn_TriggersInterruption_WhenSpeechEnergyExceeds140msSustained()
    {
        var sessionId = Guid.NewGuid();
        _bargeInManager.SetSessionState(sessionId, StreamingPipelineState.AgentSpeaking);

        // Send 6 frames of 20ms = 120ms (below 140ms threshold)
        for (int i = 1; i <= 6; i++)
        {
            var intermediate = _bargeInManager.ProcessIncomingAudioEnergy(sessionId, rmsEnergy: 850.0, frameDurationMs: 20);
            intermediate.InterruptionTriggered.Should().BeFalse($"Frame {i} (120ms total) should not yet trigger barge-in.");
            intermediate.ContinuousSpeechDurationMs.Should().Be(i * 20);
        }

        // 7th frame = 140ms -> Meets threshold!
        var triggered = _bargeInManager.ProcessIncomingAudioEnergy(sessionId, rmsEnergy: 850.0, frameDurationMs: 20);

        triggered.InterruptionTriggered.Should().BeTrue("140ms of continuous high speech energy exceeds threshold.");
        triggered.PreviousState.Should().Be(StreamingPipelineState.AgentSpeaking);
        triggered.NewState.Should().Be(StreamingPipelineState.Listening);
        _bargeInManager.GetSessionState(sessionId).Should().Be(StreamingPipelineState.Listening);
    }

    [Fact]
    public void BargeIn_FlushesDownstreamOutboundTtsBuffer_OnInterruption()
    {
        var sessionId = Guid.NewGuid();
        // Buffer 8 outbound chunks (160ms)
        for (int i = 0; i < 8; i++)
        {
            _streamingEngine.EnqueueOutboundChunk(sessionId, new byte[640]);
        }

        _bargeInManager.SetSessionState(sessionId, StreamingPipelineState.AgentSpeaking);

        // Feed 7 frames of high speech
        for (int i = 0; i < 6; i++)
        {
            _bargeInManager.ProcessIncomingAudioEnergy(sessionId, 900.0, 20);
        }
        var finalResult = _bargeInManager.ProcessIncomingAudioEnergy(sessionId, 900.0, 20);

        finalResult.InterruptionTriggered.Should().BeTrue();
        finalResult.FlushedChunksCount.Should().Be(8);
        finalResult.SavedAudioMs.Should().Be(160, "8 frames of 20ms TTS audio was truncated before reaching phone speaker.");

        // Downstream outbound queue should be zeroed
        _streamingEngine.TryDequeueOutboundChunk(sessionId, out _).Should().BeFalse();
    }

    [Fact]
    public void BargeIn_EmitsBargeInDetectedEvent_WithAccurateTelemetry()
    {
        var sessionId = Guid.NewGuid();
        _bargeInManager.SetSessionState(sessionId, StreamingPipelineState.AgentSpeaking);

        BargeInDetectedEventArgs? capturedEvent = null;
        _bargeInManager.BargeInDetected += (sender, args) => capturedEvent = args;

        // Feed 7 frames of high energy
        for (int i = 0; i < 7; i++)
        {
            _bargeInManager.ProcessIncomingAudioEnergy(sessionId, 1200.0, 20);
        }

        capturedEvent.Should().NotBeNull();
        capturedEvent!.SessionId.Should().Be(sessionId);
        capturedEvent.TriggeringRmsEnergy.Should().Be(1200.0);
        capturedEvent.Reason.Should().Be("AcousticEnergyThresholdSurpassed");

        var history = _bargeInManager.GetTelemetryHistory(sessionId);
        history.Should().HaveCount(1);
        history[0].ContinuousSpeechDurationMs.Should().Be(140.0);
    }

    [Fact]
    public void BargeIn_ExplicitTrigger_ImmediatelyFlushesAndTransitionsState()
    {
        var sessionId = Guid.NewGuid();
        _streamingEngine.EnqueueOutboundChunk(sessionId, new byte[640]);
        _bargeInManager.SetSessionState(sessionId, StreamingPipelineState.AgentSpeaking);

        var result = _bargeInManager.TriggerExplicitBargeIn(sessionId, "UserTappedMuteOrInterrupted");

        result.InterruptionTriggered.Should().BeTrue();
        result.NewState.Should().Be(StreamingPipelineState.Listening);
        result.FlushedChunksCount.Should().Be(1);
        result.SavedAudioMs.Should().Be(20);

        var history = _bargeInManager.GetTelemetryHistory(sessionId);
        history.Should().ContainSingle(t => t.Reason == "UserTappedMuteOrInterrupted");
    }

    #endregion

    #region 3. Neural Answering Machine Detector (AMD) Tests

    [Fact]
    public void NeuralAmd_GoertzelAlgorithm_DetectsPure1000HzVoicemailBeepTone()
    {
        // Synthesize 1000Hz pure sine wave (voicemail beep) at 16kHz sample rate
        int sampleRate = 16000;
        int numSamples = 1600; // 100ms
        short[] beepAudio = new short[numSamples];
        double targetFreq = 1000.0;

        for (int i = 0; i < numSamples; i++)
        {
            double angle = 2.0 * Math.PI * targetFreq * i / sampleRate;
            beepAudio[i] = (short)(Math.Sin(angle) * 15000.0);
        }

        bool detected = _amdDetector.DetectToneFrequency(beepAudio, sampleRate, out double detectedFreq, out double strength);

        detected.Should().BeTrue();
        detectedFreq.Should().Be(1000.0);
        strength.Should().BeGreaterThan(0.40);
    }

    [Fact]
    public void NeuralAmd_GoertzelAlgorithm_Detects440HzDialTone()
    {
        // Synthesize 440Hz dial tone at 16kHz
        int sampleRate = 16000;
        int numSamples = 1600;
        short[] tone440 = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double angle = 2.0 * Math.PI * 440.0 * i / sampleRate;
            tone440[i] = (short)(Math.Sin(angle) * 14000.0);
        }

        bool detected = _amdDetector.DetectToneFrequency(tone440, sampleRate, out double detectedFreq, out double strength);

        detected.Should().BeTrue();
        detectedFreq.Should().Be(440.0);
        strength.Should().BeGreaterThan(0.40);
    }

    [Fact]
    public void NeuralAmd_GoertzelAlgorithm_RejectsBroadbandNoiseAndSilence()
    {
        int sampleRate = 16000;
        short[] silence = new short[1600];

        bool silenceDetected = _amdDetector.DetectToneFrequency(silence, sampleRate, out _, out _);
        silenceDetected.Should().BeFalse();

        // White noise (pseudo-random broadband distribution)
        short[] noise = new short[1600];
        var rand = new Random(42);
        for (int i = 0; i < noise.Length; i++)
        {
            noise[i] = (short)rand.Next(-5000, 5000);
        }

        bool noiseDetected = _amdDetector.DetectToneFrequency(noise, sampleRate, out _, out _);
        noiseDetected.Should().BeFalse("Broadband noise should not trigger pure single-frequency Goertzel detection.");
    }

    [Fact]
    public void NeuralAmd_TemporalAnalysis_ClassifiesLongMonologueGreetingAsMachine_AndDropsVoicemail()
    {
        var sessionId = Guid.NewGuid();

        var decision = _amdDetector.EvaluatePcmSamples(
            sessionId: sessionId,
            samples: [],
            sampleRate: 16000,
            elapsedCallTimeMs: 4000.0,
            continuousSpeechMs: 3800.0, // > 3.5s threshold
            trailingSilenceMs: 0.0,
            customerName: "Alex Rivera",
            orderNumber: "SG-9002"
        );

        decision.Classification.Should().Be(AmdClassification.Machine);
        decision.Mode.Should().Be(AmdDetectionMode.TemporalSpeechAnalysis);
        decision.Action.Should().Be(AmdAction.DropVoicemailAndTerminate);
        decision.ShouldTerminateCall.Should().BeTrue();
        decision.VoicemailTemplateDropped.Should().Contain("Alex Rivera");
        decision.VoicemailTemplateDropped.Should().Contain("SG-9002");
    }

    [Fact]
    public void NeuralAmd_TemporalAnalysis_ClassifiesShortPhraseWithPauseAsHuman_AndTriggersGreeting()
    {
        var sessionId = Guid.NewGuid();

        // Live human says "Hello? Who is this?" (750ms) followed by conversational silence (800ms)
        var decision = _amdDetector.EvaluatePcmSamples(
            sessionId: sessionId,
            samples: [],
            sampleRate: 16000,
            elapsedCallTimeMs: 1600.0,
            continuousSpeechMs: 750.0,
            trailingSilenceMs: 800.0,
            customerName: "Maria Santos",
            orderNumber: "SG-3310"
        );

        decision.Classification.Should().Be(AmdClassification.Human);
        decision.Mode.Should().Be(AmdDetectionMode.TemporalSpeechAnalysis);
        decision.Action.Should().Be(AmdAction.TriggerConversationalGreeting);
        decision.ShouldTerminateCall.Should().BeFalse();
        decision.SpokenPrompt.Should().Contain("Maria Santos");
        decision.SpokenPrompt.Should().Contain("Sarah from SGrowing");
    }

    [Fact]
    public void NeuralAmd_EvaluateAudioBuffer_HandlesBase64BeepToneAccurately()
    {
        var sessionId = Guid.NewGuid();

        // Synthesize 1000Hz beep and convert to base64 PCM-16
        int sampleRate = 16000;
        int numSamples = 800; // 50ms
        byte[] pcmBytes = new byte[numSamples * 2];
        for (int i = 0; i < numSamples; i++)
        {
            double angle = 2.0 * Math.PI * 1000.0 * i / sampleRate;
            short val = (short)(Math.Sin(angle) * 16000.0);
            BitConverter.TryWriteBytes(pcmBytes.AsSpan(i * 2, 2), val);
        }
        string base64Audio = Convert.ToBase64String(pcmBytes);

        var request = new AmdEvaluationRequest(
            SessionId: sessionId,
            AudioBase64: base64Audio,
            Encoding: AudioEncodingFormat.Pcm16_16kHz,
            ElapsedCallTimeMs: 900.0,
            ContinuousSpeechMs: 0.0,
            TrailingSilenceMs: 0.0,
            CustomerName: "John Doe",
            OrderNumber: "SG-5501"
        );

        var decision = _amdDetector.EvaluateAudioBuffer(request);

        decision.Classification.Should().Be(AmdClassification.BeepToneDetected);
        decision.DetectedToneHz.Should().Be(1000.0);
        decision.Action.Should().Be(AmdAction.DropVoicemailAndTerminate);
        decision.ShouldTerminateCall.Should().BeTrue();
    }

    #endregion

    #region 4. Live In-Call Tool Execution Tests

    [Fact]
    public async Task LiveCallToolExecutor_GetOrderStatus_ReturnsVoiceOptimizedSummaryAndStructuredDetails()
    {
        var result = await _toolExecutor.GetOrderStatusAsync("SG-1084");

        result.Success.Should().BeTrue();
        result.ToolName.Should().Be("GetOrderStatus");
        result.VoiceSpokenSummary.Should().Contain("S G 1 0 8 4", "Order numbers should have spaced phonetic pronunciation for neural TTS.");
        result.VoiceSpokenSummary.Should().Contain("out for delivery");
        result.VoiceSpokenSummary.Should().NotContain("$", "Currencies should be spelled out as words for voice synthesis.");

        result.StructuredResult["orderCode"].Should().Be("SG-1084");
        result.StructuredResult["status"].Should().Be("OutForDelivery");
    }

    [Fact]
    public async Task LiveCallToolExecutor_RescheduleDelivery_FormatsConversationalDateAndTimeSlot()
    {
        var targetDate = new DateOnly(2026, 11, 4); // Wednesday
        var result = await _toolExecutor.RescheduleDeliveryAsync("SG-1084", targetDate, "Afternoon (2 PM - 5 PM)");

        result.Success.Should().BeTrue();
        result.ToolName.Should().Be("RescheduleDelivery");
        result.VoiceSpokenSummary.Should().Contain("Wednesday, November 4");
        result.VoiceSpokenSummary.Should().Contain("afternoon window");

        result.StructuredResult["rescheduledDate"].Should().Be("2026-11-04");
    }

    [Fact]
    public async Task LiveCallToolExecutor_ConfirmCodOrder_FormatsConfirmationAndCancellationAccurately()
    {
        // Test confirmed order
        var confirmedResult = await _toolExecutor.ConfirmCodOrderAsync("SG-1084", confirmed: true);
        confirmedResult.Success.Should().BeTrue();
        confirmedResult.VoiceSpokenSummary.Should().Contain("confirmed");
        confirmedResult.VoiceSpokenSummary.Should().Contain("priority dispatch");
        confirmedResult.StructuredResult["outcomeStatus"].Should().Be("Confirmed");

        // Test cancelled order
        var cancelledResult = await _toolExecutor.ConfirmCodOrderAsync("SG-1084", confirmed: false);
        cancelledResult.Success.Should().BeTrue();
        cancelledResult.VoiceSpokenSummary.Should().Contain("cancellation request");
        cancelledResult.VoiceSpokenSummary.Should().Contain("cancelled and no package will be delivered");
        cancelledResult.StructuredResult["outcomeStatus"].Should().Be("CancelledByCustomer");
    }

    [Fact]
    public async Task LiveCallToolExecutor_EscalateToHumanAgent_GeneratesWarmTransferSpeechAndDeskDetails()
    {
        var result = await _toolExecutor.EscalateToHumanAgentAsync(
            reason: "Customer requested complex CPQ bulk discount",
            targetDesk: "Enterprise Sales Escalation Desk");

        result.Success.Should().BeTrue();
        result.ToolName.Should().Be("EscalateToHumanAgent");
        result.VoiceSpokenSummary.Should().Contain("Enterprise Sales Escalation Desk");
        result.VoiceSpokenSummary.Should().Contain("transferring you immediately");
        result.StructuredResult["status"].Should().Be("WarmTransferInitiated");
    }

    [Fact]
    public void LiveCallToolExecutor_FormatVoiceOptimizedSpeech_NormalizesCurrenciesAcronymsAndMarkup()
    {
        string rawInput = "Your **COD** package total is $34.50. ETA is approx. 3 PM.";
        string formatted = _toolExecutor.FormatVoiceOptimizedSpeech(rawInput);

        formatted.Should().NotContain("**");
        formatted.Should().NotContain("$");
        formatted.Should().Contain("cash on delivery");
        formatted.Should().Contain("34 dollars and 50 cents");
        formatted.Should().Contain("estimated arrival time");
        formatted.Should().Contain("approximately");
    }

    [Fact]
    public async Task LiveCallToolExecutor_ExecuteToolAsync_DispatchesDynamicallyByToolName()
    {
        var args = new Dictionary<string, object?>
        {
            ["orderCode"] = "SG-2048",
            ["confirmed"] = true
        };

        var result = await _toolExecutor.ExecuteToolAsync("confirm_cod_order", args);

        result.Success.Should().BeTrue();
        result.ToolName.Should().Be("ConfirmCodOrder");
        result.VoiceSpokenSummary.Should().Contain("S G 2 0 4 8");
        result.StructuredResult["isConfirmed"].Should().Be(true);
    }

    [Fact]
    public async Task LiveCallToolExecutor_ExecuteToolAsync_HandlesUnregisteredToolGracefully()
    {
        var result = await _toolExecutor.ExecuteToolAsync("non_existent_inventory_query", null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not registered");
        result.VoiceSpokenSummary.Should().Contain("follow up with you directly");
    }

    #endregion
}
