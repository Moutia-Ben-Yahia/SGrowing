using System;
using System.Collections.Generic;
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
        list.Should().BeInDescendingOrder(o => o.ExpansionPropensityScore);
    }

    [Fact]
    public void GetOpportunity_ReturnsOpportunityById()
    {
        var opp = _engine.GetOpportunity("opp_exp_102");

        opp.Should().NotBeNull();
        opp!.AccountName.Should().Be("Nordic Fintech AB");
        opp.CurrentArrUsd.Should().Be(85000m);
    }

    [Fact]
    public void GetOpportunity_ReturnsNullForUnknownId()
    {
        var opp = _engine.GetOpportunity("non_existent_opp_999");
        opp.Should().BeNull();
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
    public void GenerateQuote_ClampsDiscountBetweenZeroAndThirty()
    {
        var oppId = "opp_exp_102"; // base 40000m

        // Discount > 30 clamped to 30: 40000 * 0.7 = 28000
        var quoteOver = _engine.GenerateQuote(new GenerateExpansionQuoteRequest(oppId, DiscountPercentage: 50m));
        quoteOver.ProjectedExpansionArrUsd.Should().Be(28000m);

        // Discount < 0 clamped to 0: 28000 * 1.0 = 28000
        var quoteUnder = _engine.GenerateQuote(new GenerateExpansionQuoteRequest(oppId, DiscountPercentage: -10m));
        quoteUnder.ProjectedExpansionArrUsd.Should().Be(28000m);
    }

    [Fact]
    public void GenerateQuote_ThrowsWhenOpportunityNotFound()
    {
        var act = () => _engine.GenerateQuote(new GenerateExpansionQuoteRequest("missing_opp", 10m));
        act.Should().Throw<KeyNotFoundException>();
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

    [Fact]
    public void IngestUtilization_CreatesNewOpportunityForUnseededTenant()
    {
        var request = new IngestUtilizationSignalRequest("tenant_brand_new_corp", "API Calls / Day", 200);

        var created = _engine.IngestUtilization(request);

        created.Should().NotBeNull();
        created.TenantId.Should().Be("tenant_brand_new_corp");
        created.SaturatedMetrics.Should().Contain(m => m.MetricName == "API Calls / Day" && m.CurrentUsed == 200);
        created.ExpansionPropensityScore.Should().Be(80);
    }
}
