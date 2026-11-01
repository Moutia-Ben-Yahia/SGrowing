using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.CRM.RetentionConcierge;
using Xunit;

namespace SGrowing.Tests.CRM;

public sealed class CustomerRetentionConciergeTests
{
    private readonly PredictiveChurnSurvivalEngine _survivalEngine = new();
    private readonly RetentionOfferBanditService _banditService = new();
    private readonly ProactiveDelightConciergeService _concierge;

    public CustomerRetentionConciergeTests()
    {
        _concierge = new ProactiveDelightConciergeService(_survivalEngine, _banditService);
    }

    #region Survival Analysis & Churn Prediction Tests

    [Fact]
    public void PredictChurn_HighlyEngagedCustomer_ReturnsLowHazardAndLongSurvival()
    {
        var happyCustomer = new CustomerEngagementProfile(
            CustomerId: "CUST-HAPPY-001",
            TenantId: "TEN-01",
            Tier: CustomerTier.Gold,
            DaysSinceLastOrder: 4,
            OrderFrequencyPerMonth: 3.5,
            AverageOrderValueDzd: 18000m,
            SupportTicketSentimentScore: 0.90, // Delighted
            ReturnRateRatio: 0.02,
            AppSessionsLast30Days: 25,
            UnopenedMarketingEmailsCount: 0,
            LifetimeValueDzd: 450000m
        );

        var prediction = _survivalEngine.PredictChurn(happyCustomer);

        prediction.HazardScore.Should().BeLessThan(0.35);
        prediction.RiskLevel.Should().BeOneOf(ChurnRiskLevel.Minimal, ChurnRiskLevel.Moderate);
        prediction.SurvivalProbabilityAt30Days.Should().BeGreaterThan(0.65);
        prediction.EstimatedDaysUntilChurn.Should().BeGreaterThan(60);
    }

    [Fact]
    public void PredictChurn_LapsedDissatisfiedCustomer_ReturnsHighHazardAndShortSurvival()
    {
        var atRiskCustomer = new CustomerEngagementProfile(
            CustomerId: "CUST-AT-RISK-99",
            TenantId: "TEN-01",
            Tier: CustomerTier.Silver,
            DaysSinceLastOrder: 75, // 2.5 months absent
            OrderFrequencyPerMonth: 0.2,
            AverageOrderValueDzd: 12000m,
            SupportTicketSentimentScore: -0.85, // Highly frustrated
            ReturnRateRatio: 0.45,             // 45% returned items
            AppSessionsLast30Days: 1,
            UnopenedMarketingEmailsCount: 8,
            LifetimeValueDzd: 120000m
        );

        var prediction = _survivalEngine.PredictChurn(atRiskCustomer);

        prediction.HazardScore.Should().BeGreaterThanOrEqualTo(0.50);
        prediction.RiskLevel.Should().BeOneOf(ChurnRiskLevel.High, ChurnRiskLevel.CriticalImminent);
        prediction.SurvivalProbabilityAt90Days.Should().BeLessThan(0.40);
        prediction.EstimatedDaysUntilChurn.Should().BeLessThan(50);
        prediction.ContributingRiskFactors.Should().Contain(f => f.Contains("Order Recency"));
        prediction.ContributingRiskFactors.Should().Contain(f => f.Contains("Frustration"));
    }

    [Fact]
    public void PredictChurn_CalculatesSurvivalProbabilitiesBetweenZeroAndOne()
    {
        var profile = new CustomerEngagementProfile(
            CustomerId: "CUST-TEST-01",
            TenantId: "TEN-01",
            Tier: CustomerTier.Standard,
            DaysSinceLastOrder: 30,
            OrderFrequencyPerMonth: 1.0,
            AverageOrderValueDzd: 5000m,
            SupportTicketSentimentScore: 0.0,
            ReturnRateRatio: 0.10,
            AppSessionsLast30Days: 5,
            UnopenedMarketingEmailsCount: 2,
            LifetimeValueDzd: 30000m
        );

        var pred = _survivalEngine.PredictChurn(profile);

        pred.SurvivalProbabilityAt30Days.Should().BeInRange(0.0, 1.0);
        pred.SurvivalProbabilityAt90Days.Should().BeInRange(0.0, 1.0);
        pred.SurvivalProbabilityAt90Days.Should().BeLessThanOrEqualTo(pred.SurvivalProbabilityAt30Days);
    }

    [Fact]
    public void PredictChurn_NominalEngagement_HasNominalFactor()
    {
        var profile = new CustomerEngagementProfile(
            CustomerId: "CUST-NOMINAL",
            TenantId: "TEN-01",
            Tier: CustomerTier.Gold,
            DaysSinceLastOrder: 10,
            OrderFrequencyPerMonth: 2.0,
            AverageOrderValueDzd: 15000m,
            SupportTicketSentimentScore: 0.50,
            ReturnRateRatio: 0.05,
            AppSessionsLast30Days: 12,
            UnopenedMarketingEmailsCount: 1,
            LifetimeValueDzd: 180000m
        );

        var pred = _survivalEngine.PredictChurn(profile);
        pred.ContributingRiskFactors.Should().Contain("Nominal Engagement Profile");
    }

    #endregion

    #region Multi-Armed Bandit Tests

    [Fact]
    public void RetentionOfferBandit_PlatinumVIP_SelectsEligibleVipOffer()
    {
        var incentive = _banditService.SelectBestIncentive(CustomerTier.PlatinumVIP);

        incentive.Should().BeOneOf(
            DelightIncentiveType.ExecutiveAccountManagerCall,
            DelightIncentiveType.ComplimentaryGiftBasket,
            DelightIncentiveType.ExclusivePrivateSaleAccess,
            DelightIncentiveType.PersonalizedDiscountCode
        );
    }

    [Fact]
    public void RetentionOfferBandit_StandardCustomer_SelectsStandardIncentive()
    {
        var incentive = _banditService.SelectBestIncentive(CustomerTier.Standard);

        incentive.Should().BeOneOf(
            DelightIncentiveType.ExpeditedShippingWaiver,
            DelightIncentiveType.BonusLoyaltyCredits
        );
    }

    [Fact]
    public void RetentionOfferBandit_RecordOutcome_UpdatesAlphaAndBetaPriors()
    {
        var initialStats = _banditService.GetArmStatistics()
            .First(a => a.IncentiveType == DelightIncentiveType.ExpeditedShippingWaiver);

        // Record a conversion
        _banditService.RecordOutcome(DelightIncentiveType.ExpeditedShippingWaiver, converted: true, retainedLtvDzd: 50000m);

        var updatedStats = _banditService.GetArmStatistics()
            .First(a => a.IncentiveType == DelightIncentiveType.ExpeditedShippingWaiver);

        updatedStats.AlphaSuccesses.Should().Be(initialStats.AlphaSuccesses + 1.0);
        updatedStats.TotalTrials.Should().Be(initialStats.TotalTrials + 1);
        updatedStats.TotalLtvRetainedDzd.Should().Be(initialStats.TotalLtvRetainedDzd + 50000m);
    }

    #endregion

    #region Proactive Delight Concierge Tests

    [Fact]
    public void ProactiveDelightConcierge_LowHazardCustomer_DoesNotDispatchDelight()
    {
        var happyCustomer = new CustomerEngagementProfile(
            CustomerId: "CUST-SAFE",
            TenantId: "TEN-01",
            Tier: CustomerTier.Gold,
            DaysSinceLastOrder: 5,
            OrderFrequencyPerMonth: 4.0,
            AverageOrderValueDzd: 25000m,
            SupportTicketSentimentScore: 0.95,
            ReturnRateRatio: 0.01,
            AppSessionsLast30Days: 20,
            UnopenedMarketingEmailsCount: 0,
            LifetimeValueDzd: 500000m
        );

        var action = _concierge.EvaluateAndDispatchDelight(new DispatchDelightRequest(happyCustomer));
        action.Should().BeNull();
    }

    [Fact]
    public void ProactiveDelightConcierge_LowHazardCustomer_ForceDispatch_DispatchesDelight()
    {
        var happyCustomer = new CustomerEngagementProfile(
            CustomerId: "CUST-SAFE-FORCE",
            TenantId: "TEN-01",
            Tier: CustomerTier.Gold,
            DaysSinceLastOrder: 5,
            OrderFrequencyPerMonth: 4.0,
            AverageOrderValueDzd: 25000m,
            SupportTicketSentimentScore: 0.95,
            ReturnRateRatio: 0.01,
            AppSessionsLast30Days: 20,
            UnopenedMarketingEmailsCount: 0,
            LifetimeValueDzd: 500000m
        );

        var action = _concierge.EvaluateAndDispatchDelight(new DispatchDelightRequest(happyCustomer, ForceDispatch: true));

        action.Should().NotBeNull();
        action!.Status.Should().Be(DelightActionStatus.Dispatched);
        action.CouponCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ProactiveDelightConcierge_HighHazardCustomer_AutonomouslyDispatchesDelight()
    {
        var atRiskCustomer = new CustomerEngagementProfile(
            CustomerId: "CUST-RISK-01",
            TenantId: "TEN-01",
            Tier: CustomerTier.PlatinumVIP,
            DaysSinceLastOrder: 80,
            OrderFrequencyPerMonth: 0.1,
            AverageOrderValueDzd: 85000m,
            SupportTicketSentimentScore: -0.90,
            ReturnRateRatio: 0.40,
            AppSessionsLast30Days: 0,
            UnopenedMarketingEmailsCount: 9,
            LifetimeValueDzd: 1500000m
        );

        var action = _concierge.EvaluateAndDispatchDelight(new DispatchDelightRequest(atRiskCustomer));

        action.Should().NotBeNull();
        action!.CustomerId.Should().Be("CUST-RISK-01");
        action.Channel.Should().Be(DelightOutreachChannel.WhatsApp); // VIP receives WhatsApp outreach
        action.EstimatedCostDzd.Should().BeGreaterThan(0m);
        action.ExpectedLtvLiftDzd.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void ProactiveDelightConcierge_RedeemAction_MarksRedeemedAndFeedsBandit()
    {
        var customer = new CustomerEngagementProfile(
            CustomerId: "CUST-REDEEM",
            TenantId: "TEN-01",
            Tier: CustomerTier.Silver,
            DaysSinceLastOrder: 90,
            OrderFrequencyPerMonth: 0.1,
            AverageOrderValueDzd: 15000m,
            SupportTicketSentimentScore: -0.80,
            ReturnRateRatio: 0.30,
            AppSessionsLast30Days: 1,
            UnopenedMarketingEmailsCount: 7,
            LifetimeValueDzd: 150000m
        );

        var action = _concierge.EvaluateAndDispatchDelight(new DispatchDelightRequest(customer, ForceDispatch: true));
        action.Should().NotBeNull();

        var redeemed = _concierge.RedeemAction(action!.ActionId, retainedLtvDzd: 85000m);
        redeemed.Should().BeTrue();

        var updated = _concierge.GetAction(action.ActionId);
        updated!.Status.Should().Be(DelightActionStatus.Redeemed);
        updated.RedeemedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void ProactiveDelightConcierge_ExpireAction_MarksExpired()
    {
        var customer = new CustomerEngagementProfile(
            CustomerId: "CUST-EXPIRE",
            TenantId: "TEN-01",
            Tier: CustomerTier.Standard,
            DaysSinceLastOrder: 95,
            OrderFrequencyPerMonth: 0.1,
            AverageOrderValueDzd: 8000m,
            SupportTicketSentimentScore: -0.80,
            ReturnRateRatio: 0.30,
            AppSessionsLast30Days: 1,
            UnopenedMarketingEmailsCount: 7,
            LifetimeValueDzd: 80000m
        );

        var action = _concierge.EvaluateAndDispatchDelight(new DispatchDelightRequest(customer, ForceDispatch: true));
        action.Should().NotBeNull();

        var expired = _concierge.ExpireAction(action!.ActionId);
        expired.Should().BeTrue();

        var updated = _concierge.GetAction(action.ActionId);
        updated!.Status.Should().Be(DelightActionStatus.Expired);
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void ServiceCollectionExtensions_AddRetentionConciergeServices_RegistersAllDependencies()
    {
        var services = new ServiceCollection();
        services.AddRetentionConciergeServices();

        services.Should().Contain(d => d.ServiceType == typeof(PredictiveChurnSurvivalEngine) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(RetentionOfferBanditService) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().Contain(d => d.ServiceType == typeof(ProactiveDelightConciergeService) && d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
