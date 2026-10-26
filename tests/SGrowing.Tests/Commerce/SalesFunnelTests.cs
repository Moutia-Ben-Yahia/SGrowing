using Microsoft.EntityFrameworkCore;
using SGrowing.Api.Data;
using SGrowing.Api.Modules.Funnels;
using SGrowing.Core.Entities;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class SalesFunnelTests
{
    private SGrowingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SGrowingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SGrowingDbContext(options);
    }

    [Fact]
    public async Task CreateFunnel_And_AddSteps_EstablishesConversionPipeline()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new SalesFunnelService(db);
        var tenantId = Guid.NewGuid();

        // Act
        var funnel = await service.CreateFunnelAsync(tenantId, "Summer Launch Funnel", "summer-sneakers", "Flagship campaign", "TND");
        var step1 = await service.AddFunnelStepAsync(tenantId, funnel.Id, "Landing Page", FunnelStepType.LandingPage, 1, "summer-sneakers");
        var step2 = await service.AddFunnelStepAsync(tenantId, funnel.Id, "Checkout Form", FunnelStepType.Checkout, 2, "checkout");
        var step3 = await service.AddFunnelStepAsync(tenantId, funnel.Id, "1-Click Upsell Modal", FunnelStepType.OneClickUpsell, 3, "upsell-belt");
        var step4 = await service.AddFunnelStepAsync(tenantId, funnel.Id, "Order Confirmation", FunnelStepType.ThankYouPage, 4, "thank-you");

        var steps = await service.GetFunnelStepsAsync(tenantId, funnel.Id);

        // Assert
        Assert.NotNull(funnel);
        Assert.Equal(4, steps.Count);
        Assert.Equal(FunnelStepType.LandingPage, steps[0].StepType);
        Assert.Equal(FunnelStepType.Checkout, steps[1].StepType);
        Assert.Equal(FunnelStepType.OneClickUpsell, steps[2].StepType);
        Assert.Equal(FunnelStepType.ThankYouPage, steps[3].StepType);
    }

    [Fact]
    public async Task OrderBump_And_OneClickUpsell_CalculatesDiscountsAndPricing()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new SalesFunnelService(db);
        var tenantId = Guid.NewGuid();

        var funnel = await service.CreateFunnelAsync(tenantId, "Watch Promo", "watch-promo");
        var checkoutStep = await service.AddFunnelStepAsync(tenantId, funnel.Id, "Checkout", FunnelStepType.Checkout, 1);
        var upsellStep = await service.AddFunnelStepAsync(tenantId, funnel.Id, "Upsell", FunnelStepType.OneClickUpsell, 2);

        // Act
        var bump = await service.CreateOrderBumpAsync(
            tenantId,
            funnel.Id,
            checkoutStep.Id,
            "Luxury Travel Case",
            "Protect your watch when traveling",
            "CASE-LEATHER-TRAVEL",
            50.0m,
            25.0m,
            "SPECIAL 50% OFF"
        );

        var upsell = await service.CreateOneClickUpsellAsync(
            tenantId,
            funnel.Id,
            upsellStep.Id,
            "Matching Steel Strap",
            "STRAP-STEEL-CHRONO",
            "Add an extra stainless steel bracelet",
            80.0m,
            40.0m,
            "https://cdn.sgrowing.io/straps/steel.png"
        );

        var bumps = await service.GetOrderBumpsAsync(tenantId, funnel.Id);
        var upsells = await service.GetOneClickUpsellsAsync(tenantId, funnel.Id);

        // Assert
        Assert.Single(bumps);
        Assert.Equal(25.0m, bump.DiscountedPrice);
        Assert.Equal(50.0m, bump.OriginalPrice);

        Assert.Single(upsells);
        Assert.Equal(40.0m, upsell.UpsellPrice);
        Assert.Equal(40.0m, upsell.SavingsAmount);
    }

    [Fact]
    public async Task RecordConversionEvent_And_GetFunnelPerformance_AggregatesMetrics()
    {
        // Arrange
        using var db = CreateDbContext();
        var service = new SalesFunnelService(db);
        var tenantId = Guid.NewGuid();

        var funnel = await service.CreateFunnelAsync(tenantId, "Sneaker Funnel", "sneakers-funnel");
        var step1 = await service.AddFunnelStepAsync(tenantId, funnel.Id, "Landing", FunnelStepType.LandingPage, 1);
        var step2 = await service.AddFunnelStepAsync(tenantId, funnel.Id, "Checkout", FunnelStepType.Checkout, 2);

        // Act: 100 PageViews, 10 Checkouts, 8 Purchases, 3 Order Bumps (+25 TND), 2 Upsells (+40 TND)
        for (int i = 0; i < 100; i++)
        {
            await service.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.PageView, 0, step1.Id);
        }

        for (int i = 0; i < 10; i++)
        {
            await service.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.CheckoutInitiated, 0, step2.Id);
        }

        for (int i = 0; i < 8; i++)
        {
            await service.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.FunnelOrderCompleted, 180.0m, step2.Id);
        }

        await service.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.OrderBumpAccepted, 25.0m, step2.Id);
        await service.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.OrderBumpAccepted, 25.0m, step2.Id);
        await service.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.OrderBumpAccepted, 25.0m, step2.Id);

        await service.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.UpsellAccepted, 40.0m);
        await service.RecordConversionEventAsync(tenantId, funnel.Id, FunnelEventType.UpsellAccepted, 40.0m);

        var perf = await service.GetFunnelPerformanceAsync(tenantId, funnel.Id);

        // Assert
        Assert.NotNull(perf);
        Assert.Equal(100, perf.TotalVisitors);
        Assert.Equal(8, perf.TotalOrders);
        Assert.Equal(8.0, perf.ConversionRatePercentage);
        // Total revenue = 8 * 180 (1440) + 3 * 25 (75) + 2 * 40 (80) = 1595
        Assert.Equal(1595.0m, perf.TotalRevenue);
        Assert.Equal(3, perf.BumpOrdersCount);
        Assert.Equal(75.0m, perf.BumpRevenue);
        Assert.Equal(2, perf.UpsellOrdersCount);
        Assert.Equal(80.0m, perf.UpsellRevenue);
    }
}
