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
    public void EvaluatePagePersonalization_MatchesHealthcareSegment_With_Hipaa_Proof()
    {
        var request = new EvaluatePersonalizationRequest(
            "192.0.2.88",
            "moderna.com",
            "Healthcare",
            850,
            new[] { "HIPAA", "FDA", "Clinical" },
            "/solutions/healthcare"
        );

        var result = _engine.EvaluatePagePersonalization(request);

        result.Should().NotBeNull();
        result.MatchedSegmentId.Should().Be("SEG-HLT-02");
        result.TailoredHeroHeadline.Should().Contain("Zero-Trust Patient Acquisition");
        result.CallToActionText.Should().Contain("HIPAA Compliant");
        result.TailoredCustomerProofLogos.Should().Contain("Moderna");
        result.TailoredPricingTier.Should().Be("Enterprise Sovereign Custom"); // > 500
    }

    [Fact]
    public void EvaluatePagePersonalization_MidMarketHeadcount_Assigns_Growth_Scale_Tier()
    {
        var request = new EvaluatePersonalizationRequest(
            "203.0.113.12",
            "startup-grow.io",
            "Technology",
            120, // <= 500
            new[] { "PLG", "Self-Serve" },
            "/pricing"
        );

        var result = _engine.EvaluatePagePersonalization(request);

        result.Should().NotBeNull();
        result.TailoredPricingTier.Should().Be("Growth Scale Tier");
        result.TailoredCustomerProofLogos.Should().Contain("Vercel");
    }

    [Fact]
    public void EvaluatePagePersonalization_UnknownIndustry_Gracefully_Defaults_To_First_Segment()
    {
        var request = new EvaluatePersonalizationRequest(
            "10.0.0.1",
            "unknown-corp.biz",
            "Unknown Sector XYZ",
            200,
            new string[0],
            "/landing"
        );

        var result = _engine.EvaluatePagePersonalization(request);

        result.Should().NotBeNull();
        result.MatchedSegmentId.Should().NotBeNullOrEmpty();
        result.TailoredHeroHeadline.Should().NotBeNullOrEmpty();
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
        receipt.ConversionType.Should().Be("DemoScheduled");
    }
}
