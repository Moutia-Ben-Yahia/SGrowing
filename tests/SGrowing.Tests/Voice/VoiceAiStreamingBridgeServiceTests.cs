using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SGrowing.Api.Modules.Voice.Streaming;
using Xunit;

namespace SGrowing.Tests.Voice;

public class VoiceAiStreamingBridgeServiceTests
{
    private readonly VoiceAiStreamingBridgeService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public VoiceAiStreamingBridgeServiceTests()
    {
        _service = new VoiceAiStreamingBridgeService();
    }

    private byte[] GenerateSyntheticPcmChunk(short amplitude = 0, int sampleCount = 320)
    {
        // 20ms of 16kHz mono 16-bit PCM = 320 samples = 640 bytes
        var bytes = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            var sampleBytes = BitConverter.GetBytes(amplitude);
            bytes[i * 2] = sampleBytes[0];
            bytes[i * 2 + 1] = sampleBytes[1];
        }
        return bytes;
    }

    [Fact]
    public async Task InitiateSession_CreatesValidSession_InInitiatedState()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(
            TenantId: _tenantId,
            CallerNumber: "+1 (415) 123-4567",
            CalleeNumber: "+1 (800) 999-0001",
            VoiceProfileId: "sophia-enterprise"
        ));

        session.Should().NotBeNull();
        session.Id.Should().NotBeEmpty();
        session.TenantId.Should().Be(_tenantId);
        session.Lifecycle.Should().Be(TelephonyCallLifecycle.Initiated);
        session.PipelineState.Should().Be(StreamingPipelineState.Idle);
        session.CallerNumber.Should().Be("+1 (415) 123-4567");
        session.CalleeNumber.Should().Be("+1 (800) 999-0001");
        session.CallSid.Should().StartWith("CA_STREAM_");
    }

    [Fact]
    public async Task InitiateSession_UsesDefaultVoiceProfile_WhenNoneSpecified()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(
            TenantId: _tenantId,
            VoiceProfileId: null
        ));

        session.VoiceProfile.Should().NotBeNull();
        session.VoiceProfile.Id.Should().Be("sophia-enterprise");
        session.VoiceProfile.BargeInEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task InitiateSession_UsesSpecifiedVoiceProfile_WhenValid()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(
            TenantId: _tenantId,
            VoiceProfileId: "alex-technical"
        ));

        session.VoiceProfile.Id.Should().Be("alex-technical");
        session.VoiceProfile.Name.Should().Contain("Alex");
        session.VoiceProfile.BaseLatencyMs.Should().Be(130.0);
    }

    [Fact]
    public async Task InitiateSession_GeneratesUniqueCallSid_ForMultipleInvocations()
    {
        var s1 = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        var s2 = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        s1.CallSid.Should().NotBe(s2.CallSid);
        s1.Id.Should().NotBe(s2.Id);
    }

    [Fact]
    public async Task IngestAudioFrame_SilentFrame_RemainsInListeningState()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        var silentPcm = GenerateSyntheticPcmChunk(amplitude: 10);
        string base64 = Convert.ToBase64String(silentPcm);

        var result = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(
            SequenceNumber: 1,
            PcmBase64: base64
        ));

        result.IsSpeech.Should().BeFalse();
        result.SpeechOnsetDetected.Should().BeFalse();
        result.BargeInTriggered.Should().BeFalse();
        result.CurrentState.Should().Be(StreamingPipelineState.Listening);
        result.CallLifecycle.Should().Be(TelephonyCallLifecycle.ActiveAudio);
    }

    [Fact]
    public async Task IngestAudioFrame_CalculatesRmsEnergyAndDb_FromValidPcmPayload()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        var speechPcm = GenerateSyntheticPcmChunk(amplitude: 3000); // strong speech sample
        string base64 = Convert.ToBase64String(speechPcm);

        var result = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(
            SequenceNumber: 1,
            PcmBase64: base64
        ));

        result.RmsEnergy.Should().BeGreaterThan(2500);
        result.DecibelsDb.Should().BeGreaterThan(-30.0);
        result.IsSpeech.Should().BeTrue();
    }

    [Fact]
    public async Task IngestAudioFrame_LowRmsEnergy_DoesNotTriggerSpeechOnset()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        var result = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(
            SequenceNumber: 1,
            SimulatedRmsEnergy: 120.0 // below 450 threshold
        ));

        result.IsSpeech.Should().BeFalse();
        result.SpeechOnsetDetected.Should().BeFalse();
        result.VadConfidence.Should().Be(0.0);
    }

    [Fact]
    public async Task IngestAudioFrame_SingleHighEnergyFrame_DoesNotTriggerOnsetUntilSecondConsecutiveFrame()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        // 1st frame above threshold
        var r1 = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(
            SequenceNumber: 1,
            SimulatedRmsEnergy: 1500.0
        ));
        r1.IsSpeech.Should().BeTrue();
        r1.SpeechOnsetDetected.Should().BeFalse(); // requires 2 consecutive frames

        // 2nd frame above threshold
        var r2 = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(
            SequenceNumber: 2,
            SimulatedRmsEnergy: 1600.0
        ));
        r2.IsSpeech.Should().BeTrue();
        r2.SpeechOnsetDetected.Should().BeTrue(); // onset officially detected
        r2.SpeechOnsetLatencyMs.Should().Be(40.0); // 2 frames * 20ms = 40ms (<100ms)
    }

    [Fact]
    public async Task IngestAudioFrame_TwoConsecutiveSpeechFrames_TriggersSpeechOnsetUnder100Ms()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 1200.0));
        var res = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 2, SimulatedRmsEnergy: 1400.0));

        res.SpeechOnsetDetected.Should().BeTrue();
        res.SpeechOnsetLatencyMs.Should().BeLessThanOrEqualTo(100.0);
        res.CurrentState.Should().Be(StreamingPipelineState.UserSpeaking);
    }

    [Fact]
    public async Task IngestAudioFrame_SpeechOnsetLatency_IsAccurateUnder100Ms()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 800.0));
        var res2 = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 2, SimulatedRmsEnergy: 950.0));

        res2.SpeechOnsetLatencyMs.Should().Be(40.0);
    }

    [Fact]
    public async Task IngestAudioFrame_TransitionsLifecycle_ToConnectedAndActiveAudio()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.Lifecycle.Should().Be(TelephonyCallLifecycle.Initiated);

        var res = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 50.0));

        res.CallLifecycle.Should().Be(TelephonyCallLifecycle.ActiveAudio);
        session.Lifecycle.Should().Be(TelephonyCallLifecycle.ActiveAudio);
    }

    [Fact]
    public async Task IngestAudioFrame_SimulatedRmsEnergy_TriggersSpeechVAD()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        var res = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(
            SequenceNumber: 1,
            SimulatedRmsEnergy: 2000.0
        ));

        res.IsSpeech.Should().BeTrue();
        res.VadConfidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task IngestAudioFrame_ConsecutiveSilence_AfterSpeech_TriggersTurnaroundTurn()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        // User speaks for 2 frames
        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 1500.0));
        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(
            SequenceNumber: 2,
            SimulatedRmsEnergy: 1600.0,
            SimulatedTextChunk: "We have an approved budget of $50,000 for immediate deployment."
        ));

        // User stops speaking (silence frames)
        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 3, SimulatedRmsEnergy: 50.0));
        var res4 = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 4, SimulatedRmsEnergy: 40.0));

        res4.CurrentState.Should().Be(StreamingPipelineState.AgentSpeaking);
        session.Transcripts.Should().HaveCountGreaterThanOrEqualTo(2);
        session.Transcripts.Any(t => t.Speaker == SpeakerRole.Agent).Should().BeTrue();
        session.TurnaroundMetrics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task IngestAudioFrame_AgentSpeaking_UserSpeechOnset_TriggersBargeInInterruption()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true, -14.0);
        session.PipelineState.Should().Be(StreamingPipelineState.AgentSpeaking);

        // User speaks 1st frame
        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 1800.0));
        // User speaks 2nd frame (onset detected while agent is speaking)
        var result = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 2, SimulatedRmsEnergy: 2100.0));

        result.BargeInTriggered.Should().BeTrue();
        result.OutboundBufferAction.Should().Be("FLUSH_OUTBOUND_QUEUE_CLEAR_TTS");
        result.CurrentState.Should().Be(StreamingPipelineState.BargeInFlush);
    }

    [Fact]
    public async Task IngestAudioFrame_BargeIn_TransitionsPipelineStateToBargeInFlush()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true);

        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 2000.0));
        var res = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 2, SimulatedRmsEnergy: 2000.0));

        res.CurrentState.Should().Be(StreamingPipelineState.BargeInFlush);
        session.PipelineState.Should().Be(StreamingPipelineState.BargeInFlush);
    }

    [Fact]
    public async Task IngestAudioFrame_BargeIn_ReturnsBufferFlushSignal()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true);

        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 1500.0));
        var res = await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 2, SimulatedRmsEnergy: 1500.0));

        res.OutboundBufferAction.Should().Be("FLUSH_OUTBOUND_QUEUE_CLEAR_TTS");
    }

    [Fact]
    public async Task IngestAudioFrame_BargeIn_IncrementsBargeInCountAndBufferFlushes()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true);

        int initialBargeIns = session.TotalBargeInCount;
        int initialFlushes = session.TotalBufferFlushes;

        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 2200.0));
        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 2, SimulatedRmsEnergy: 2200.0));

        session.TotalBargeInCount.Should().Be(initialBargeIns + 1);
        session.TotalBufferFlushes.Should().Be(initialFlushes + 1);
    }

    [Fact]
    public async Task IngestAudioFrame_BargeIn_MarksCurrentAgentTranscriptAsInterrupted()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.AddPreseededTurn("Can you explain how this works?", "Our system utilizes real-time neural VAD...", 410.0, wasInterrupted: false);
        session.SetAgentSpeaking(true);

        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 2500.0));
        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 2, SimulatedRmsEnergy: 2500.0));

        var lastAgent = session.Transcripts.Last(t => t.Speaker == SpeakerRole.Agent);
        lastAgent.IsInterrupted.Should().BeTrue();
    }

    [Fact]
    public async Task IngestAudioFrame_BargeIn_HaltsAgentSpeakingAndClearsOutputDb()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true, -12.0);
        session.CurrentOutputDb.Should().Be(-12.0);

        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 1, SimulatedRmsEnergy: 2000.0));
        await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(SequenceNumber: 2, SimulatedRmsEnergy: 2000.0));

        session.IsAgentSpeaking.Should().BeFalse();
        session.CurrentOutputDb.Should().Be(-90.0);
    }

    [Fact]
    public async Task TriggerBargeInAsync_ExplicitCall_FlushesOutboundTtsBuffer()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true);

        bool success = await _service.TriggerBargeInAsync(session.Id, new BargeInRequest(Reason: "OperatorForcedInterruption", InterruptionOffsetMs: 120.0));

        success.Should().BeTrue();
        session.TotalBargeInCount.Should().Be(1);
        session.TotalBufferFlushes.Should().Be(1);
        session.PipelineState.Should().Be(StreamingPipelineState.BargeInFlush);
        session.IsAgentSpeaking.Should().BeFalse();
    }

    [Fact]
    public async Task TriggerBargeInAsync_NonExistentSession_ReturnsFalse()
    {
        bool success = await _service.TriggerBargeInAsync(Guid.NewGuid(), new BargeInRequest());
        success.Should().BeFalse();
    }

    [Fact]
    public async Task TriggerBargeInAsync_AppendsSystemInterruptionTranscriptSegment()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true);

        await _service.TriggerBargeInAsync(session.Id, new BargeInRequest(Reason: "CallerBargedIn", InterruptionOffsetMs: 50.0));

        var sysSeg = session.Transcripts.FirstOrDefault(t => t.Speaker == SpeakerRole.System && t.Text.Contains("Barge-In"));
        sysSeg.Should().NotBeNull();
        sysSeg!.Text.Should().Contain("CallerBargedIn");
    }

    [Fact]
    public void TurnaroundLatency_CalculatesAccurateTotalMs_Under600MsTarget()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn("What is your turnaround latency?", "It is consistently sub-600ms.", turnaroundMs: 430.0);

        var metrics = session.TurnaroundMetrics;
        metrics.Should().HaveCount(1);
        metrics[0].TotalTurnaroundMs.Should().Be(430.0);
        metrics[0].IsSub600Ms.Should().BeTrue();
        metrics[0].TotalTurnaroundMs.Should().BeLessThan(600.0);
    }

    [Fact]
    public void TurnaroundLatency_BreaksDownVadAsrLlmTtsPhases()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn("Question", "Answer", turnaroundMs: 400.0);

        var m = session.TurnaroundMetrics[0];
        m.VadLatencyMs.Should().BeGreaterThan(0);
        m.AsrLatencyMs.Should().BeGreaterThan(0);
        m.LlmLatencyMs.Should().BeGreaterThan(0);
        m.TtsLatencyMs.Should().BeGreaterThan(0);
        (m.AsrLatencyMs + m.LlmLatencyMs + m.TtsLatencyMs).Should().BeApproximately(400.0, 1.0);
    }

    [Fact]
    public void TurnaroundLatency_ClassifiesLatencyGrade_OptimalOrTarget()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn("Q1", "A1", turnaroundMs: 380.0); // <400
        session.AddPreseededTurn("Q2", "A2", turnaroundMs: 490.0); // 400-600
        session.AddPreseededTurn("Q3", "A3", turnaroundMs: 650.0); // >600

        session.TurnaroundMetrics[0].LatencyGrade.Should().Be("Optimal (<400ms)");
        session.TurnaroundMetrics[1].LatencyGrade.Should().Be("Target (<600ms)");
        session.TurnaroundMetrics[2].LatencyGrade.Should().Be("Degraded (>600ms)");
    }

    [Fact]
    public void TurnaroundLatency_RollingMetrics_CalculatesAverageMinMaxCorrectly()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn("Q1", "A1", turnaroundMs: 400.0);
        session.AddPreseededTurn("Q2", "A2", turnaroundMs: 500.0);

        var m = session.ToMetrics();
        m.AverageTurnaroundLatencyMs.Should().Be(450.0);
        m.MinTurnaroundLatencyMs.Should().Be(400.0);
        m.MaxTurnaroundLatencyMs.Should().Be(500.0);
    }

    [Fact]
    public void TurnaroundLatency_Sub600msCompliance_IsTrueWhenAllUnder600Ms()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn("Q1", "A1", turnaroundMs: 390.0);
        session.AddPreseededTurn("Q2", "A2", turnaroundMs: 440.0);

        var m = session.ToMetrics();
        m.Sub600msCompliance.Should().BeTrue();
    }

    [Fact]
    public async Task TransferCall_TransitionsLifecycle_ToTransferredToHuman()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        bool transferred = await _service.TransferCallAsync(session.Id, new TransferCallRequest(
            TargetDesk: "Tier-2-Escalations",
            Reason: "Complex SLA inquiry",
            Notes: "Customer requesting senior engineer"
        ));

        transferred.Should().BeTrue();
        session.Lifecycle.Should().Be(TelephonyCallLifecycle.TransferredToHuman);
        session.TransferDestination.Should().Be("Tier-2-Escalations");
        session.TransferReason.Should().Be("Complex SLA inquiry");
    }

    [Fact]
    public async Task TransferCall_SetsDestinationReasonAndNotes()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        await _service.TransferCallAsync(session.Id, new TransferCallRequest(
            TargetDesk: "VIP-Queue",
            Reason: "High Value Lead",
            Notes: "Deal size >$100k"
        ));

        session.TransferDestination.Should().Be("VIP-Queue");
        session.TransferredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task TransferCall_TransitionsPipelineStateToIdle()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true);

        await _service.TransferCallAsync(session.Id, new TransferCallRequest(TargetDesk: "Desk", Reason: "Escalate"));

        session.PipelineState.Should().Be(StreamingPipelineState.Idle);
        session.IsAgentSpeaking.Should().BeFalse();
        session.CurrentOutputDb.Should().Be(-90.0);
    }

    [Fact]
    public async Task TransferCall_AppendsSystemTransferTranscript()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        await _service.TransferCallAsync(session.Id, new TransferCallRequest(TargetDesk: "Support-Tier-3", Reason: "Outage"));

        var seg = session.Transcripts.Last(t => t.Speaker == SpeakerRole.System);
        seg.Text.Should().Contain("Warm Call Transfer to Support-Tier-3");
    }

    [Fact]
    public async Task TransferCall_NonExistentSession_ReturnsFalse()
    {
        bool success = await _service.TransferCallAsync(Guid.NewGuid(), new TransferCallRequest("Desk", "Reason"));
        success.Should().BeFalse();
    }

    [Fact]
    public async Task HangupCall_TransitionsLifecycle_ToCallCompleted()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        bool hungup = await _service.HangupCallAsync(session.Id, new HangupCallRequest("CustomerSatisfied"));

        hungup.Should().BeTrue();
        session.Lifecycle.Should().Be(TelephonyCallLifecycle.CallCompleted);
        session.EndedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task HangupCall_SetsEndedAtTimestamp()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        await _service.HangupCallAsync(session.Id, new HangupCallRequest());

        session.EndedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task HangupCall_TransitionsPipelineStateToIdle()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true);

        await _service.HangupCallAsync(session.Id, new HangupCallRequest());

        session.PipelineState.Should().Be(StreamingPipelineState.Idle);
        session.IsAgentSpeaking.Should().BeFalse();
    }

    [Fact]
    public async Task HangupCall_AppendsSystemHangupTranscript()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        await _service.HangupCallAsync(session.Id, new HangupCallRequest("NormalWrapUp"));

        var seg = session.Transcripts.Last(t => t.Speaker == SpeakerRole.System);
        seg.Text.Should().Contain("Call completed. Reason: NormalWrapUp");
    }

    [Fact]
    public async Task HangupCall_NonExistentSession_ReturnsFalse()
    {
        bool success = await _service.HangupCallAsync(Guid.NewGuid(), new HangupCallRequest());
        success.Should().BeFalse();
    }

    [Fact]
    public void IntentExtraction_ExtractsProspectBudget_Accurately()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn(
            "We have a budget of $75,000 for this project.",
            "Understood, that fits our enterprise tier perfectly.",
            410.0
        );

        session.ExtractedInsights.Should().NotBeNull();
        session.ExtractedInsights!.ProspectBudget.Should().Be(75000m);
    }

    [Fact]
    public void IntentExtraction_ExtractsTimeframe_QuarterOrImmediate()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn(
            "We need this deployed by Q4.",
            "We can definitely hit that deployment window.",
            400.0
        );

        session.ExtractedInsights.Should().NotBeNull();
        session.ExtractedInsights!.Timeframe.Should().Be("Q4");
    }

    [Fact]
    public void IntentExtraction_ComputesPositiveSentiment_ForEnthusiasticUtterance()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn(
            "This is great, awesome platform and perfect for our deal!",
            "Thank you! We take pride in sub-600ms latency.",
            390.0
        );

        session.ExtractedInsights.Should().NotBeNull();
        session.ExtractedInsights!.SentimentScore.Should().BeGreaterThan(0.3);
        session.ExtractedInsights!.SentimentLabel.Should().Be("Positive");
    }

    [Fact]
    public void IntentExtraction_ComputesNegativeSentiment_ForFrustratedUtterance()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn(
            "I am frustrated with the slow broken legacy service and angry about downtime.",
            "I completely understand your frustration. Let me help.",
            420.0
        );

        session.ExtractedInsights.Should().NotBeNull();
        session.ExtractedInsights!.SentimentScore.Should().BeLessThan(0.0);
        session.ExtractedInsights!.SentimentLabel.Should().Be("Frustrated");
    }

    [Fact]
    public void IntentExtraction_ComputesCrmDealValue_WithTierMultiplier()
    {
        var session = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };

        session.AddPreseededTurn(
            "Our budget is $50,000 for this year.",
            "Sounds promising.",
            410.0
        );

        session.ExtractedInsights.Should().NotBeNull();
        session.ExtractedInsights!.CrmDealValue.Should().Be(50000m * 1.15m); // $57,500
    }

    [Fact]
    public void IntentExtraction_MapsIntentClassification_DemoVsEscalation()
    {
        var s1 = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("sophia", "Sophia", "azure", "AE", 140, 1.0, "en-US", true)
        };
        s1.AddPreseededTurn("I would like to schedule a demo next week.", "Sure thing!", 400.0);
        s1.ExtractedInsights!.Intent.Should().Be("ScheduleDemo");

        var s2 = new VoiceCallSession
        {
            TenantId = _tenantId,
            VoiceProfile = new VoiceAgentProfile("alex", "Alex", "openai", "Architect", 130, 1.0, "en-US", true)
        };
        s2.AddPreseededTurn("Please transfer me to a human engineer immediately.", "Connecting now.", 390.0);
        s2.ExtractedInsights!.Intent.Should().Be("TechnicalEscalation");
    }

    [Fact]
    public async Task ListSessions_ReturnsAllSessions_AndFiltersByTenant()
    {
        var allSessions = await _service.ListSessionsAsync();
        allSessions.Should().NotBeEmpty();
        allSessions.Count.Should().BeGreaterThanOrEqualTo(4); // from seeded demo calls

        var tenantSessions = await _service.ListSessionsAsync(_tenantId);
        tenantSessions.Should().NotBeEmpty();
        tenantSessions.All(s => s.TenantId == _tenantId).Should().BeTrue();
    }

    [Fact]
    public async Task ListAgentProfiles_ReturnsAllSeededNeuralProfiles()
    {
        var profiles = await _service.ListAgentProfilesAsync();
        profiles.Should().HaveCount(3);
        profiles.Select(p => p.Id).Should().Contain(new[] { "sophia-enterprise", "alex-technical", "marcus-closer" });
        profiles.All(p => p.BargeInEnabled).Should().BeTrue();
    }

    [Fact]
    public async Task GetSessionMetrics_ReturnsFullSnapshot_WithTranscriptsAndMeters()
    {
        var seededId = Guid.Parse("00000000-0000-0000-0000-000000000050");
        var metrics = await _service.GetSessionMetricsAsync(seededId);

        metrics.Should().NotBeNull();
        metrics!.SessionId.Should().Be(seededId);
        metrics.Transcript.Should().NotBeEmpty();
        metrics.RecentTurnLatencies.Should().NotBeEmpty();
        metrics.Sub600msCompliance.Should().BeTrue();
        metrics.AverageTurnaroundLatencyMs.Should().BeLessThan(600.0);
    }

    [Fact]
    public async Task ConcurrentAudioStreaming_MultiThreadedIngestion_MaintainsIntegrity()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));

        int threadCount = 20;
        int framesPerThread = 25;
        var tasks = new List<Task>();

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks.Add(Task.Run(async () =>
            {
                for (int f = 0; f < framesPerThread; f++)
                {
                    int seq = (threadId * framesPerThread) + f + 1;
                    double energy = (f % 5 == 0) ? 1200.0 : 150.0;
                    await _service.IngestAudioFrameAsync(session.Id, new IngestAudioFrameRequest(
                        SequenceNumber: seq,
                        SimulatedRmsEnergy: energy
                    ));
                }
            }));
        }

        await Task.WhenAll(tasks);

        session.TotalFramesReceived.Should().Be(threadCount * framesPerThread);
    }

    [Fact]
    public async Task ConcurrentBargeIn_UnderHighContention_MaintainsConsistentState()
    {
        var session = await _service.InitiateSessionAsync(new InitiateSessionRequest(TenantId: _tenantId));
        session.SetAgentSpeaking(true);

        int bargeInAttempts = 15;
        var tasks = Enumerable.Range(0, bargeInAttempts).Select(i =>
            Task.Run(() => _service.TriggerBargeInAsync(session.Id, new BargeInRequest(Reason: $"ContentionTest-{i}")))
        );

        await Task.WhenAll(tasks);

        session.TotalBargeInCount.Should().Be(bargeInAttempts);
        session.TotalBufferFlushes.Should().Be(bargeInAttempts);
        session.IsAgentSpeaking.Should().BeFalse();
        session.PipelineState.Should().Be(StreamingPipelineState.BargeInFlush);
    }
}
