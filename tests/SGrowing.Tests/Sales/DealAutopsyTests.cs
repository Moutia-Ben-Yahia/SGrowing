using SGrowing.Api.Modules.Sales.DealAutopsy;
using Xunit;

namespace SGrowing.Tests.Sales;

public sealed class DealAutopsyTests
{
    private readonly DealAutopsyEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsSeededAutopsiesAndPlaybooks()
    {
        var overview = _engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.TotalAutopsiesCompletedQuarter >= 1);
        Assert.True(overview.TotalLostPipelineValueUsd > 1000000.0);
        Assert.NotEmpty(overview.RecentAutopsies);
        Assert.NotEmpty(overview.ActivePlaybooks);
    }

    [Fact]
    public void AnalyzeLostDeal_PricingPushback_IdentifiesPricingCategoryAndSignal()
    {
        var record = _engine.AnalyzeLostDeal(
            "OPP-TEST-001",
            "OmniRetail Global",
            250000.0,
            "Sarah Jenkins",
            "Customer stated CFO enforced a strict budget freeze and our enterprise tier is too expensive compared to alternatives."
        );

        Assert.NotNull(record);
        Assert.StartsWith("AUTO-", record.AutopsyId);
        Assert.Equal(LossReasonCategory.PricingAndTcoMisalignment, record.PrimaryLossCategory);
        Assert.NotEmpty(record.DetectedSignals);
        Assert.Equal(0.92, record.AutopsyConfidenceScore);
    }

    [Fact]
    public void GenerateRecoveryPlaybook_ComplianceBlocker_RecommendsSovereignAttestation()
    {
        var playbook = _engine.GenerateRecoveryPlaybook("OPP-TEST-001", LossReasonCategory.SecurityAndComplianceBlocker);

        Assert.NotNull(playbook);
        Assert.StartsWith("PLAY-", playbook.PlaybookId);
        Assert.Contains("Sovereign Cluster Attestation", playbook.RecommendedRecoveryStrategy);
        Assert.True(playbook.ProbabilityOfPipelineRevival >= 0.60);
    }
}
