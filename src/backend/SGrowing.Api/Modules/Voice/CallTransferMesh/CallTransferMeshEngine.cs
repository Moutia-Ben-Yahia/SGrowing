using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.CallTransferMesh;

public interface ICallTransferMeshEngine
{
    List<LiveCallSession> GetActiveCalls();
    LiveCallSession? GetCall(string callId);
    LiveCallSession InitiateWarmTransfer(InitiateWarmTransferRequest request);
    LiveCallSession CompleteTransfer(CompleteTransferRequest request);
}

public class CallTransferMeshEngine : ICallTransferMeshEngine
{
    private readonly ConcurrentDictionary<string, LiveCallSession> _calls = new(StringComparer.OrdinalIgnoreCase);

    public CallTransferMeshEngine()
    {
        // Seed active live call sessions
        var call1 = new LiveCallSession(
            CallId: "call_vce_801",
            CallerPhoneNumber: "+1 (415) 555-0199",
            CustomerName: "Dr. Evelyn Reed (Vertex BioPharma)",
            IntentClassification: "SOC2 Compliance & HIPAA Security Review",
            CallerSentimentScore: 0.65,
            AssignedAiPersona: "SecurityArchitectAI",
            TargetHumanSpecialist: null,
            State: CallTransferState.AiHandling,
            StartedAtUtc: DateTime.UtcNow.AddMinutes(-4)
        );

        var call2 = new LiveCallSession(
            CallId: "call_vce_802",
            CallerPhoneNumber: "+44 20 7946 0912",
            CustomerName: "Lars Lindholm (Nordic Fintech)",
            IntentClassification: "Cross-Border FX Hedging Forward Rate Lock",
            CallerSentimentScore: -0.35,
            AssignedAiPersona: "FinOpsVoiceSdr",
            TargetHumanSpecialist: "Marcus_VP_FinOps",
            State: CallTransferState.AgentPrewarming,
            StartedAtUtc: DateTime.UtcNow.AddMinutes(-6)
        );

        _calls[call1.CallId] = call1;
        _calls[call2.CallId] = call2;
    }

    public List<LiveCallSession> GetActiveCalls()
    {
        return _calls.Values.OrderByDescending(c => c.StartedAtUtc).ToList();
    }

    public LiveCallSession? GetCall(string callId)
    {
        _calls.TryGetValue(callId, out var call);
        return call;
    }

    public LiveCallSession InitiateWarmTransfer(InitiateWarmTransferRequest request)
    {
        if (!_calls.TryGetValue(request.CallId, out var existing))
        {
            throw new KeyNotFoundException($"Call {request.CallId} not found.");
        }

        var updated = existing with
        {
            TargetHumanSpecialist = request.TargetHumanSpecialist,
            State = CallTransferState.ThreeWayBridge
        };

        _calls[request.CallId] = updated;
        return updated;
    }

    public LiveCallSession CompleteTransfer(CompleteTransferRequest request)
    {
        if (!_calls.TryGetValue(request.CallId, out var existing))
        {
            throw new KeyNotFoundException($"Call {request.CallId} not found.");
        }

        var updated = existing with
        {
            State = CallTransferState.TransferCompleted,
            CallerSentimentScore = Math.Min(1.0, existing.CallerSentimentScore + 0.4)
        };

        _calls[request.CallId] = updated;
        return updated;
    }
}
