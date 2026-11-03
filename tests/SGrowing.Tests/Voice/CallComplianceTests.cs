using System;
using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Voice.CallCompliance;
using Xunit;

namespace SGrowing.Tests.Voice;

public class CallComplianceTests
{
    private readonly CallComplianceEngine _engine = new();

    [Fact]
    public void GetAuditCorpus_ReturnsSeededAudits()
    {
        var corpus = _engine.GetAuditCorpus();

        corpus.Should().NotBeNull();
        corpus.Should().HaveCount(3);
        corpus.Should().Contain(a => a.AuditId == "audit_rec_101" && a.Status == AuditStatus.CompliantPassed);
        corpus.Should().Contain(a => a.AuditId == "audit_rec_103" && a.Status == AuditStatus.FlaggedForReview);
    }

    [Fact]
    public void RecordDecision_UpdatesStatusAndReturnsResult()
    {
        var auditId = "audit_rec_103";
        var request = new AuditCallRequest(auditId, AuditStatus.CompliantPassed, "Agent confirmed verbal disclosure on secondary audio track.");

        var result = _engine.RecordDecision(request);

        result.Should().NotBeNull();
        result.AuditId.Should().Be(auditId);
        result.UpdatedStatus.Should().Be(AuditStatus.CompliantPassed);

        var audit = _engine.GetAuditCorpus().Find(a => a.AuditId == auditId)!;
        audit.Status.Should().Be(AuditStatus.CompliantPassed);
    }

    [Fact]
    public void RecordDecision_ThrowsWhenAuditNotFound()
    {
        var action = () => _engine.RecordDecision(new AuditCallRequest("non_existent_audit", AuditStatus.CompliantPassed, "notes"));

        action.Should().Throw<KeyNotFoundException>()
            .WithMessage("*not found*");
    }
}
