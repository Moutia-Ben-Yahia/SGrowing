using SGrowing.Api.Modules.Sales.ContractRedline;
using Xunit;

namespace SGrowing.Tests.Sales;

public class ContractRedlineTests
{
    [Fact]
    public void GetOverview_ComputesMetricsAndListsActiveRedlines()
    {
        var engine = new ContractRedlineEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.Equal("Northstar Enterprise Holdings", overview.AccountName);
        Assert.True(overview.TotalClausesReviewedCount >= 2);
        Assert.True(overview.AcceptableCompromisesCount >= 1);
        Assert.True(overview.AverageDeviationRiskScore > 0);
        Assert.NotEmpty(overview.ActiveRedlines);
        Assert.NotEmpty(overview.RecentApprovals);
    }

    [Fact]
    public void NegotiateClause_GeneratesCompromiseAndClassifiesRisk()
    {
        var engine = new ContractRedlineEngine();
        var request = new NegotiateClauseRequest(
            "MSA-2026-NORTHSTAR",
            "IP_OWNERSHIP",
            "Customer shall own all custom models and code derivatives synthesized during deployment."
        );

        var record = engine.NegotiateClause(request);

        Assert.NotNull(record);
        Assert.StartsWith("RED-", record.ClauseId);
        Assert.Equal("UNACCEPTABLE_ESCALATE", record.RiskClassification);
        Assert.Equal("COMPROMISE_OFFERED", record.Status);

        var overview = engine.GetOverview();
        Assert.Contains(overview.ActiveRedlines, r => r.ClauseId == record.ClauseId);
    }

    [Fact]
    public void ApproveDeviation_RecordsSealAndUpdatesStatusToApproved()
    {
        var engine = new ContractRedlineEngine();
        var request = new ApproveDeviationRequest(
            "RED-LOL-001",
            "Michael Chang (Chief Legal Officer)",
            "Executive business case approval granted for Strategic Tier 1 account"
        );

        var approval = engine.ApproveDeviation(request);

        Assert.NotNull(approval);
        Assert.StartsWith("APP-", approval.ApprovalId);
        Assert.StartsWith("SEAL-SHA256-", approval.CryptographicAuditSeal);

        var overview = engine.GetOverview();
        var clause = overview.ActiveRedlines.FirstOrDefault(r => r.ClauseId == "RED-LOL-001");
        Assert.NotNull(clause);
        Assert.Equal("APPROVED", clause.Status);
    }

    [Fact]
    public void NegotiateClause_GoverningLaw_ClassifiesAsStandardAccept()
    {
        var engine = new ContractRedlineEngine();
        var request = new NegotiateClauseRequest(
            "MSA-2026-NORTHSTAR",
            "GOVERNING_LAW",
            "Agreement shall be governed by the laws of the State of Delaware."
        );

        var record = engine.NegotiateClause(request);

        Assert.NotNull(record);
        Assert.Equal(20.0, record.DeviationRiskScore);
        Assert.Equal("STANDARD_ACCEPT", record.RiskClassification);
        Assert.Equal("COMPROMISE_OFFERED", record.Status);
    }

    [Fact]
    public void NegotiateClause_Indemnification_ClassifiesAsAcceptableCompromise()
    {
        var engine = new ContractRedlineEngine();
        var request = new NegotiateClauseRequest(
            "MSA-2026-NORTHSTAR",
            "INDEMNIFICATION",
            "Customer seeks mutual defense obligations for direct IP infringement."
        );

        var record = engine.NegotiateClause(request);

        Assert.NotNull(record);
        Assert.Equal(35.0, record.DeviationRiskScore);
        Assert.Equal("ACCEPTABLE_COMPROMISE", record.RiskClassification);
    }

    [Fact]
    public void ApproveDeviation_UnknownClauseId_StillGeneratesSealAndApproval()
    {
        var engine = new ContractRedlineEngine();
        var request = new ApproveDeviationRequest(
            "RED-UNKNOWN-999",
            "Elena Rostova (General Counsel)",
            "Special waiver for sandbox partnership"
        );

        var approval = engine.ApproveDeviation(request);

        Assert.NotNull(approval);
        Assert.Equal("RED-UNKNOWN-999", approval.ClauseId);
        Assert.StartsWith("SEAL-SHA256-", approval.CryptographicAuditSeal);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentApprovals, a => a.ApprovalId == approval.ApprovalId);
    }

    [Fact]
    public void NegotiateClause_LimitationOfLiability_AssignsScore45AndAcceptableCompromise()
    {
        var engine = new ContractRedlineEngine();
        var request = new NegotiateClauseRequest(
            "MSA-2026-NORTHSTAR",
            "LIMITATION_OF_LIABILITY",
            "Super-cap of 3x annual fees for data incident indemnity"
        );

        var record = engine.NegotiateClause(request);

        Assert.NotNull(record);
        Assert.Equal(45.0, record.DeviationRiskScore);
        Assert.Equal("ACCEPTABLE_COMPROMISE", record.RiskClassification);
        Assert.Equal("COMPROMISE_OFFERED", record.Status);
    }

    [Fact]
    public void ApproveDeviation_GeneratesDeterministicSha256SealPrefix()
    {
        var engine = new ContractRedlineEngine();
        var clauseId = "RED-LOL-001";
        var counsel = "General Legal Counsel";
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(clauseId + counsel)))[..12];

        var request = new ApproveDeviationRequest(clauseId, counsel, "Accepted business rationale");
        var approval = engine.ApproveDeviation(request);

        Assert.NotNull(approval);
        Assert.Equal($"SEAL-SHA256-{expectedHash}", approval.CryptographicAuditSeal);
    }
}
