using SGrowing.Api.Modules.Sales.ValueRealization;
using Xunit;

namespace SGrowing.Tests.Sales;

public class ValueRealizationTests
{
    [Fact]
    public void GetOverview_ReportsAccountsRoiAndDecks()
    {
        var engine = new ValueRealizationEngine();

        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.MonitoredEnterpriseAccountsCount >= 2);
        Assert.True(overview.AggregateRealizedRoiDollars > 1_000_000m);
        Assert.True(overview.AverageAccountNpsHealthScore > 90.0);
        Assert.True(overview.ScheduledQbrPresentationsCount >= 1);
        Assert.NotEmpty(overview.TopRealizedMetrics);
        Assert.NotEmpty(overview.RecentExecutiveDecks);
    }

    [Fact]
    public void GenerateQbrDeck_CreatesStructuredExecutiveSlides()
    {
        var engine = new ValueRealizationEngine();
        var request = new GenerateQbrDeckRequest(
            "Acme Global Logistics",
            "2026-Q3",
            3_500_000m,
            97.0,
            650_000m
        );

        var deck = engine.GenerateQbrDeck(request);

        Assert.NotNull(deck);
        Assert.StartsWith("QBR-DECK-", deck.DeckId);
        Assert.Equal("Acme Global Logistics", deck.CustomerAccountName);
        Assert.Equal("2026-Q3", deck.FiscalQuarter);
        Assert.Equal(3_500_000m, deck.TotalRealizedRoiDollars);
        Assert.Equal(3, deck.GeneratedSlides.Count);

        var overview = engine.GetOverview();
        Assert.Contains(overview.RecentExecutiveDecks, d => d.DeckId == deck.DeckId);
    }

    [Fact]
    public void AttestRoi_CalculatesVerifiedSavingsAndMultiplier()
    {
        var engine = new ValueRealizationEngine();
        var request = new AttestRoiRequest(
            "Acme Global Logistics",
            "COST_REDUCTION",
            2_000_000m,
            500_000m,
            "INVOICE_CLEARANCE_AUDIT"
        );

        var metric = engine.AttestRoi(request);

        Assert.NotNull(metric);
        Assert.StartsWith("VAL-MET-", metric.MetricId);
        Assert.Equal("Acme Global Logistics", metric.CustomerAccountName);
        Assert.Equal(1_500_000m, metric.AnnualizedRoiDollars);
        Assert.Equal(4.0, metric.RealizationMultiplier);

        var overview = engine.GetOverview();
        Assert.Contains(overview.TopRealizedMetrics, m => m.MetricId == metric.MetricId);
    }

    [Fact]
    public void AttestRoi_WhenRealizedCurrentValueExceedsBaseline_ReturnsZeroSavings()
    {
        var engine = new ValueRealizationEngine();
        var request = new AttestRoiRequest(
            "Acme Global Logistics",
            "COST_OVERRUN",
            500_000m,
            800_000m, // current value > baseline
            "AUDIT"
        );

        var metric = engine.AttestRoi(request);

        Assert.Equal(0m, metric.AnnualizedRoiDollars); // Math.Max(0, 500k - 800k) = 0
        Assert.Equal(0.6, metric.RealizationMultiplier); // 500 / 800 = 0.625 -> 0.6
    }

    [Fact]
    public void AttestRoi_WhenRealizedCurrentValueIsZero_ReturnsMultiplierOne()
    {
        var engine = new ValueRealizationEngine();
        var request = new AttestRoiRequest(
            "Acme Global Logistics",
            "EFFICIENCY",
            100_000m,
            0m,
            "AUDIT"
        );

        var metric = engine.AttestRoi(request);

        Assert.Equal(100_000m, metric.AnnualizedRoiDollars);
        Assert.Equal(1.0, metric.RealizationMultiplier);
    }
}
