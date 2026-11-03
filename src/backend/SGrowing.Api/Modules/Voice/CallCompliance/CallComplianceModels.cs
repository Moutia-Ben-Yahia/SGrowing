using System;
using System.Collections.Generic;

namespace SGrowing.Api.Modules.Voice.CallCompliance;

public enum AuditStatus
{
    PendingAudit,
    CompliantPassed,
    FlaggedForReview,
    ViolationCritical
}

public record CallAuditEntry(
    string AuditId,
    string CallId,
    string AgentName,
    DateTime TimestampUtc,
    int DurationSeconds,
    double ScriptAdherenceScore, // 0 - 100
    bool PiiRedactionVerified,
    bool MandatoryDisclosureRecited,
    AuditStatus Status,
    List<string> FlaggedSnippets
);

public record AuditCallRequest(
    string AuditId,
    AuditStatus Decision,
    string AuditorNotes
);

public record AuditDecisionResult(
    string AuditId,
    AuditStatus UpdatedStatus,
    string Notes,
    DateTime DecidedAtUtc
);
