using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Funnels;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Funnels;

public class AutonomousFunnelPipelineTests
{
    private static SGrowingDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(databaseName: $"AutonomousFunnelPipelineDb_{Guid.NewGuid()}")
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task GenerateFunnelAsync_Full15StepPipeline_GeneratesValidFunnelSpecification()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        var request = new AutonomousFunnelGenerateRequest(
            ConceptPrompt: "Handmade Vintage Leather Travel Backpack",
            ProductSku: "SKU-LEATHER-BP-01",
            TargetAudience: "Discerning travelers and remote professionals in North Africa",
            PricePoint: 89.00m,
            Currency: "TND"
        );

        // Act
        var spec = await service.GenerateFunnelAsync(tenantId, request);

        // Assert - Overall Spec
        spec.Should().NotBeNull();
        spec.TenantId.Should().Be(tenantId);
        spec.Name.Should().Contain("Leather Travel Backpack");
        spec.Slug.Should().StartWith("handmade-vintage-lea-");
        spec.Status.Should().Be("Draft");
        spec.IsPublished.Should().BeFalse();
        spec.LiveUrl.Should().Contain(spec.Slug);
        spec.PerformanceScore.Should().BeGreaterThanOrEqualTo(90);
        spec.SeoScore.Should().Be(100);
        spec.AccessibilityScore.Should().BeGreaterThanOrEqualTo(95);

        // Step 1: Product Entity
        spec.Product.Should().NotBeNull();
        spec.Product.Sku.Should().Be("SKU-LEATHER-BP-01");
        spec.Product.BasePrice.Should().Be(89.00m);
        spec.Product.Currency.Should().Be("TND");
        spec.Product.Category.Should().Be("Fashion & Leather Goods");
        spec.Product.Specifications.Should().ContainKey("Build Material");

        // Step 2: Target Audience Taxonomy
        spec.Audience.Should().NotBeNull();
        spec.Audience.PainPoints.Should().HaveCountGreaterThanOrEqualTo(3);
        spec.Audience.EmotionalDesires.Should().HaveCountGreaterThanOrEqualTo(3);
        spec.Audience.Objections.Should().HaveCountGreaterThanOrEqualTo(3);

        // Step 3: Value Proposition Angle
        spec.ValueProp.Should().NotBeNull();
        spec.ValueProp.RiskFreeAngle.Should().Contain("Cash on Delivery");
        spec.ValueProp.QualityAngle.Should().NotBeNullOrWhiteSpace();

        // Step 4: AIDA / PAS Copywriting
        spec.Copywriting.Should().NotBeNull();
        spec.Copywriting.AttentionHeadline.Should().Contain(spec.Product.Title);
        spec.Copywriting.PasProblem.Should().NotBeNullOrWhiteSpace();
        spec.Copywriting.DesireBullets.Should().HaveCount(5);
        spec.Copywriting.ActionCta.Should().Contain("Order Now");

        // Step 5: Tiered Quantity Breaks
        spec.QuantityBreaks.Should().HaveCount(3);
        var tier1 = spec.QuantityBreaks[0];
        var tier2 = spec.QuantityBreaks[1];
        var tier3 = spec.QuantityBreaks[2];

        tier1.Quantity.Should().Be(1);
        tier1.TotalPrice.Should().Be(89.00m);
        tier1.DiscountPercentage.Should().Be(0);

        tier2.Quantity.Should().Be(2);
        tier2.DiscountPercentage.Should().Be(15);
        tier2.IsMostPopular.Should().BeTrue();
        tier2.TotalPrice.Should().Be(Math.Round(89.00m * 2 * 0.85m, 2));

        tier3.Quantity.Should().Be(3);
        tier3.DiscountPercentage.Should().Be(25);
        tier3.IsBestValue.Should().BeTrue();
        tier3.TotalPrice.Should().Be(Math.Round(89.00m * 3 * 0.75m, 2));

        // Step 6: Order Bump Specification & EV
        spec.OrderBump.Should().NotBeNull();
        spec.OrderBump.BumpPrice.Should().BeGreaterThan(0m);
        spec.OrderBump.RoiMetrics.Should().NotBeNull();
        spec.OrderBump.RoiMetrics.ExpectedValueBump.Should().BeGreaterThan(0m);

        // Step 7: Social Proof Reviews
        spec.SocialProof.Should().NotBeNull();
        spec.SocialProof.AverageRating.Should().Be(4.9);
        spec.SocialProof.Reviews.Should().HaveCountGreaterThanOrEqualTo(3);
        spec.SocialProof.Reviews.Should().Contain(r => r.Location == "Tunis");

        // Step 8: FAQs
        spec.Faqs.Should().HaveCount(5);
        spec.Faqs.Should().Contain(f => f.Question.Contains("Cash On Delivery"));

        // Step 9: Sticky Mobile Action Bar
        spec.StickyBar.IsEnabled.Should().BeTrue();
        spec.StickyBar.TargetAnchorId.Should().Be("cod-checkout-form");

        // Step 10: 1-Step COD Checkout Config
        spec.CheckoutConfig.Enabled.Should().BeTrue();
        spec.CheckoutConfig.DefaultPaymentMethod.Should().Be("Cash on Delivery (COD)");
        spec.CheckoutConfig.PhoneCountryPrefix.Should().Be("+216");
        spec.CheckoutConfig.AvailableGovernorates.Should().Contain("Tunis");
        spec.CheckoutConfig.AvailableGovernorates.Should().Contain("Sousse");
        spec.CheckoutConfig.AvailableGovernorates.Should().Contain("Sfax");

        // Step 11: Urgency & Scarcity
        spec.UrgencyConfig.CountdownMinutes.Should().Be(15);
        spec.UrgencyConfig.RemainingStockUnits.Should().Be(7);

        // Step 12: Declarative Liquid Sections
        spec.LiquidSections.Should().HaveCount(9);
        spec.LiquidSections.Should().Contain(s => s.Type == "announcement-bar");
        spec.LiquidSections.Should().Contain(s => s.Type == "hero-gallery");
        spec.LiquidSections.Should().Contain(s => s.Type == "quantity-breaks");
        spec.LiquidSections.Should().Contain(s => s.Type == "order-bump");
        spec.LiquidSections.Should().Contain(s => s.Type == "one-step-checkout");

        // Step 13: Pixel Events
        spec.PixelEvents.Should().HaveCount(3);
        spec.PixelEvents.Should().Contain(p => p.EventName == "ViewContent");
        spec.PixelEvents.Should().Contain(p => p.EventName == "InitiateCheckout");
        spec.PixelEvents.Should().Contain(p => p.EventName == "Purchase");

        // Step 14: Tokens
        spec.Tokens.PrimaryColor.Should().Be("#10b981");

        // Step 15: Pipeline Steps Validation
        spec.PipelineSteps.Should().HaveCount(15);
        for (int i = 0; i < 15; i++)
        {
            spec.PipelineSteps[i].StepNumber.Should().Be(i + 1);
            spec.PipelineSteps[i].Status.Should().Be("Completed");
            spec.PipelineSteps[i].DurationMs.Should().BeGreaterThan(0);
        }
    }

    [Theory]
    [InlineData(49.00, 14.00, 0.75, 0.28)]
    [InlineData(100.00, 25.00, 0.80, 0.30)]
    [InlineData(59.00, 19.00, 75.0, 25.0)] // Using percentage representation (> 1.0)
    public async Task EvaluateOrderBumpRoiAsync_CalculatesCorrectExpectedValue(
        decimal basePrice,
        decimal bumpPrice,
        decimal bumpMarginPct,
        double takeRate)
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        // Act
        var result = await service.EvaluateOrderBumpRoiAsync(tenantId, basePrice, bumpPrice, bumpMarginPct, takeRate);

        // Normalize expected parameters
        decimal expectedMargin = bumpMarginPct > 1.0m ? bumpMarginPct / 100m : bumpMarginPct;
        decimal expectedTakeRate = (decimal)(takeRate > 1.0 ? takeRate / 100.0 : takeRate);

        decimal expectedGross = Math.Round(expectedTakeRate * bumpPrice * expectedMargin, 4);
        decimal expectedFriction = Math.Round(basePrice * 0.005m, 4);
        decimal expectedEv = Math.Round(expectedGross - expectedFriction, 4);

        // Assert
        result.Should().NotBeNull();
        result.BasePrice.Should().Be(basePrice);
        result.BumpPrice.Should().Be(bumpPrice);
        result.GrossBumpContribution.Should().Be(expectedGross);
        result.CheckoutFrictionCost.Should().Be(expectedFriction);
        result.ExpectedValueBump.Should().Be(expectedEv);
        result.IsRecommended.Should().BeTrue();
        result.NetAovLift.Should().Be(expectedEv);
        result.NetAovLiftPct.Should().Be(Math.Round((expectedEv / basePrice) * 100m, 2));
        result.StrategicAdvice.Should().Contain("Strongly Recommended");
    }

    [Fact]
    public async Task EvaluateOrderBumpRoiAsync_WhenNegativeMarginOrZeroBump_IsNotRecommended()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        // Act: bump price is 0
        var zeroBumpResult = await service.EvaluateOrderBumpRoiAsync(tenantId, 100m, 0m, 0.75m, 0.25);

        // Assert
        zeroBumpResult.ExpectedValueBump.Should().BeLessThanOrEqualTo(0m);
        zeroBumpResult.IsRecommended.Should().BeFalse();
        zeroBumpResult.StrategicAdvice.Should().Contain("Not Recommended");
    }

    [Fact]
    public async Task GetJobStatusAsync_TracksProgressAndStepHistory()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        var request = new AutonomousFunnelGenerateRequest("Wireless ANC Noise Cancelling Headphones", "SKU-ANC-01", null, 129.00m, "TND");
        var spec = await service.GenerateFunnelAsync(tenantId, request);

        // Act
        var status = await service.GetJobStatusAsync(tenantId, spec.JobId);

        // Assert
        status.Should().NotBeNull();
        status!.JobId.Should().Be(spec.JobId);
        status.FunnelId.Should().Be(spec.FunnelId);
        status.Status.Should().Be("Completed");
        status.ProgressPercentage.Should().Be(100);
        status.CurrentStepNumber.Should().Be(15);
        status.CurrentStepName.Should().Be("Instant Publishing State & Unique Slug Assignment");
        status.Steps.Should().HaveCount(15);
        status.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CompileLiquidFunnelTemplateAsync_ResolvesAllTokensIntoFullHtml()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        var request = new AutonomousFunnelGenerateRequest("Smart Fitness Activity Tracker", "SKU-FIT-01", null, 65.00m, "TND");
        var spec = await service.GenerateFunnelAsync(tenantId, request);

        // Act
        var compiled = await service.CompileLiquidFunnelTemplateAsync(tenantId, spec.FunnelId);

        // Assert
        compiled.Should().NotBeNull();
        compiled.FunnelId.Should().Be(spec.FunnelId);
        compiled.TenantId.Should().Be(tenantId);
        compiled.CompiledSections.Should().HaveCount(9);

        // Check full HTML content
        compiled.FullCompiledHtml.Should().Contain("<!DOCTYPE html>");
        compiled.FullCompiledHtml.Should().Contain("dir=\"ltr\"");
        compiled.FullCompiledHtml.Should().Contain("Smart Fitness Activity Tracker");
        compiled.FullCompiledHtml.Should().Contain("65.00 TND");
        compiled.FullCompiledHtml.Should().Contain("cod-checkout-form");
        compiled.FullCompiledHtml.Should().Contain("urgency-timer-countdown");
        compiled.FullCompiledHtml.Should().Contain("toggleOrderBump");
    }

    [Fact]
    public async Task PublishFunnelAsync_TransitionsStateToPublishedAndAssignsLiveUrl()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        var request = new AutonomousFunnelGenerateRequest("Organic Coffee Beans", "SKU-COFFEE-01", null, 28.00m, "TND");
        var spec = await service.GenerateFunnelAsync(tenantId, request);

        // Initial state
        spec.IsPublished.Should().BeFalse();
        spec.Status.Should().Be("Draft");

        // Act
        var publishResult = await service.PublishFunnelAsync(tenantId, spec.FunnelId);

        // Assert
        publishResult.Should().NotBeNull();
        publishResult.FunnelId.Should().Be(spec.FunnelId);
        publishResult.IsPublished.Should().BeTrue();
        publishResult.LiveUrl.Should().Be($"https://buy.sgrowing.io/f/{spec.Slug}");
        publishResult.PublishedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify updated spec via GetFunnelByIdAsync
        var updatedSpec = await service.GetFunnelByIdAsync(tenantId, spec.FunnelId);
        updatedSpec.Should().NotBeNull();
        updatedSpec!.IsPublished.Should().BeTrue();
        updatedSpec.Status.Should().Be("Published");
    }

    [Fact]
    public async Task GenerateFunnelAsync_MultipleExecutionsWithSamePrompt_GeneratesDistinctUniqueSlugs()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        var request1 = new AutonomousFunnelGenerateRequest("Luxury Mechanical Automatic Watch", null, null, 199.00m, "TND");
        var request2 = new AutonomousFunnelGenerateRequest("Luxury Mechanical Automatic Watch", null, null, 199.00m, "TND");

        // Act
        var spec1 = await service.GenerateFunnelAsync(tenantId, request1);
        var spec2 = await service.GenerateFunnelAsync(tenantId, request2);

        // Assert
        spec1.Slug.Should().NotBe(spec2.Slug);
        spec1.FunnelId.Should().NotBe(spec2.FunnelId);
    }

    [Fact]
    public async Task GenerateFunnelAsync_EdgeCase_MissingSku_AutoGeneratesSku()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        var request = new AutonomousFunnelGenerateRequest("Minimalist Aluminum Laptop Stand", ProductSku: null, TargetAudience: null, PricePoint: 45.00m, Currency: "TND");

        // Act
        var spec = await service.GenerateFunnelAsync(tenantId, request);

        // Assert
        spec.Product.Sku.Should().StartWith("SKU-AUTO-");
        spec.Product.Sku.Length.Should().BeGreaterThan(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15.50)]
    public async Task GenerateFunnelAsync_EdgeCase_ZeroOrNegativePrice_ThrowsArgumentException(decimal price)
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        var request = new AutonomousFunnelGenerateRequest("Sample Product", "SKU-001", null, price, "TND");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateFunnelAsync(tenantId, request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INVALID_CURRENCY")]
    [InlineData("XX")]
    public async Task GenerateFunnelAsync_EdgeCase_InvalidCurrency_ThrowsArgumentException(string invalidCurrency)
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        var request = new AutonomousFunnelGenerateRequest("Sample Product", "SKU-001", null, 50.00m, invalidCurrency);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateFunnelAsync(tenantId, request));
    }

    [Fact]
    public async Task GetFunnelByIdAsync_NonexistentId_ReturnsNull()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        // Act
        var result = await service.GetFunnelByIdAsync(tenantId, Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task PublishFunnelAsync_NonexistentId_ThrowsKeyNotFoundException()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var service = new AutonomousFunnelBuilderService(db);
        var tenantId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.PublishFunnelAsync(tenantId, Guid.NewGuid()));
    }
}
