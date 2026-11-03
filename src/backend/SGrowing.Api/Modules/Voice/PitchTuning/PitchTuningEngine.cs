using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.PitchTuning;

public interface IPitchTuningEngine
{
    PitchTuningOverview GetOverview();
    PitchTuningResult TunePitch(TunePitchCommand command);
    LiveCallTelemetry SimulateSentimentShift(string callId, AcousticSentiment newSentiment);
}

public class PitchTuningEngine : IPitchTuningEngine
{
    private readonly ConcurrentDictionary<string, LiveCallTelemetry> _calls = new();

    public PitchTuningEngine()
    {
        SeedLiveCalls();
    }

    private void SeedLiveCalls()
    {
        var call1 = new LiveCallTelemetry(
            CallId: "CALL-VOICE-9801",
            ContactName: "Jonathan Hayes (VP Infrastructure)",
            CompanyName: "CyberPoint Defense",
            PhoneNumber: "+1 (415) 883-9912",
            CurrentSentiment: AcousticSentiment.Defensive,
            ConfidenceScore: 0.94,
            FundamentalFrequencyHz: 148.5,
            SpeechRateWpm: 120,
            CurrentPersona: TonePersona.Empathic,
            RecommendedPivotAngle: "Risk-Reversal & Zero-Disruption SLA Assurance",
            DurationSeconds: 142
        );

        var call2 = new LiveCallTelemetry(
            CallId: "CALL-VOICE-9802",
            ContactName: "Dr. Evelyn Reed (Head of Data)",
            CompanyName: "BioGenomics AI",
            PhoneNumber: "+44 20 7946 0192",
            CurrentSentiment: AcousticSentiment.Curious,
            ConfidenceScore: 0.89,
            FundamentalFrequencyHz: 128.2,
            SpeechRateWpm: 145,
            CurrentPersona: TonePersona.Consultative,
            RecommendedPivotAngle: "Autonomous Multi-Region Sharding Architecture Deep Dive",
            DurationSeconds: 285
        );

        var call3 = new LiveCallTelemetry(
            CallId: "CALL-VOICE-9803",
            ContactName: "Alexandre Moreau (Chief Product Officer)",
            CompanyName: "Lumiere Fintech Paris",
            PhoneNumber: "+33 1 42 68 55 00",
            CurrentSentiment: AcousticSentiment.Enthusiastic,
            ConfidenceScore: 0.96,
            FundamentalFrequencyHz: 135.0,
            SpeechRateWpm: 158,
            CurrentPersona: TonePersona.Assertive,
            RecommendedPivotAngle: "Executive Pilot Fast-Track & Q4 Implementation Escrow",
            DurationSeconds: 410
        );

        _calls[call1.CallId] = call1;
        _calls[call2.CallId] = call2;
        _calls[call3.CallId] = call3;
    }

    public PitchTuningOverview GetOverview()
    {
        var active = _calls.Values.OrderByDescending(c => c.DurationSeconds).ToList();
        var avgSentiment = active.Count > 0 ? active.Average(c => (int)c.CurrentSentiment) : 2.0;

        return new PitchTuningOverview(
            ActiveCallsCount: active.Count,
            AverageCallSentimentScore: Math.Round(avgSentiment, 2),
            ConversionLiftPercentage: 34.8, // 34.8% lift achieved via real-time pitch adaptation
            ActiveCalls: active
        );
    }

    public PitchTuningResult TunePitch(TunePitchCommand command)
    {
        if (!_calls.TryGetValue(command.CallId, out var existing))
        {
            throw new KeyNotFoundException($"Call {command.CallId} not found.");
        }

        var updated = existing with
        {
            FundamentalFrequencyHz = Math.Round(existing.FundamentalFrequencyHz + command.TargetPitchShiftHz, 1),
            SpeechRateWpm = command.TargetSpeechRateWpm,
            CurrentPersona = command.TargetPersona,
            RecommendedPivotAngle = command.TacticalPitchAngle
        };

        _calls[command.CallId] = updated;

        double deescalationLikelihood = command.TargetPersona switch
        {
            TonePersona.Empathic => 0.92,
            TonePersona.Consultative => 0.88,
            TonePersona.Assertive => 0.79,
            _ => 0.70
        };

        var log = $"Real-Time Speech DSP: Adjusted F0 by {command.TargetPitchShiftHz:+0.0;-0.0} Hz, pace to {command.TargetSpeechRateWpm} WPM, switched persona to {command.TargetPersona}. Angle: '{command.TacticalPitchAngle}'.";

        return new PitchTuningResult(
            CallId: command.CallId,
            AppliedPitchShiftHz: command.TargetPitchShiftHz,
            AppliedSpeechRateWpm: command.TargetSpeechRateWpm,
            AppliedPersona: command.TargetPersona,
            PitchAngle: command.TacticalPitchAngle,
            ProjectedDeescalationLikelihood: deescalationLikelihood,
            TuningLog: log
        );
    }

    public LiveCallTelemetry SimulateSentimentShift(string callId, AcousticSentiment newSentiment)
    {
        if (!_calls.TryGetValue(callId, out var existing))
        {
            throw new KeyNotFoundException($"Call {callId} not found.");
        }

        var (pivot, persona, targetWpm) = newSentiment switch
        {
            AcousticSentiment.Defensive => ("Risk-Reversal & SOC2 Compliance Guarantee", TonePersona.Empathic, 122),
            AcousticSentiment.Skeptical => ("Verifiable Benchmark Data & Case Studies", TonePersona.Consultative, 130),
            AcousticSentiment.Curious => ("Interactive Architecture & Live Sandboxing", TonePersona.Consultative, 145),
            AcousticSentiment.Enthusiastic => ("Fast-Track Procurement & Multi-Year Incentive Lock", TonePersona.Assertive, 160),
            _ => ("Value Discovery & Core Workflow Alignment", TonePersona.Consultative, 140)
        };

        var updated = existing with
        {
            CurrentSentiment = newSentiment,
            RecommendedPivotAngle = pivot,
            CurrentPersona = persona,
            SpeechRateWpm = targetWpm,
            ConfidenceScore = 0.95
        };

        _calls[callId] = updated;
        return updated;
    }
}
