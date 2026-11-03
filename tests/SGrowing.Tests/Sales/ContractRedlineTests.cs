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
}
