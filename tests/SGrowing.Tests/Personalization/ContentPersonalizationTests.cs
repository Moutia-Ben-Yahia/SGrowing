using System.Collections.Generic;
using FluentAssertions;
using SGrowing.Api.Modules.Personalization.ContentPersonalization;
using Xunit;

namespace SGrowing.Tests.Personalization;

public class ContentPersonalizationTests
{
    private readonly ContentPersonalizationEngine _engine = new();

    [Fact]
    public void GetOverview_ReturnsActiveSegmentsAndRules()
    {
        var overview = _engine.GetOverview();

        overview.Should().NotBeNull();
        overview.SegmentCount.Should().Be(3);
        overview.ActiveRulesCount.Should().Be(3);
        overview.AverageConversionLiftPct.Should().BeGreaterThan(10.0);
        overview.MonthlyPersonalizedImpressions.Should().BeGreaterThan(1000000);
        overview.Segments.Should().HaveCount(3);
        overview.ActiveRules.Should().HaveCount(3);
    }

    [Fact]
    public void EvaluatePagePersonalization_MatchesFintechSegmentAndSelectsTailoredHero()
    {
        var request = new EvaluatePersonalizationRequest(
            "198.51.100.24",
            "stripe.com",
            "Financial Services",
            1500,
            new[] { "SOC2", "Multi-Jurisdiction", "Ledger" },
            "/enterprise"
        );

        var result = _engine.EvaluatePagePersonalization(request);

        result.Should().NotBeNull();
        result.MatchedSegmentId.Should().Be("SEG-ENT-01");
        result.TailoredHeroHeadline.Should().Contain("Financial Institutions");
        result.TailoredCustomerProofLogos.Should().Contain("Stripe");
        result.TailoredPricingTier.Should().Be("Enterprise Sovereign Custom");
        result.EvaluationLatencyMs.Should().BeLessThan(50);
    }

    [Fact]
    public void RecordConversion_CreatesReceiptAndUpdatesWeights()
    {
        var request = new RecordConversionRequest(
            "DEC-SAMPLE-99",
            "RULE-FIN-01",
            "DemoScheduled",
            50000m
        );

        var receipt = _engine.RecordConversion(request);

        receipt.Should().NotBeNull();
        receipt.ReceiptId.Should().StartWith("REC-");
        receipt.AttributedValueUsd.Should().Be(50000m);
        receipt.UpdatedVariantWeight.Should().BeGreaterThan(0.9);
    }
}
