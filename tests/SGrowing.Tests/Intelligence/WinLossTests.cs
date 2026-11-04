using System.Collections.Generic;
using System.Linq;
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
    public void GetPostMortemByDealId_ReturnsNullForUnknownDeal()
    {
        var pm = _engine.GetPostMortemByDealId("unknown_deal_999");
        Assert.Null(pm);
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
    public void AnalyzeDeal_IdentifiesSecurityHurdle()
    {
        var request = new AnalyzeDealRequest(
            "deal_sec_review",
            "Health Shield",
            150000m,
            DealOutcome.LostToCompetitor,
            "Deal stalled because CISO demanded HIPAA compliance and security audit certifications early in evaluation.",
            "LegacyCorp"
        );

        var result = _engine.AnalyzeDeal(request);

        Assert.NotNull(result);
        Assert.Contains(result.RootCauses, c => c.Category == "SecurityHurdle");
        Assert.Contains(result.FeatureGapsTagged, g => g.Contains("CISO"));
    }

    [Fact]
    public void AnalyzeDeal_HandlesGeneralOutcomeWhenNoSpecificKeywordsPresent()
    {
        var request = new AnalyzeDealRequest(
            "deal_general_won",
            "Standard Enterprises",
            60000m,
            DealOutcome.WonClosed,
            "Smooth sales cycle with executive sponsor alignment.",
            null
        );

        var result = _engine.AnalyzeDeal(request);

        Assert.NotNull(result);
        Assert.Contains(result.RootCauses, c => c.Category == "General");
        Assert.Empty(result.CompetitorInsights);
    }

    [Fact]
    public void GetAllPostMortems_ReturnsAllHistoricalDeals_SortedDescending()
    {
        var all = _engine.GetAllPostMortems();

        Assert.NotNull(all);
        Assert.True(all.Count >= 2);
        Assert.True(all.First().AnalyzedAtUtc >= all.Last().AnalyzedAtUtc);
    }

    [Fact]
    public void AnalyzeDeal_WonAgainstCompetitor_CapturesCompetitorWeaknessTactic()
    {
        var request = new AnalyzeDealRequest(
            "deal_won_enterprise_99",
            "Apex Logistics",
            300000m,
            DealOutcome.WonClosed,
            "Customer loved our native real-time sync and transparent pricing. Replaced bloated vendor.",
            "LegacyVendor"
        );

        var result = _engine.AnalyzeDeal(request);

        Assert.NotNull(result);
        Assert.Equal(DealOutcome.WonClosed, result.Outcome);
        Assert.Single(result.CompetitorInsights);
        Assert.Equal("LegacyVendor", result.CompetitorInsights[0].CompetitorName);
        Assert.Contains("suffered from slow voice latency", result.CompetitorInsights[0].ObservedTactic);
    }

    [Fact]
    public void GetPostMortemByDealId_IsCaseInsensitive_ReturnsPostMortem()
    {
        var pm = _engine.GetPostMortemByDealId("DEAL_OMEGA_401");

        Assert.NotNull(pm);
        Assert.Equal("Omega FinTech Corp", pm.CompanyName);
        Assert.Equal(DealOutcome.LostToCompetitor, pm.Outcome);
    }
}
