using SGrowing.Api.Modules.Intelligence.WinLoss;
using Xunit;

namespace SGrowing.Tests.Intelligence;

public class WinLossTests
{
    private readonly WinLossEngine _engine = new();

    [Fact]
    public void SeededDeals_AreClassifiedWithCausesAndCompetitorInsights()
    {
        var lost = _engine.GetPostMortemByDealId("deal_omega_401");

        Assert.NotNull(lost);
        Assert.Equal(DealOutcome.LostToCompetitor, lost.Outcome);
        Assert.Contains(lost.RootCauses, c => c.Category == "IntegrationGap");
        Assert.Contains(lost.CompetitorInsights, ci => ci.CompetitorName == "Salesforce");
        Assert.NotEmpty(lost.FeatureGapsTagged);
    }

    [Fact]
    public void AnalyzeDeal_IdentifiesPricingResistance()
    {
        var request = new AnalyzeDealRequest(
            "deal_test_loss",
            "Budget Corp",
            80000m,
            DealOutcome.LostToCompetitor,
            "Customer stated we were too expensive and had serious price resistance during executive negotiation.",
            "CompetitorX"
        );

        var result = _engine.AnalyzeDeal(request);

        Assert.NotNull(result);
        Assert.Contains(result.RootCauses, c => c.Category == "Pricing");
    }

    [Fact]
    public void GetAllPostMortems_ReturnsAllHistoricalDeals()
    {
        var all = _engine.GetAllPostMortems();

        Assert.NotNull(all);
        Assert.True(all.Count >= 2);
    }
}
