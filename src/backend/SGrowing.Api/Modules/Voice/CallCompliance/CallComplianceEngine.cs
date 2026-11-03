using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SGrowing.Api.Modules.Voice.CallCompliance;

public interface ICallComplianceEngine
{
    List<CallAuditEntry> GetAuditCorpus();
    AuditDecisionResult RecordDecision(AuditCallRequest request);
}

public class CallComplianceEngine : ICallComplianceEngine
{
    private readonly ConcurrentDictionary<string, CallAuditEntry> _audits = new(StringComparer.OrdinalIgnoreCase);

    public CallComplianceEngine()
    {
        // Seed audit records
        var a1 = new CallAuditEntry(
            "audit_rec_101",
            "call_voice_01",
            "AI Agent (Atlas SDR)",
            DateTime.UtcNow.AddMinutes(-45),
            180,
            96.5,
            true,
            true,
            AuditStatus.CompliantPassed,
            new List<string>()
        );

        var a2 = new CallAuditEntry(
            "audit_rec_102",
            "call_voice_02",
            "Human Specialist (Elena Rostova)",
            DateTime.UtcNow.AddMinutes(-30),
            240,
            88.0,
            true,
            true,
            AuditStatus.CompliantPassed,
            new List<string>()
        );

        var a3 = new CallAuditEntry(
            "audit_rec_103",
            "call_voice_03",
            "AI Agent (Titan Support)",
            DateTime.UtcNow.AddMinutes(-10),
            120,
            64.0,
            true,
            false,
            AuditStatus.FlaggedForReview,
            new List<string> { "Mandatory recording disclosure statement skipped at 00:03" }
        );

        _audits[a1.AuditId] = a1;
        _audits[a2.AuditId] = a2;
        _audits[a3.AuditId] = a3;
    }

    public List<CallAuditEntry> GetAuditCorpus() => _audits.Values.OrderByDescending(a => a.TimestampUtc).ToList();

    public AuditDecisionResult RecordDecision(AuditCallRequest request)
    {
        if (!_audits.TryGetValue(request.AuditId, out var existing))
        {
            throw new KeyNotFoundException($"Audit entry {request.AuditId} not found.");
        }

        var updated = existing with
        {
            Status = request.Decision
        };

        _audits[request.AuditId] = updated;

        return new AuditDecisionResult(
            request.AuditId,
            request.Decision,
            request.AuditorNotes,
            DateTime.UtcNow
        );
    }
}
