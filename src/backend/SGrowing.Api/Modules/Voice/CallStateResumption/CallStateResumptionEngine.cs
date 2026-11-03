using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.CallStateResumption;

public sealed class CallStateResumptionEngine
{
    private readonly ConcurrentDictionary<string, ActiveCallSessionSnapshotRecord> _snapshots = new();
    private readonly ConcurrentBag<CallResumptionEventRecord> _resumptionEvents = new();

    public CallStateResumptionEngine()
    {
        InitializeDefaultData();
    }

    private void InitializeDefaultData()
    {
        var s1 = new ActiveCallSessionSnapshotRecord(
            "CALL-SES-991",
            "+1-415-555-0199",
            "+1-800-555-0100",
            "Marcus Brody (VP Technology)",
            "We were wondering how your SOC2 compliance handles multi-region data sovereignty...",
            "Security Architecture & Contract Negotiation",
            "COMMERCIAL_PROPOSAL_REVIEW",
            14,
            DateTime.UtcNow.AddSeconds(-8)
        );

        var s2 = new ActiveCallSessionSnapshotRecord(
            "CALL-SES-992",
            "+44-20-7946-0922",
            "+44-800-014-9900",
            "Helena Berg (Director of FinOps)",
            "Can we lock in the multi-currency hedging rates before next quarter?",
            "Treasury & Payment Terms",
            "PAYMENT_TERMS_DISCOUNT",
            9,
            DateTime.UtcNow.AddSeconds(-3)
        );

        _snapshots[s1.SessionId] = s1;
        _snapshots[s2.SessionId] = s2;

        var r1 = new CallResumptionEventRecord(
            "RES-EVT-1001",
            "CALL-SES-988",
            "CALL-SES-988-REC",
            "CELLULAR_TOWER_HANDOFF_DROP",
            2.8,
            "Hi Marcus, apologize for the brief signal drop! We were discussing the SOC2 data sovereignty boundaries.",
            "RESUMED_IN_PROGRESS"
        );

        _resumptionEvents.Add(r1);
    }

    public StateResumptionOverview GetOverview()
    {
        var snapshotList = _snapshots.Values.ToList();
        var resumptionList = _resumptionEvents.ToList();
        var successful = resumptionList.Count(r => r.ResumptionStatus != "ABANDONED");
        var successRate = resumptionList.Count > 0 ? (double)successful / resumptionList.Count * 100.0 : 96.8;
        var avgLatency = resumptionList.Count > 0 ? resumptionList.Average(r => r.ReconnectLatencySeconds) : 3.1;

        return new StateResumptionOverview(
            snapshotList.Count,
            resumptionList.Count,
            successRate,
            avgLatency,
            snapshotList,
            resumptionList
        );
    }

    public CallResumptionEventRecord TriggerAutoCallback(TriggerAutoCallbackRequest request)
    {
        var resumptionId = $"RES-{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var reconnectSessionId = $"{request.SessionId}-REC";
        var snapshot = _snapshots.GetValueOrDefault(request.SessionId);

        var custName = snapshot?.CustomerName ?? "there";
        var lastTopic = snapshot?.DialogGoalTopic ?? "our conversation";
        var greeting = $"Hi {custName.Split(' ')[0]}, apologies for the dropped connection! We were right in the middle of discussing {lastTopic}.";

        var evt = new CallResumptionEventRecord(
            resumptionId,
            request.SessionId,
            reconnectSessionId,
            request.DisconnectReason,
            2.4,
            greeting,
            "AUTO_CALLED_BACK"
        );

        _resumptionEvents.Add(evt);
        return evt;
    }

    public ActiveCallSessionSnapshotRecord SaveCallSnapshot(SaveCallSnapshotRequest request)
    {
        var record = new ActiveCallSessionSnapshotRecord(
            request.SessionId,
            request.CallerPhoneNumber,
            request.CalleePhoneNumber,
            request.CustomerName,
            request.LastAcousticUtterance,
            request.DialogGoalTopic,
            request.CurrentContractPhase,
            request.TurnCounter,
            DateTime.UtcNow
        );

        _snapshots[request.SessionId] = record;
        return record;
    }
}
