using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.EmotionDeescalation;

public sealed class EmotionDeescalationEngine
{
    private readonly ConcurrentDictionary<string, LiveCallAcousticTelemetry> _telemetry = new();
    private readonly ConcurrentDictionary<string, SupervisorEscalationTicket> _escalations = new();
    private readonly ConcurrentBag<SupervisorHandoffReceipt> _handoffHistory = new();

    public EmotionDeescalationEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var call1 = new LiveCallAcousticTelemetry(
            "CALL-VOC-8801",
            "AGT-EMMA-01",
            "+1-415-555-0199",
            145.2,
            210.5,
            0.88,
            "HIGH_FRUSTRATION",
            "LEAP Empathy Pivot: Acknowledge delayed onboarding before addressing pricing"
        );

        var call2 = new LiveCallAcousticTelemetry(
            "CALL-VOC-8802",
            "AGT-LIAM-02",
            "+44-20-7946-0912",
            82.0,
            145.0,
            0.24,
            "CALM_COLLABORATIVE",
            "Maintain positive validation and proceed to contract signature"
        );

        _telemetry[call1.CallId] = call1;
        _telemetry[call2.CallId] = call2;

        var esc1 = new SupervisorEscalationTicket(
            "ESC-SUP-101",
            call1.CallId,
            call1.CustomerPhone,
            0.92,
            "Contract Renewal Dispute & Integration Blocker",
            "Authorize 1-month SLA credit + Dedicated Solution Architect",
            "PENDING_SUPERVISOR_ACCEPT",
            DateTime.UtcNow.AddMinutes(-5)
        );

        _escalations[esc1.EscalationId] = esc1;
    }

    public EmotionDeescalationOverview GetOverview()
    {
        var teleList = _telemetry.Values.ToList();
        var escList = _escalations.Values.ToList();
        var avgStress = teleList.Count > 0 ? teleList.Average(t => t.FrustrationScore) : 0.45;

        return new EmotionDeescalationOverview(
            teleList.Count,
            91.4,
            Math.Round(avgStress, 2),
            escList.Count(e => e.Status == "PENDING_SUPERVISOR_ACCEPT"),
            teleList,
            escList
        );
    }

    public AcousticAnalysisResult AnalyzeAcoustics(AnalyzeAcousticsRequest request)
    {
        var frustration = Math.Min(1.0, (request.PitchVarianceHz / 200.0 * 0.5) + (request.SpeechVelocityWordsPerMin / 250.0 * 0.5));
        var requiresDeescalation = frustration > 0.65;
        var emotion = frustration > 0.75 ? "ELEVATED_ANGER" : (frustration > 0.5 ? "ANNOYED" : "RECEPTIVE");

        var script = requiresDeescalation
            ? "I completely understand how critical this downtime is for your revenue, and I am taking personal ownership right now to resolve this immediately."
            : "Thank you for that context. Let's look at how we can tailor this configuration to your team's specific requirements.";

        var sentiment = 1.0 - frustration;

        return new AcousticAnalysisResult(
            $"ACO-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            request.CallId,
            Math.Round(sentiment, 2),
            Math.Round(frustration, 2),
            emotion,
            requiresDeescalation,
            script,
            112
        );
    }

    public SupervisorHandoffReceipt TriggerSupervisorHandoff(SupervisorHandoffRequest request)
    {
        var receipt = new SupervisorHandoffReceipt(
            $"TRF-{Guid.NewGuid():N}"[..10].ToUpperInvariant(),
            request.CallId,
            request.SupervisorId,
            "WARM_HANDOFF_CONNECTED",
            $"BRIEF-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            DateTime.UtcNow
        );

        _handoffHistory.Add(receipt);
        return receipt;
    }
}
