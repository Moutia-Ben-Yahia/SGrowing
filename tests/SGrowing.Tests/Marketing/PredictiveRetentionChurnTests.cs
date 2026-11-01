using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Marketing.Retention;
using Xunit;

namespace SGrowing.Tests.Marketing;

/// <summary>
/// Comprehensive test suite for Phase 30: Autonomous Omnichannel Customer Retention,
/// Predictive Churn Radar &amp; Win-Back Bandits.
/// </summary>
public class PredictiveRetentionChurnTests
{
    private readonly PredictiveChurnRadarService _radarService = new();
    private readonly RetentionPolicyBanditService _banditService = new();
    private readonly OmnichannelRetentionOrchestrator _orchestrator;

    public PredictiveRetentionChurnTests()
    {
        _orchestrator = new OmnichannelRetentionOrchestrator(_radarService, _banditService);
    }

    #region 1. Cox Proportional Hazards Survival Analysis & Hazard Rate Tests

    [Fact]
    public void CalculateHazardRate_WithBaselineZeroDeficits_MatchesTheoreticalBaseline()
    {
        // h0 = 0.05, beta1 = 1.8, beta2 = 1.4, beta3 = 1.2
        // cadenceDecay = 1.0, sentiment = 1.0 (deficit 0), openRate = 1.0 (deficit 0)
        // exponent = 1.8 * 1.0 + 1.4 * 0.0 + 1.2 * 0.0 = 1.8
        // expected hazard = 0.05 * exp(1.8) ~= 0.30248
        double hazard = _radarService.CalculateHazardRate(cadenceDecay: 1.0, sentimentScore: 1.0, openRate: 1.0);

        double theoretical = 0.05 * Math.Exp(1.8);
        hazard.Should().BeApproximately(theoretical, 0.0001);
    }

    [Fact]
    public void CalculateHazardRate_WithSentimentDeficit_IncreasesHazardExponentially()
    {
        // Baseline positive sentiment vs negative sentiment
        double positiveHazard = _radarService.CalculateHazardRate(cadenceDecay: 1.0, sentimentScore: 1.0, openRate: 0.8);
        double negativeHazard = _radarService.CalculateHazardRate(cadenceDecay: 1.0, sentimentScore: -1.0, openRate: 0.8);

        // Negative sentiment creates deficit of 2.0 (1 - (-1)), adding beta2 * 2.0 = 2.8 to exponent
        // hazard ratio should be exp(2.8) ~= 16.44
        double hazardRatio = negativeHazard / positiveHazard;
        hazardRatio.Should().BeApproximately(Math.Exp(2.8), 0.01);
    }

    [Fact]
    public void CalculateHazardRate_WithOpenRateDeficit_AppliesBeta3Weight()
    {
        // 100% open rate (deficit 0) vs 0% open rate (deficit 1)
        double fullOpenHazard = _radarService.CalculateHazardRate(cadenceDecay: 1.0, sentimentScore: 0.5, openRate: 1.0);
        double zeroOpenHazard = _radarService.CalculateHazardRate(cadenceDecay: 1.0, sentimentScore: 0.5, openRate: 0.0);

        // Deficit adds beta3 * 1.0 = 1.2 to exponent; hazard ratio should be exp(1.2) ~= 3.32
        double hazardRatio = zeroOpenHazard / fullOpenHazard;
        hazardRatio.Should().BeApproximately(Math.Exp(1.2), 0.01);
    }

    [Fact]
    public void CalculateHazardRate_WithHighCadenceDecay_SignificantlyAcceleratesHazard()
    {
        double normalHazard = _radarService.CalculateHazardRate(cadenceDecay: 1.0, sentimentScore: 0.5, openRate: 0.5);
        double delayedHazard = _radarService.CalculateHazardRate(cadenceDecay: 2.0, sentimentScore: 0.5, openRate: 0.5);

        // Decay +1.0 adds beta1 * 1.0 = 1.8 to exponent; hazard ratio should be exp(1.8) ~= 6.05
        double hazardRatio = delayedHazard / normalHazard;
        hazardRatio.Should().BeApproximately(Math.Exp(1.8), 0.01);
    }

    [Fact]
    public void CalculateHazardRate_ClampsExtremeInputsSafely()
    {
        // Tests out-of-range inputs to verify no NaN or double overflow occurs
        double hazard = _radarService.CalculateHazardRate(cadenceDecay: 99.0, sentimentScore: -5.0, openRate: -1.0);

        hazard.Should().BeGreaterThan(0.0);
        double.IsNaN(hazard).Should().BeFalse();
        double.IsInfinity(hazard).Should().BeFalse();
    }

    #endregion

    #region 2. Risk Score Scaling & Monotonicity Tests

    [Fact]
    public void CalculateRiskScore_WithZeroHazard_ReturnsZero()
    {
        double score = _radarService.CalculateRiskScore(0.0);
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateRiskScore_MonotonicallyIncreasesWithHazardRate()
    {
        double score1 = _radarService.CalculateRiskScore(0.1);
        double score2 = _radarService.CalculateRiskScore(0.5);
        double score3 = _radarService.CalculateRiskScore(1.2);
        double score4 = _radarService.CalculateRiskScore(3.0);

        score1.Should().BeLessThan(score2);
        score2.Should().BeLessThan(score3);
        score3.Should().BeLessThan(score4);
    }

    [Fact]
    public void CalculateRiskScore_MapsAsymptoticallyTowards100()
    {
        double scoreExtreme = _radarService.CalculateRiskScore(50.0);

        scoreExtreme.Should().Be(100.0);
        scoreExtreme.Should().BeLessThanOrEqualTo(100.0);
    }

    #endregion

    #region 3. Risk Tier Categorization Tests

    [Theory]
    [InlineData(0.0, ChurnRiskTier.Low)]
    [InlineData(12.5, ChurnRiskTier.Low)]
    [InlineData(29.99, ChurnRiskTier.Low)]
    public void CategorizeRiskTier_ScoresBelow30_ClassifiesAsLow(double score, ChurnRiskTier expected)
    {
        _radarService.CategorizeRiskTier(score).Should().Be(expected);
    }

    [Theory]
    [InlineData(30.0, ChurnRiskTier.Moderate)]
    [InlineData(45.5, ChurnRiskTier.Moderate)]
    [InlineData(59.99, ChurnRiskTier.Moderate)]
    public void CategorizeRiskTier_ScoresBetween30And60_ClassifiesAsModerate(double score, ChurnRiskTier expected)
    {
        _radarService.CategorizeRiskTier(score).Should().Be(expected);
    }

    [Theory]
    [InlineData(60.0, ChurnRiskTier.High)]
    [InlineData(72.5, ChurnRiskTier.High)]
    [InlineData(84.99, ChurnRiskTier.High)]
    public void CategorizeRiskTier_ScoresBetween60And85_ClassifiesAsHigh(double score, ChurnRiskTier expected)
    {
        _radarService.CategorizeRiskTier(score).Should().Be(expected);
    }

    [Theory]
    [InlineData(85.0, ChurnRiskTier.Critical)]
    [InlineData(94.2, ChurnRiskTier.Critical)]
    [InlineData(100.0, ChurnRiskTier.Critical)]
    public void CategorizeRiskTier_ScoresAtOrAbove85_ClassifiesAsCritical(double score, ChurnRiskTier expected)
    {
        _radarService.CategorizeRiskTier(score).Should().Be(expected);
    }

    #endregion

    #region 4. Expected Days to Churn Calculation Tests

    [Fact]
    public void CalculateExpectedDaysToChurn_WithHealthyCustomer_ReturnsLongHorizon()
    {
        // 5 days since order, 30 days cadence, low hazard (0.1)
        int days = _radarService.CalculateExpectedDaysToChurn(daysSinceLastOrder: 5, historicalCadenceDays: 30, hazardRate: 0.1);

        days.Should().BeGreaterThan(50);
    }

    [Fact]
    public void CalculateExpectedDaysToChurn_WithOverdueCustomer_ReducesHorizon()
    {
        int healthyDays = _radarService.CalculateExpectedDaysToChurn(daysSinceLastOrder: 5, historicalCadenceDays: 30, hazardRate: 0.1);
        int overdueDays = _radarService.CalculateExpectedDaysToChurn(daysSinceLastOrder: 50, historicalCadenceDays: 30, hazardRate: 1.5);

        overdueDays.Should().BeLessThan(healthyDays);
        overdueDays.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateExpectedDaysToChurn_WithSeverelyLapsedCustomer_ClampsToMinimum1Day()
    {
        // 120 days since last order (far beyond 2.5 * 30 = 75 day churn window)
        int days = _radarService.CalculateExpectedDaysToChurn(daysSinceLastOrder: 120, historicalCadenceDays: 30, hazardRate: 10.0);

        days.Should().Be(1);
    }

    [Fact]
    public void CalculateExpectedDaysToChurn_WithZeroCadence_UsesSensibleDefault()
    {
        // Should not throw divide-by-zero or return negative
        int days = _radarService.CalculateExpectedDaysToChurn(daysSinceLastOrder: 10, historicalCadenceDays: 0, hazardRate: 0.5);

        days.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateExpectedDaysToChurn_HighHazard_AcceleratesTimeDecay()
    {
        int lowHazardDays = _radarService.CalculateExpectedDaysToChurn(daysSinceLastOrder: 30, historicalCadenceDays: 30, hazardRate: 0.2);
        int highHazardDays = _radarService.CalculateExpectedDaysToChurn(daysSinceLastOrder: 30, historicalCadenceDays: 30, hazardRate: 4.0);

        highHazardDays.Should().BeLessThan(lowHazardDays);
    }

    #endregion

    #region 5. Primary Risk Factors Identification Tests

    [Fact]
    public void IdentifyPrimaryRiskFactors_WithHealthyCustomer_ReturnsNormalEngagement()
    {
        var signals = new CustomerBehaviorSignals(
            CustomerId: "CUST-HEALTHY",
            DaysSinceLastOrder: 10,
            HistoricalOrderCadenceDays: 30,
            CadenceDecayRatio: 0.33,
            AvgOrderValue: 7500m,
            SupportTicketSentimentScore: 0.9,
            UnresolvedTicketCount: 0,
            WhatsAppMessageOpenRate: 0.95,
            EmailClickRate: 0.40,
            BrowsingDropOffRatio: 0.20);

        var factors = _radarService.IdentifyPrimaryRiskFactors(signals, hazardRate: 0.15);

        factors.Should().ContainSingle();
        factors[0].Should().Contain("Normal customer engagement profile");
    }

    [Fact]
    public void IdentifyPrimaryRiskFactors_WithOverdueCadence_FlagsCadenceDelay()
    {
        var signals = new CustomerBehaviorSignals(
            CustomerId: "CUST-OVERDUE",
            DaysSinceLastOrder: 60,
            HistoricalOrderCadenceDays: 30,
            CadenceDecayRatio: 2.0,
            AvgOrderValue: 4000m,
            SupportTicketSentimentScore: 0.5,
            UnresolvedTicketCount: 0,
            WhatsAppMessageOpenRate: 0.80,
            EmailClickRate: 0.30,
            BrowsingDropOffRatio: 0.25);

        var factors = _radarService.IdentifyPrimaryRiskFactors(signals, hazardRate: 0.80);

        factors.Should().Contain(f => f.Contains("cadence delayed"));
    }

    [Fact]
    public void IdentifyPrimaryRiskFactors_WithNegativeSentimentAndUnresolvedTickets_FlagsSupportIssues()
    {
        var signals = new CustomerBehaviorSignals(
            CustomerId: "CUST-UPSET",
            DaysSinceLastOrder: 15,
            HistoricalOrderCadenceDays: 30,
            CadenceDecayRatio: 0.5,
            AvgOrderValue: 5000m,
            SupportTicketSentimentScore: -0.8,
            UnresolvedTicketCount: 3,
            WhatsAppMessageOpenRate: 0.70,
            EmailClickRate: 0.20,
            BrowsingDropOffRatio: 0.30);

        var factors = _radarService.IdentifyPrimaryRiskFactors(signals, hazardRate: 1.5);

        factors.Should().Contain(f => f.Contains("Negative support ticket sentiment"));
        factors.Should().Contain(f => f.Contains("Unresolved customer support tickets"));
    }

    [Fact]
    public void IdentifyPrimaryRiskFactors_WithLowWhatsAppAndHighBrowseDropOff_FlagsChannelAndFunnelFriction()
    {
        var signals = new CustomerBehaviorSignals(
            CustomerId: "CUST-FRICTION",
            DaysSinceLastOrder: 25,
            HistoricalOrderCadenceDays: 30,
            CadenceDecayRatio: 0.83,
            AvgOrderValue: 3000m,
            SupportTicketSentimentScore: 0.2,
            UnresolvedTicketCount: 0,
            WhatsAppMessageOpenRate: 0.10,
            EmailClickRate: 0.05,
            BrowsingDropOffRatio: 0.85);

        var factors = _radarService.IdentifyPrimaryRiskFactors(signals, hazardRate: 1.2);

        factors.Should().Contain(f => f.Contains("WhatsApp channel disengagement"));
        factors.Should().Contain(f => f.Contains("drop-off without purchase"));
    }

    #endregion

    #region 6. AssessCustomerRisk End-to-End Tests

    [Fact]
    public void AssessCustomerRisk_HealthyCustomer_ProducesLowRiskAssessment()
    {
        var signals = new CustomerBehaviorSignals(
            CustomerId: "CUST-001",
            DaysSinceLastOrder: 12,
            HistoricalOrderCadenceDays: 30,
            CadenceDecayRatio: 0.4,
            AvgOrderValue: 9000m,
            SupportTicketSentimentScore: 0.9,
            UnresolvedTicketCount: 0,
            WhatsAppMessageOpenRate: 0.90,
            EmailClickRate: 0.35,
            BrowsingDropOffRatio: 0.15);

        var assessment = _radarService.AssessCustomerRisk(signals);

        assessment.CustomerId.Should().Be("CUST-001");
        assessment.RiskTier.Should().Be(ChurnRiskTier.Low);
        assessment.ChurnRiskScore.Should().BeLessThan(30.0);
        assessment.ExpectedDaysToChurn.Should().BeGreaterThan(40);
        assessment.AssessedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AssessCustomerRisk_CriticalCustomer_ProducesCriticalRiskAssessment()
    {
        var signals = new CustomerBehaviorSignals(
            CustomerId: "CUST-CRITICAL",
            DaysSinceLastOrder: 80,
            HistoricalOrderCadenceDays: 30,
            CadenceDecayRatio: 2.67,
            AvgOrderValue: 3500m,
            SupportTicketSentimentScore: -0.9,
            UnresolvedTicketCount: 4,
            WhatsAppMessageOpenRate: 0.05,
            EmailClickRate: 0.0,
            BrowsingDropOffRatio: 0.90);

        var assessment = _radarService.AssessCustomerRisk(signals);

        assessment.CustomerId.Should().Be("CUST-CRITICAL");
        assessment.RiskTier.Should().Be(ChurnRiskTier.Critical);
        assessment.ChurnRiskScore.Should().BeGreaterThanOrEqualTo(85.0);
        assessment.ExpectedDaysToChurn.Should().BeLessThanOrEqualTo(5);
        assessment.PrimaryRiskFactors.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void AssessCustomerRisk_DerivesCadenceDecayWhenRatioIsZero()
    {
        var signals = new CustomerBehaviorSignals(
            CustomerId: "CUST-DERIVED",
            DaysSinceLastOrder: 45,
            HistoricalOrderCadenceDays: 30,
            CadenceDecayRatio: 0.0, // Unset, will derive 45 / 30 = 1.5
            AvgOrderValue: 4500m,
            SupportTicketSentimentScore: 0.2,
            UnresolvedTicketCount: 0,
            WhatsAppMessageOpenRate: 0.60,
            EmailClickRate: 0.15,
            BrowsingDropOffRatio: 0.40);

        var assessment = _radarService.AssessCustomerRisk(signals);

        assessment.HazardRate.Should().BeGreaterThan(0.5);
        assessment.PrimaryRiskFactors.Should().Contain(f => f.Contains("cadence delayed"));
    }

    [Fact]
    public void AssessCustomerRisk_ThrowsArgumentNullException_OnNullSignals()
    {
        var act = () => _radarService.AssessCustomerRisk(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region 7. Batch Radar Scan Tests

    [Fact]
    public async Task ScanBatchAsync_WithEmptyCohort_ReturnsZeroCount()
    {
        var request = new BatchRadarScanRequest(Customers: []);

        var response = await _radarService.ScanBatchAsync(request);

        response.Success.Should().BeTrue();
        response.TotalScanned.Should().Be(0);
        response.AtRiskCount.Should().Be(0);
        response.AtRiskCustomers.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanBatchAsync_FiltersCustomersBelowThreshold()
    {
        var healthy = new CustomerBehaviorSignals("CUST-H", 10, 30, 0.33, 8000m, 0.9, 0, 0.9, 0.3, 0.1);
        var critical = new CustomerBehaviorSignals("CUST-C", 75, 30, 2.5, 3000m, -0.8, 2, 0.1, 0.0, 0.8);

        var request = new BatchRadarScanRequest(
            Customers: [healthy, critical],
            RiskThreshold: 60.0); // Only High & Critical

        var response = await _radarService.ScanBatchAsync(request);

        response.Success.Should().BeTrue();
        response.TotalScanned.Should().Be(2);
        response.AtRiskCount.Should().Be(1);
        response.AtRiskCustomers.Should().ContainSingle(c => c.CustomerId == "CUST-C");
    }

    [Fact]
    public async Task ScanBatchAsync_RanksCohortDescendingByRiskScore()
    {
        var low = new CustomerBehaviorSignals("CUST-LOW", 10, 30, 0.33, 8000m, 0.8, 0, 0.9, 0.3, 0.1);
        var moderate = new CustomerBehaviorSignals("CUST-MOD", 35, 30, 1.16, 5000m, 0.3, 0, 0.6, 0.2, 0.3);
        var critical = new CustomerBehaviorSignals("CUST-CRIT", 85, 30, 2.8, 3000m, -0.9, 3, 0.1, 0.0, 0.9);

        var request = new BatchRadarScanRequest(
            Customers: [low, moderate, critical],
            RiskThreshold: 0.0); // Return all ranked

        var response = await _radarService.ScanBatchAsync(request);

        response.AtRiskCustomers.Should().HaveCount(3);
        response.AtRiskCustomers[0].CustomerId.Should().Be("CUST-CRIT");
        response.AtRiskCustomers[1].CustomerId.Should().Be("CUST-MOD");
        response.AtRiskCustomers[2].CustomerId.Should().Be("CUST-LOW");
        response.AtRiskCustomers.Should().BeInDescendingOrder(c => c.ChurnRiskScore);
    }

    [Fact]
    public async Task ScanBatchAsync_RespectsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new BatchRadarScanRequest(Customers: [
            new CustomerBehaviorSignals("CUST-1", 30, 30, 1.0, 5000m, 0.5, 0, 0.5, 0.2, 0.3)
        ]);

        var act = async () => await _radarService.ScanBatchAsync(request, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region 8. Multi-Armed Bandit Incentive Catalog & Sampling Tests

    [Fact]
    public void GetIncentiveDefinition_ContainsExpectedFourAlgerianIncentives()
    {
        var catalog = _banditService.GetAllIncentives();

        catalog.Should().HaveCount(4);

        var shipping = _banditService.GetIncentiveDefinition(IncentiveType.ExpressShippingVoucher);
        shipping.CostDzd.Should().Be(600m);
        shipping.ExpiryDays.Should().Be(5);

        var bundle = _banditService.GetIncentiveDefinition(IncentiveType.BundleDiscount15);
        bundle.DiscountPercentage.Should().Be(15m);
        bundle.CostDzd.Should().Be(1200m);

        var vip = _banditService.GetIncentiveDefinition(IncentiveType.VipConciergeCall);
        vip.CostDzd.Should().Be(800m);

        var gift = _banditService.GetIncentiveDefinition(IncentiveType.FreeGiftWithPurchase);
        gift.CostDzd.Should().Be(1500m);
    }

    [Fact]
    public void SampleBeta_GeneratesValuesWithinUnitInterval()
    {
        var deterministicRng = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            double sample = _banditService.SampleBeta(alpha: 2.0, beta: 5.0, deterministicRng);
            sample.Should().BeGreaterThan(0.0).And.BeLessThan(1.0);
        }
    }

    [Fact]
    public void SampleBeta_WithHighAlpha_ProducesHigherMean()
    {
        var rng = new Random(12345);
        List<double> highAlphaSamples = [];
        List<double> highBetaSamples = [];

        for (int i = 0; i < 200; i++)
        {
            highAlphaSamples.Add(_banditService.SampleBeta(alpha: 20.0, beta: 2.0, rng));
            highBetaSamples.Add(_banditService.SampleBeta(alpha: 2.0, beta: 20.0, rng));
        }

        double meanHighAlpha = highAlphaSamples.Average();
        double meanHighBeta = highBetaSamples.Average();

        meanHighAlpha.Should().BeGreaterThan(0.75); // Theoretical: 20 / 22 ~= 0.909
        meanHighBeta.Should().BeLessThan(0.25);   // Theoretical: 2 / 22 ~= 0.091
    }

    [Fact]
    public void ResetPriors_RestoresAllArmsToUniformDistribution()
    {
        _banditService.RecordFeedback(IncentiveType.ExpressShippingVoucher, converted: true, 5000m);
        _banditService.ResetPriors(1.0, 1.0);

        var stats = _banditService.GetAllArmStats();
        stats.Should().AllSatisfy(s =>
        {
            s.TotalPulls.Should().Be(0);
            s.TotalConversions.Should().Be(0);
            s.ConversionRate.Should().Be(0.0);
            s.CumulativeRevenueLift.Should().Be(0m);
        });
    }

    #endregion

    #region 9. Contextual Bandit Recommendation Tests

    [Fact]
    public void RecommendIncentive_VipCustomerWithUnresolvedTickets_BiasesTowardsVipConcierge()
    {
        // High AOV (15,000 DZD) + negative sentiment + open tickets -> Context multiplier 1.25 for VIP call
        var signals = new CustomerBehaviorSignals(
            CustomerId: "VIP-UPSET",
            DaysSinceLastOrder: 40,
            HistoricalOrderCadenceDays: 30,
            CadenceDecayRatio: 1.33,
            AvgOrderValue: 15000m,
            SupportTicketSentimentScore: -0.7,
            UnresolvedTicketCount: 2,
            WhatsAppMessageOpenRate: 0.50,
            EmailClickRate: 0.20,
            BrowsingDropOffRatio: 0.30);

        var deterministicRng = new Random(777);
        var recommendation = _banditService.RecommendIncentive(signals, random: deterministicRng);

        recommendation.CustomerId.Should().Be("VIP-UPSET");
        recommendation.SelectedIncentive.Should().NotBeNull();
        recommendation.RecommendedChannel.Should().BeOneOf(RetentionChannel.AutomatedVoiceConcierge, RetentionChannel.WhatsAppInteractive);
    }

    [Fact]
    public void RecommendIncentive_PriceHesitantCustomer_BiasesTowardsBundleDiscount()
    {
        var signals = new CustomerBehaviorSignals(
            CustomerId: "CART-ABANDON",
            DaysSinceLastOrder: 45,
            HistoricalOrderCadenceDays: 30,
            CadenceDecayRatio: 1.5,
            AvgOrderValue: 4000m,
            SupportTicketSentimentScore: 0.4,
            UnresolvedTicketCount: 0,
            WhatsAppMessageOpenRate: 0.85,
            EmailClickRate: 0.30,
            BrowsingDropOffRatio: 0.80);

        var recommendation = _banditService.RecommendIncentive(signals);

        recommendation.CustomerId.Should().Be("CART-ABANDON");
        recommendation.SelectedIncentive.Should().NotBeNull();
    }

    [Fact]
    public void RecommendIncentive_ComputesExplorationProbabilityWithinValidBounds()
    {
        var signals = new CustomerBehaviorSignals("CUST-PROB", 20, 30, 0.66, 6000m, 0.5, 0, 0.7, 0.2, 0.3);

        var recommendation = _banditService.RecommendIncentive(signals);

        recommendation.ExplorationProbability.Should().BeInRange(0.01, 1.0);
    }

    [Fact]
    public void RecommendIncentive_ComputesEstimatedConversionLiftAboveZero()
    {
        var signals = new CustomerBehaviorSignals("CUST-LIFT", 20, 30, 0.66, 6000m, 0.5, 0, 0.7, 0.2, 0.3);

        var recommendation = _banditService.RecommendIncentive(signals);

        recommendation.EstimatedConversionLift.Should().BeGreaterThanOrEqualTo(0.0);
    }

    #endregion

    #region 10. Multi-Armed Bandit Feedback & Learning Convergence Tests

    [Fact]
    public void RecordFeedback_OnConversion_IncrementsAlphaAndConversions()
    {
        _banditService.ResetPriors();

        var stats = _banditService.RecordFeedback(
            IncentiveType.ExpressShippingVoucher,
            converted: true,
            revenueSavedDzd: 4500m);

        stats.TotalPulls.Should().Be(1);
        stats.TotalConversions.Should().Be(1);
        stats.ConversionRate.Should().Be(1.0);
        stats.CumulativeRevenueLift.Should().Be(4500m);
    }

    [Fact]
    public void RecordFeedback_OnDefection_IncrementsBetaOnly()
    {
        _banditService.ResetPriors();

        var stats = _banditService.RecordFeedback(
            IncentiveType.BundleDiscount15,
            converted: false,
            revenueSavedDzd: 0m);

        stats.TotalPulls.Should().Be(1);
        stats.TotalConversions.Should().Be(0);
        stats.ConversionRate.Should().Be(0.0);
        stats.CumulativeRevenueLift.Should().Be(0m);
    }

    [Fact]
    public void RecordFeedback_AccumulatesRevenueLiftAccurately()
    {
        _banditService.ResetPriors();

        _banditService.RecordFeedback(IncentiveType.VipConciergeCall, converted: true, 12000m);
        _banditService.RecordFeedback(IncentiveType.VipConciergeCall, converted: false, 0m);
        var finalStats = _banditService.RecordFeedback(IncentiveType.VipConciergeCall, converted: true, 8000m);

        finalStats.TotalPulls.Should().Be(3);
        finalStats.TotalConversions.Should().Be(2);
        finalStats.ConversionRate.Should().BeApproximately(0.6667, 0.001);
        finalStats.CumulativeRevenueLift.Should().Be(20000m);
    }

    [Fact]
    public void Bandit_LearnsAndConvergesTowardsSuperiorArm()
    {
        _banditService.ResetPriors();

        // Simulate 40 rounds of feedback where BundleDiscount15 performs significantly better (80% vs 10%)
        for (int i = 0; i < 40; i++)
        {
            _banditService.RecordFeedback(IncentiveType.BundleDiscount15, converted: i % 5 != 0, 3000m); // 80% conversion
            _banditService.RecordFeedback(IncentiveType.ExpressShippingVoucher, converted: i % 10 == 0, 1000m); // 10% conversion
            _banditService.RecordFeedback(IncentiveType.FreeGiftWithPurchase, converted: i % 10 == 0, 1000m); // 10% conversion
            _banditService.RecordFeedback(IncentiveType.VipConciergeCall, converted: i % 10 == 0, 1000m); // 10% conversion
        }

        var report = _banditService.GetBanditStats();

        report.LeadingArm.Should().Be(IncentiveType.BundleDiscount15);
        var bundleStats = report.Arms.Single(a => a.IncentiveType == IncentiveType.BundleDiscount15);
        bundleStats.ConversionRate.Should().BeGreaterThan(0.70);
    }

    [Fact]
    public void GetBanditStats_ReturnsAccurateSummaryAndLeadingArm()
    {
        _banditService.ResetPriors();

        _banditService.RecordFeedback(IncentiveType.ExpressShippingVoucher, converted: true, 2000m);
        _banditService.RecordFeedback(IncentiveType.FreeGiftWithPurchase, converted: false, 0m);

        var response = _banditService.GetBanditStats();

        response.Success.Should().BeTrue();
        response.Arms.Should().HaveCount(4);
        response.TotalRevenueSavedDzd.Should().Be(2000m);
        response.LeadingArm.Should().Be(IncentiveType.ExpressShippingVoucher);
        response.OverallConversionRate.Should().Be(0.5);
    }

    #endregion

    #region 11. Omnichannel Cascading Dispatch & Channel Selection Tests

    [Fact]
    public void DetermineCascadeSequence_ForWhatsAppPrimary_OrdersCorrectly()
    {
        var cascade = _orchestrator.DetermineCascadeSequence(RetentionChannel.WhatsAppInteractive);

        cascade.Should().Equal(
            RetentionChannel.WhatsAppInteractive,
            RetentionChannel.AutomatedVoiceConcierge,
            RetentionChannel.TargetedEmail);
    }

    [Fact]
    public void DetermineCascadeSequence_ForVoicePrimary_OrdersCorrectly()
    {
        var cascade = _orchestrator.DetermineCascadeSequence(RetentionChannel.AutomatedVoiceConcierge);

        cascade.Should().Equal(
            RetentionChannel.AutomatedVoiceConcierge,
            RetentionChannel.WhatsAppInteractive,
            RetentionChannel.TargetedEmail);
    }

    [Fact]
    public void DetermineCascadeSequence_ForEmailPrimary_OrdersCorrectly()
    {
        var cascade = _orchestrator.DetermineCascadeSequence(RetentionChannel.TargetedEmail);

        cascade.Should().Equal(
            RetentionChannel.TargetedEmail,
            RetentionChannel.WhatsAppInteractive,
            RetentionChannel.AutomatedVoiceConcierge);
    }

    [Fact]
    public void SelectRecommendedChannel_HighWhatsAppEngagement_SelectsWhatsApp()
    {
        var signals = new CustomerBehaviorSignals(
            "CUST-WA", 25, 30, 0.83, 5000m, 0.5, 0,
            WhatsAppMessageOpenRate: 0.85,
            EmailClickRate: 0.10,
            BrowsingDropOffRatio: 0.20);

        var rec = _banditService.RecommendIncentive(signals);

        // When not VIP concierge call, customer with 85% WhatsApp open rate gets WhatsApp
        if (rec.SelectedIncentive.Type != IncentiveType.VipConciergeCall)
        {
            rec.RecommendedChannel.Should().Be(RetentionChannel.WhatsAppInteractive);
        }
    }

    [Fact]
    public void SelectRecommendedChannel_LowWhatsAppHighEmail_SelectsEmail()
    {
        var signals = new CustomerBehaviorSignals(
            "CUST-EMAIL", 25, 30, 0.83, 3000m, 0.5, 0,
            WhatsAppMessageOpenRate: 0.15,
            EmailClickRate: 0.45,
            BrowsingDropOffRatio: 0.20);

        var rec = _banditService.RecommendIncentive(signals);

        if (rec.SelectedIncentive.Type != IncentiveType.VipConciergeCall)
        {
            rec.RecommendedChannel.Should().Be(RetentionChannel.TargetedEmail);
        }
    }

    #endregion

    #region 12. Randomized 10% Holdout Control & Causal Lift Tests

    [Fact]
    public void DispatchIntervention_WithForceHoldout_AssignsHoldoutWithoutIncentive()
    {
        var signals = new CustomerBehaviorSignals("CUST-HOLD", 35, 30, 1.16, 5000m, 0.4, 0, 0.7, 0.2, 0.3);

        var intervention = _orchestrator.DispatchIntervention(signals, forceHoldout: true);

        intervention.IsHoldoutControl.Should().BeTrue();
        intervention.IncentiveApplied.Should().BeNull();
        intervention.DispatchStatus.Should().Be(InterventionStatus.Ignored);
        intervention.RevenueSavedDzd.Should().Be(0m);
    }

    [Fact]
    public void DispatchIntervention_WithBypassHoldout_DispatchesIncentive()
    {
        var signals = new CustomerBehaviorSignals("CUST-TREAT", 35, 30, 1.16, 5000m, 0.4, 0, 0.7, 0.2, 0.3);

        var intervention = _orchestrator.DispatchIntervention(signals, bypassHoldout: true);

        intervention.IsHoldoutControl.Should().BeFalse();
        intervention.IncentiveApplied.Should().NotBeNull();
        intervention.DispatchStatus.Should().Be(InterventionStatus.Dispatched);
    }

    [Fact]
    public void DispatchIntervention_RandomizedHoldout_ApproximatesTenPercentOverLargeSample()
    {
        var rng = new Random(999);
        int holdoutCount = 0;
        const int totalSamples = 1000;

        for (int i = 0; i < totalSamples; i++)
        {
            if (_orchestrator.IsCustomerInHoldout($"CUST-{i}", rng))
            {
                holdoutCount++;
            }
        }

        double ratio = (double)holdoutCount / totalSamples;
        ratio.Should().BeInRange(0.07, 0.13); // Centered around 10% holdout
    }

    [Fact]
    public void CalculateCausalLiftMetrics_ComputesIncrementalLiftAndNetRevenueSaved()
    {
        var orchestrator = new OmnichannelRetentionOrchestrator(_radarService, _banditService);

        // 1. Dispatch 10 treatment interventions (bypass holdout)
        List<RetentionInterventionRecord> treatments = [];
        for (int i = 0; i < 10; i++)
        {
            var signals = new CustomerBehaviorSignals($"TRT-{i}", 30, 30, 1.0, 5000m, 0.5, 0, 0.6, 0.2, 0.2);
            treatments.Add(orchestrator.DispatchIntervention(signals, bypassHoldout: true));
        }

        // 2. Dispatch 2 holdout control interventions
        List<RetentionInterventionRecord> controls = [];
        for (int i = 0; i < 2; i++)
        {
            var signals = new CustomerBehaviorSignals($"CTL-{i}", 30, 30, 1.0, 5000m, 0.5, 0, 0.6, 0.2, 0.2);
            controls.Add(orchestrator.DispatchIntervention(signals, forceHoldout: true));
        }

        // 3. Mark 4 treatment interventions as converted with 5,000 DZD revenue each
        for (int i = 0; i < 4; i++)
        {
            orchestrator.MarkInterventionOutcome(treatments[i].InterventionId, InterventionStatus.Converted, revenueSavedDzd: 5000m);
        }

        // 4. Leave control with 0 conversions (0% organic conversion)
        var metrics = orchestrator.CalculateCausalLiftMetrics();

        metrics.TotalInterventions.Should().Be(12);
        metrics.TreatmentCount.Should().Be(10);
        metrics.HoldoutControlCount.Should().Be(2);
        metrics.TreatmentConversions.Should().Be(4);
        metrics.ControlConversions.Should().Be(0);

        metrics.TreatmentConversionRate.Should().Be(0.40); // 4 / 10 = 40%
        metrics.ControlConversionRate.Should().Be(0.00);   // 0 / 2 = 0%
        metrics.IncrementalCausalLift.Should().Be(0.40);   // 40% - 0% = +40%

        metrics.TotalRevenueSavedDzd.Should().Be(20000m);  // 4 * 5000 = 20,000 DZD
        metrics.TotalIncentiveCostDzd.Should().BeGreaterThan(0m);
        metrics.NetRevenueSavedDzd.Should().Be(metrics.TotalRevenueSavedDzd - metrics.TotalIncentiveCostDzd);
    }

    [Fact]
    public void MarkInterventionOutcome_UpdatesStatusAndFeedsBackBandit()
    {
        _banditService.ResetPriors();
        var signals = new CustomerBehaviorSignals("CUST-FLOW", 30, 30, 1.0, 6000m, 0.5, 0, 0.8, 0.2, 0.2);

        var intervention = _orchestrator.DispatchIntervention(signals, bypassHoldout: true);
        var updated = _orchestrator.MarkInterventionOutcome(
            intervention.InterventionId,
            InterventionStatus.Converted,
            revenueSavedDzd: 7000m);

        updated.Should().NotBeNull();
        updated!.DispatchStatus.Should().Be(InterventionStatus.Converted);
        updated.RevenueSavedDzd.Should().Be(7000m);

        var armStats = _banditService.GetAllArmStats().Single(a => a.IncentiveType == intervention.IncentiveApplied!.Type);
        armStats.TotalConversions.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GetInterventionHistory_FiltersByCustomerId()
    {
        var s1 = new CustomerBehaviorSignals("TARGET-CUST", 25, 30, 0.83, 4000m, 0.5, 0, 0.5, 0.2, 0.2);
        var s2 = new CustomerBehaviorSignals("OTHER-CUST", 25, 30, 0.83, 4000m, 0.5, 0, 0.5, 0.2, 0.2);

        _orchestrator.DispatchIntervention(s1, bypassHoldout: true);
        _orchestrator.DispatchIntervention(s2, bypassHoldout: true);

        var targetHistory = _orchestrator.GetInterventionHistory("TARGET-CUST");

        targetHistory.Should().NotBeEmpty();
        targetHistory.Should().AllSatisfy(h => h.CustomerId.Should().Be("TARGET-CUST"));
    }

    #endregion

    #region 13. Dependency Injection & Health Checks Tests

    [Fact]
    public void AddPredictiveRetentionServices_RegistersRequiredServicesAsSingletons_WithoutBuildingProvider()
    {
        var services = new ServiceCollection();

        services.AddPredictiveRetentionServices();

        services.Should().Contain(d =>
            d.ServiceType == typeof(IPredictiveChurnRadarService) &&
            d.ImplementationType == typeof(PredictiveChurnRadarService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IRetentionPolicyBanditService) &&
            d.ImplementationType == typeof(RetentionPolicyBanditService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        services.Should().Contain(d =>
            d.ServiceType == typeof(IOmnichannelRetentionOrchestrator) &&
            d.ImplementationType == typeof(OmnichannelRetentionOrchestrator) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void RetentionEngineHealth_ContainsRequiredCapabilities()
    {
        var health = new RetentionEngineHealth(
            Status: "Operational",
            Version: "1.0.0-phase30",
            TimestampUtc: DateTime.UtcNow,
            Capabilities: new Dictionary<string, string>
            {
                ["SurvivalAnalysis"] = "Cox Proportional Hazards",
                ["BanditPolicy"] = "Thompson Sampling",
                ["OmnichannelDispatch"] = "Cascading Channels",
                ["CausalExperimentation"] = "10% Holdout Control"
            });

        health.Status.Should().Be("Operational");
        health.Version.Should().Be("1.0.0-phase30");
        health.Capabilities.Should().ContainKey("SurvivalAnalysis");
        health.Capabilities.Should().ContainKey("BanditPolicy");
        health.Capabilities.Should().ContainKey("OmnichannelDispatch");
        health.Capabilities.Should().ContainKey("CausalExperimentation");
    }

    #endregion
}
