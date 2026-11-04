using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Funnels.MabOptimizer;
using Xunit;

namespace SGrowing.Tests.Funnels;

public class MabOptimizerTests
{
    private readonly MabOptimizerEngine _engine = new(seed: 42);

    [Fact]
    public void CreateFunnel_InitializesBlocksAndBalancedVariants()
    {
        // Arrange
        var request = new CreateFunnelRequest(
            TenantId: "tenant_growth_funnel",
            FunnelId: "funnel_onboarding",
            Path: "/get-started",
            Title: "Get Started with Growth OS",
            SeoMetaDescription: "High-velocity AI-native growth operating system.",
            InitialBlocks: new List<FunnelBlock>
            {
                new("blk_1", BlockType.HeroBanner, "Scale Revenue Autonomously", "AI Agents that sell", new(), 1),
                new("blk_2", BlockType.PricingMatrix, "Flexible Tiers", "Built for any scale", new(), 2)
            }
        );

        // Act
        var funnel = _engine.CreateFunnel(request);

        // Assert
        funnel.Should().NotBeNull();
        funnel.TenantId.Should().Be("tenant_growth_funnel");
        funnel.FunnelId.Should().Be("funnel_onboarding");
        funnel.Blocks.Should().HaveCount(2);
        funnel.Variants.Should().HaveCount(2);
        funnel.Variants.First().TrafficWeight.Should().Be(0.5);
    }

    [Fact]
    public void CreateFunnel_CustomVariantNames_InitializesRequestedVariants()
    {
        // Arrange
        var request = new CreateFunnelRequest(
            TenantId: "tenant_custom_variants",
            FunnelId: "funnel_custom_split",
            Path: "/signup",
            Title: "Custom Signup Split",
            SeoMetaDescription: "Three-way split",
            InitialBlocks: new List<FunnelBlock>(),
            InitialVariantNames: new List<string> { "Control", "Benefit Headline", "Social Proof Leading" }
        );

        // Act
        var funnel = _engine.CreateFunnel(request);

        // Assert
        funnel.Variants.Should().HaveCount(3);
        funnel.Variants[0].Name.Should().Be("Control");
        funnel.Variants[1].Name.Should().Be("Benefit Headline");
        funnel.Variants[2].Name.Should().Be("Social Proof Leading");
        funnel.Variants[0].TrafficWeight.Should().BeApproximately(0.3333, 0.001);
    }

    [Fact]
    public void UpdateBlocks_ReordersAndPersistsBlocks()
    {
        // Arrange
        var request = new CreateFunnelRequest(
            TenantId: "tenant_reorder",
            FunnelId: "funnel_order_test",
            Path: "/pricing",
            Title: "Pricing Funnel",
            SeoMetaDescription: "Pricing plans",
            InitialBlocks: new List<FunnelBlock>
            {
                new("b1", BlockType.HeroBanner, "Hero", "", new(), 1)
            }
        );
        _engine.CreateFunnel(request);

        var newBlocks = new List<FunnelBlock>
        {
            new("b2", BlockType.LeadCaptureForm, "Capture", "", new(), 1),
            new("b1", BlockType.HeroBanner, "Hero", "", new(), 2)
        };

        // Act
        var updated = _engine.UpdateBlocks(new UpdateBlocksRequest("tenant_reorder", "funnel_order_test", newBlocks));

        // Assert
        updated.Blocks.Should().HaveCount(2);
        updated.Blocks[0].Id.Should().Be("b2");
        updated.Blocks[1].Id.Should().Be("b1");
    }

    [Fact]
    public void UpdateBlocks_ThrowsKeyNotFoundException_WhenFunnelDoesNotExist()
    {
        // Arrange
        var req = new UpdateBlocksRequest("invalid_tenant", "invalid_funnel", new List<FunnelBlock>());

        // Act
        var act = () => _engine.UpdateBlocks(req);

        // Assert
        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*invalid_funnel*");
    }

    [Fact]
    public void RecordImpressionAndConversion_DynamicallyShiftsTrafficWeights()
    {
        // Arrange
        const string tenantId = "tenant_mab_shift";
        const string funnelId = "funnel_saas_lead";

        _engine.CreateFunnel(new CreateFunnelRequest(
            TenantId: tenantId,
            FunnelId: funnelId,
            Path: "/demo",
            Title: "Book a Demo",
            SeoMetaDescription: "Schedule demo",
            InitialBlocks: new List<FunnelBlock>()
        ));

        // Act: Generate 100 impressions for var_1 with 25 conversions (25% CR)
        // and 100 impressions for var_2 with 5 conversions (5% CR)
        for (var i = 0; i < 100; i++)
        {
            _engine.RecordImpression(new RecordImpressionRequest(tenantId, funnelId, "var_1"));
            if (i < 25)
            {
                _engine.RecordConversion(new RecordConversionRequest(tenantId, funnelId, "var_1", 100m));
            }

            _engine.RecordImpression(new RecordImpressionRequest(tenantId, funnelId, "var_2"));
            if (i < 5)
            {
                _engine.RecordConversion(new RecordConversionRequest(tenantId, funnelId, "var_2", 100m));
            }
        }

        var report = _engine.GetOptimizationReport(tenantId, funnelId);

        // Assert
        report.LeadingVariantId.Should().Be("var_1");
        report.ConfidenceLevel.Should().BeGreaterThan(90.0);
        report.UpliftPercent.Should().BeGreaterThan(100.0);

        var var1 = report.Variants.First(v => v.VariantId == "var_1");
        var var2 = report.Variants.First(v => v.VariantId == "var_2");
        var1.ConversionRate.Should().Be(0.25);
        var2.ConversionRate.Should().Be(0.05);
        var1.TrafficWeight.Should().BeGreaterThan(var2.TrafficWeight);
    }

    [Fact]
    public void SelectVariant_ThompsonSampling_ReturnsVariantWithNonZeroWeight()
    {
        // Arrange
        const string tenantId = "tenant_select_variant";
        const string funnelId = "funnel_select_test";

        _engine.CreateFunnel(new CreateFunnelRequest(
            TenantId: tenantId,
            FunnelId: funnelId,
            Path: "/compare",
            Title: "Compare Plans",
            SeoMetaDescription: "Plan comparison",
            InitialBlocks: new List<FunnelBlock>()
        ));

        // Act
        var selected = _engine.SelectVariant(tenantId, funnelId);

        // Assert
        selected.Should().NotBeNull();
        selected.VariantId.Should().StartWith("var_");
        selected.TrafficWeight.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RecordImpression_ThrowsKeyNotFoundException_WhenFunnelDoesNotExist()
    {
        // Act
        var act = () => _engine.RecordImpression(new RecordImpressionRequest("tenant_x", "funnel_y", "var_1"));

        // Assert
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GetOptimizationReport_ThrowsKeyNotFoundException_WhenFunnelNotFound()
    {
        // Act
        var act = () => _engine.GetOptimizationReport("nonexistent_tenant", "nonexistent_funnel");

        // Assert
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void GenerateSeoJsonLd_ProducesValidSchemaOrgMetadata()
    {
        // Arrange
        var page = _engine.CreateFunnel(new CreateFunnelRequest(
            TenantId: "tenant_seo",
            FunnelId: "funnel_seo_test",
            Path: "/enterprise",
            Title: "Enterprise Growth OS",
            SeoMetaDescription: "Enterprise grade AI growth engine",
            InitialBlocks: new List<FunnelBlock>()
        ));

        // Act
        var jsonLd = _engine.GenerateSeoJsonLd(page);

        // Assert
        jsonLd.Should().Contain("\"@context\": \"https://schema.org\"");
        jsonLd.Should().Contain("\"@type\": \"WebPage\"");
        jsonLd.Should().Contain("\"name\": \"Enterprise Growth OS\"");
        jsonLd.Should().Contain("https://app.sgrowing.io/enterprise");
    }
}
