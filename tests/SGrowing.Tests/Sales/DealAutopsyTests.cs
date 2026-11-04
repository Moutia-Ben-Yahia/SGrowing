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

    [Fact]
    public void AnalyzeLostDeal_CompetitorIncumbent_IdentifiesCompetitorDisplacement()
    {
        var record = _engine.AnalyzeLostDeal(
            "OPP-TEST-002",
            "MegaBank International",
            480000.0,
            "Marcus Vance",
            "Customer renewed 3-year contract with existing Salesforce and HubSpot vendor stack."
        );

        Assert.NotNull(record);
        Assert.Equal(LossReasonCategory.CompetitorDisplacement, record.PrimaryLossCategory);
        Assert.Contains(record.DetectedSignals, s => s.FactorName == "Incumbent Vendor Lock-in");
    }

    [Fact]
    public void AnalyzeLostDeal_SecurityAndLegal_IdentifiesSecurityBlocker()
    {
        var record = _engine.AnalyzeLostDeal(
            "OPP-TEST-003",
            "HealthNet Shield",
            320000.0,
            "Sarah Jenkins",
            "Legal and compliance team refused public cloud multi-tenancy due to strict HIPAA data sovereignty."
        );

        Assert.NotNull(record);
        Assert.Equal(LossReasonCategory.SecurityAndComplianceBlocker, record.PrimaryLossCategory);
        Assert.Contains(record.DetectedSignals, s => s.FactorName == "InfoSec & Compliance Roadblock");
    }

    [Fact]
    public void AnalyzeLostDeal_VagueStagnation_FallsBackToBudgetFreezeOrNoDecision()
    {
        var record = _engine.AnalyzeLostDeal(
            "OPP-TEST-004",
            "Starlight Media Group",
            95000.0,
            "Alexandra Hayes",
            "Internal reorganization occurred and communications went dark with no decision made."
        );

        Assert.NotNull(record);
        Assert.Equal(LossReasonCategory.BudgetFreezeOrNoDecision, record.PrimaryLossCategory);
        Assert.Contains(record.DetectedSignals, s => s.FactorName == "Project Indefinitely Shelved");
    }

    [Theory]
    [InlineData(LossReasonCategory.CompetitorDisplacement, "Competitive Replacement Matrix")]
    [InlineData(LossReasonCategory.PricingAndTcoMisalignment, "Consumption-Based Tier")]
    [InlineData(LossReasonCategory.ChampionDepartureOrReorg, "Executive Sponsor CAB Invitation")]
    public void GenerateRecoveryPlaybook_ProducesAppropriateRecoveryStrategy(LossReasonCategory category, string expectedFragment)
    {
        var playbook = _engine.GenerateRecoveryPlaybook("OPP-THEORY-01", category);

        Assert.NotNull(playbook);
        Assert.Contains(expectedFragment, playbook.RecommendedRecoveryStrategy);
        Assert.Equal(60, playbook.ReEngagementCooldownDays);
    }
}
