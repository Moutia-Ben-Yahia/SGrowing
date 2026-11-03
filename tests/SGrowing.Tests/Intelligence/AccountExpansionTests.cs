using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Intelligence.AccountExpansion;
using Xunit;

namespace SGrowing.Tests.Intelligence;

public class AccountExpansionTests
{
    private readonly AccountExpansionEngine _engine = new();

    [Fact]
    public void GetOpportunities_ReturnsSortedByPropensity()
    {
        var list = _engine.GetOpportunities();

        list.Should().NotBeNull();
        list.Should().HaveCount(3);
        list.First().OpportunityId.Should().Be("opp_exp_101");
        list.First().ExpansionPropensityScore.Should().Be(94);
        list.First().AccountName.Should().Be("Vertex BioPharma Labs");
    }

    [Fact]
    public void GenerateQuote_AppliesDiscountAndMarksGenerated()
    {
        var oppId = "opp_exp_101";
        var request = new GenerateExpansionQuoteRequest(oppId, DiscountPercentage: 10m);

        var quote = _engine.GenerateQuote(request);

        quote.IsQuoteGenerated.Should().BeTrue();
        // 65000 - 10% = 58500
        quote.ProjectedExpansionArrUsd.Should().Be(58500m);
    }

    [Fact]
    public void IngestUtilization_UpdatesMetricAndBoostsPropensity()
    {
        var request = new IngestUtilizationSignalRequest("tenant_nordic_fintech", "Vault Rotations / Day", 50);

        var updated = _engine.IngestUtilization(request);

        updated.Should().NotBeNull();
        var metric = updated.SaturatedMetrics.First(m => m.MetricName == "Vault Rotations / Day");
        metric.CurrentUsed.Should().Be(970); // 920 + 50
        metric.UtilizationPct.Should().Be(97.0);
        updated.ExpansionPropensityScore.Should().BeGreaterThan(88);
    }
}
