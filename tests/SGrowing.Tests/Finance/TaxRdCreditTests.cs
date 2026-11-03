using SGrowing.Api.Modules.Finance.TaxRdCredit;
using Xunit;

namespace SGrowing.Tests.Finance;

public class TaxRdCreditTests
{
    [Fact]
    public void GetOverview_AggregatesQreAndCalculatesTaxCredit()
    {
        var engine = new TaxRdCreditLedgerEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.Equal("2026", overview.FiscalYear);
        Assert.True(overview.TotalQualifyingProjectsCount >= 2);
        Assert.True(overview.TotalCompanyQreUsd > 2000000.00m);
        Assert.True(overview.TotalEstimatedTaxCreditUsd > 200000.00m);
        Assert.True(overview.PayrollTaxOffsetClaimableUsd > 0);
        Assert.NotEmpty(overview.Projects);
        Assert.NotEmpty(overview.RecentClaims);
    }

    [Fact]
    public void QualifyProject_EvaluatesFourPartTestAndCalculatesTotalQre()
    {
        var engine = new TaxRdCreditLedgerEngine();
        var request = new QualifyProjectRequest(
            "Ultra-Low-Latency WebAssembly Edge CDN Kernel",
            "Eliminating micro-VM cold starts under 5ms using memory snapshot paging",
            850000.00m,
            210000.00m,
            true
        );

        var project = engine.QualifyProject(request);

        Assert.NotNull(project);
        Assert.StartsWith("PRJ-RD-", project.ProjectId);
        Assert.Equal(1060000.00m, project.TotalQreUsd);
        Assert.Equal("FULLY_QUALIFIED", project.FourPartTestStatus);

        var overview = engine.GetOverview();
        Assert.Contains(overview.Projects, p => p.ProjectId == project.ProjectId);
    }

    [Fact]
    public void ComputeCredit_CalculatesAscCreditAndGeneratesAuditBinder()
    {
        var engine = new TaxRdCreditLedgerEngine();
        var request = new ComputeCreditRequest(
            "2026",
            1000000.00m
        );

        var claim = engine.ComputeCredit(request);

        Assert.NotNull(claim);
        Assert.StartsWith("CLM-6765-", claim.ClaimId);
        Assert.True(claim.AscCreditCalculatedUsd > 0);
        Assert.StartsWith("BINDER-SHA256-", claim.AuditBinderHash);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentClaims, c => c.ClaimId == claim.ClaimId);
    }
}
