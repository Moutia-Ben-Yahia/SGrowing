using FluentAssertions;
using SGrowing.Api.Modules.Commerce.DynamicPricing;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class DynamicElasticityPricingTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly PriceElasticityEngine _elasticityEngine = new();
    private readonly CompetitorScraperMeshService _scraperMesh = new();
    private readonly MarginGuardrailValidator _guardrailValidator = new();

    #region 1. Price Elasticity Engine Tests (Arc Elasticity Midpoint Formula)

    [Fact]
    public void CalculateArcElasticity_WithElasticDemand_ShouldCalculateCorrectlyAndClassifyAsElastic()
    {
        // Price rises from $100 to $120 (+20%)
        // Demand drops from 1000 to 750 (-25%)
        // Arc formula: ((750 - 1000) / (1000 + 750)) / ((120 - 100) / (100 + 120))
        // = (-250 / 1750) / (20 / 220) = (-0.142857) / (0.090909) ≈ -1.5714
        decimal p1 = 100m, q1 = 1000m;
        decimal p2 = 120m, q2 = 750m;

        var result = _elasticityEngine.CalculateArcElasticity(p1, q1, p2, q2);

        result.Elasticity.Should().BeApproximately(-1.5714m, 0.001m);
        result.AbsoluteElasticity.Should().BeGreaterThan(1.0m);
        result.Category.Should().Be(ElasticityCategory.Elastic);
        result.PriceDeltaPercent.Should().Be(20.0m);
        result.QuantityDeltaPercent.Should().Be(-25.0m);
        result.Interpretation.Should().Contain("Elastic demand");
    }

    [Fact]
    public void CalculateArcElasticity_WithInelasticDemand_ShouldClassifyAsInelastic()
    {
        // Price rises from $100 to $120 (+20%)
        // Demand drops slightly from 1000 to 950 (-5%)
        // Arc formula: (-50 / 1950) / (20 / 220) = (-0.025641) / (0.090909) ≈ -0.2821
        decimal p1 = 100m, q1 = 1000m;
        decimal p2 = 120m, q2 = 950m;

        var result = _elasticityEngine.CalculateArcElasticity(p1, q1, p2, q2);

        result.Elasticity.Should().BeApproximately(-0.2821m, 0.001m);
        result.AbsoluteElasticity.Should().BeLessThan(1.0m);
        result.Category.Should().Be(ElasticityCategory.Inelastic);
        result.Interpretation.Should().Contain("Inelastic demand");
    }

    [Fact]
    public void CalculateArcElasticity_WithUnitaryDemand_ShouldClassifyAsUnitary()
    {
        // Price rises from $100 to $120
        // Demand drops from 120 to 100
        // ΔQ/SumQ = -20/220; ΔP/SumP = 20/220 => ε = -1.0000
        decimal p1 = 100m, q1 = 120m;
        decimal p2 = 120m, q2 = 100m;

        var result = _elasticityEngine.CalculateArcElasticity(p1, q1, p2, q2);

        result.Elasticity.Should().Be(-1.0000m);
        result.AbsoluteElasticity.Should().Be(1.0000m);
        result.Category.Should().Be(ElasticityCategory.Unitary);
        result.Interpretation.Should().Contain("Unitary elasticity");
    }

    [Fact]
    public void CalculateArcElasticity_WhenPriceUnchanged_ShouldReturnZeroUndefined()
    {
        var result = _elasticityEngine.CalculateArcElasticity(50m, 200m, 50m, 250m);

        result.Elasticity.Should().Be(0.0000m);
        result.Category.Should().Be(ElasticityCategory.Inelastic);
        result.PriceDeltaPercent.Should().Be(0m);
        result.Interpretation.Should().Contain("undefined / zero");
    }

    [Fact]
    public void CalculateArcElasticity_WhenQuantityUnchanged_ShouldReturnZeroPerfectInelastic()
    {
        var result = _elasticityEngine.CalculateArcElasticity(40m, 500m, 60m, 500m);

        result.Elasticity.Should().Be(0.0000m);
        result.Category.Should().Be(ElasticityCategory.Inelastic);
        result.QuantityDeltaPercent.Should().Be(0m);
        result.Interpretation.Should().Contain("Perfectly Inelastic");
    }

    [Fact]
    public void CalculateArcElasticity_WithNegativeInputs_ShouldThrowArgumentOutOfRangeException()
    {
        var act = () => _elasticityEngine.CalculateArcElasticity(-10m, 100m, 20m, 80m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region 2. Demand Estimation & Margin Maximization Tests

    [Fact]
    public void EstimateDemand_InvertedMidpointFormula_ShouldAccuratelyRecoverOriginalQuantity()
    {
        // Baseline: Price = 100, Demand = 1000
        // Observed at Price = 120: Demand = 800
        // ΔQ/SumQ = -200/1800 = -1/9; ΔP/SumP = 20/220 = 1/11 => ε = -11/9 ≈ -1.2222m
        decimal basePrice = 100m;
        decimal baseDemand = 1000m;
        decimal elasticity = -1.222222m;

        decimal estimatedAt120 = _elasticityEngine.EstimateDemand(120m, basePrice, baseDemand, elasticity);

        estimatedAt120.Should().BeApproximately(800m, 1.0m);
    }

    [Fact]
    public void EstimateDemand_WhenTargetPriceDramaticallyHigh_ShouldClampToZeroDemand()
    {
        decimal basePrice = 50m;
        decimal baseDemand = 100m;
        decimal elasticity = -2.5m; // highly elastic

        decimal estimatedAtAstronomicalPrice = _elasticityEngine.EstimateDemand(500m, basePrice, baseDemand, elasticity);

        estimatedAtAstronomicalPrice.Should().Be(0m);
    }

    [Fact]
    public void ComputeOptimalPrice_DiscreteCandidates_ShouldSelectPriceMaximizingTotalContributionMargin()
    {
        // UnitCost = 30
        // Candidates: 40, 50, 60, 70
        // BasePrice = 50, BaseDemand = 200, Elasticity = -1.5
        var request = new OptimalPriceSearchRequest(
            BasePrice: 50m,
            BaseDemand: 200m,
            UnitCost: 30m,
            Elasticity: -1.5m,
            CandidatePrices: [40m, 50m, 60m, 70m]);

        var result = _elasticityEngine.ComputeOptimalPrice(request);

        result.OptimalPrice.Should().BeOneOf(40m, 50m, 60m, 70m);
        result.TotalContributionMargin.Should().BeGreaterThan(0m);
        result.EvaluatedPoints.Should().HaveCount(4);

        // Optimal point must have the maximum margin among evaluated points
        var maxMarginPoint = result.EvaluatedPoints.MaxBy(p => p.TotalContributionMargin);
        result.OptimalPrice.Should().Be(maxMarginPoint!.Price);
        result.TotalContributionMargin.Should().Be(maxMarginPoint.TotalContributionMargin);
    }

    [Fact]
    public void ComputeOptimalPrice_ContinuousRangeSearch_ShouldFindOptimalPriceAndPositiveMargin()
    {
        var request = new OptimalPriceSearchRequest(
            BasePrice: 100m,
            BaseDemand: 500m,
            UnitCost: 60m,
            Elasticity: -1.2m,
            MinPrice: 70m,
            MaxPrice: 150m,
            StepSize: 5.0m);

        var result = _elasticityEngine.ComputeOptimalPrice(request);

        result.OptimalPrice.Should().BeInRange(70m, 150m);
        result.UnitMargin.Should().Be(result.OptimalPrice - 60m);
        result.TotalContributionMargin.Should().BeGreaterThan(0m);
        result.EvaluatedPoints.Should().NotBeEmpty();
    }

    #endregion

    #region 3. Inventory Velocity & Time-of-Day Traffic Weighting Tests

    [Fact]
    public void CalculateStockVelocityMultiplier_WhenDaysOfSupplyCriticallyLow_ShouldApplyScarcityMarkup()
    {
        // Critically low inventory (< 7 days of supply)
        decimal multZeroDays = _elasticityEngine.CalculateStockVelocityMultiplier(0m);
        decimal multThreeDays = _elasticityEngine.CalculateStockVelocityMultiplier(3.5m);
        decimal multSixDays = _elasticityEngine.CalculateStockVelocityMultiplier(6.5m);

        multZeroDays.Should().Be(1.2500m); // Maximum scarcity markup (+25%)
        multThreeDays.Should().BeGreaterThan(1.10m);
        multSixDays.Should().BeGreaterThan(1.05m);
    }

    [Fact]
    public void CalculateStockVelocityMultiplier_WhenDaysOfSupplyExcessStagnant_ShouldApplyLiquidationDiscount()
    {
        // Excess inventory (> 60 days of supply)
        decimal multSixtyOneDays = _elasticityEngine.CalculateStockVelocityMultiplier(61m);
        decimal multOneHundredDays = _elasticityEngine.CalculateStockVelocityMultiplier(100m);
        decimal multTwoHundredDays = _elasticityEngine.CalculateStockVelocityMultiplier(200m);

        multSixtyOneDays.Should().BeLessThan(1.0m);
        multOneHundredDays.Should().BeLessThan(multSixtyOneDays);
        multTwoHundredDays.Should().Be(0.8500m); // 15% discount floor
    }

    [Fact]
    public void CalculateStockVelocityMultiplier_WhenDaysOfSupplyHealthy_ShouldReturnNeutral()
    {
        // Healthy inventory (7 to 60 days of supply)
        decimal mult7 = _elasticityEngine.CalculateStockVelocityMultiplier(7.0m);
        decimal mult30 = _elasticityEngine.CalculateStockVelocityMultiplier(30.0m);
        decimal mult60 = _elasticityEngine.CalculateStockVelocityMultiplier(60.0m);

        mult7.Should().Be(1.0000m);
        mult30.Should().Be(1.0000m);
        mult60.Should().Be(1.0000m);
    }

    [Fact]
    public void CalculateTimeOfDayMultiplier_PeakHours_ShouldApplySurgeFactor()
    {
        // Prime evening shopping hours (18:00 - 22:00) => +5% surge
        decimal multEvening = _elasticityEngine.CalculateTimeOfDayMultiplier(19);
        multEvening.Should().Be(1.0500m);

        // Lunch surge (12:00 - 14:00) => +2% surge
        decimal multLunch = _elasticityEngine.CalculateTimeOfDayMultiplier(13);
        multLunch.Should().Be(1.0200m);
    }

    [Fact]
    public void CalculateTimeOfDayMultiplier_OffPeakHours_ShouldApplyOffPeakDiscount()
    {
        // Overnight off-peak (01:00 - 05:00) => -4%
        decimal multNight = _elasticityEngine.CalculateTimeOfDayMultiplier(3);
        multNight.Should().Be(0.9600m);

        // Standard daytime (10:00) => 1.0000
        decimal multDay = _elasticityEngine.CalculateTimeOfDayMultiplier(10);
        multDay.Should().Be(1.0000m);
    }

    [Fact]
    public void AdjustPriceForVelocityAndTraffic_CombinesBothMultipliersAccurately()
    {
        decimal basePrice = 100m;
        decimal daysOfSupply = 0m; // +25% scarcity (1.25)
        int hour = 20;             // +5% evening surge (1.05)

        var adjustment = _elasticityEngine.AdjustPriceForVelocityAndTraffic(basePrice, daysOfSupply, hour);

        // Combined = 1.25 * 1.05 = 1.3125
        adjustment.CombinedMultiplier.Should().Be(1.3125m);
        adjustment.AdjustedPrice.Should().Be(131.25m);
        adjustment.VelocityReasoning.Should().Contain("scarcity markup");
        adjustment.TrafficReasoning.Should().Contain("Traffic weighting");
    }

    #endregion

    #region 4. Competitor Scraper Mesh & Intelligence Tests

    [Fact]
    public async Task CompetitorScraperMesh_RecordAndAggregate_ShouldCalculateAccurateMedianMinMaxSpread()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "TEST-SKU-100";

        // Record 5 competitor observations
        List<CompetitorPriceObservation> observations =
        [
            new(Guid.NewGuid(), _tenantId, sku, "CompetitorA", 100m, true),
            new(Guid.NewGuid(), _tenantId, sku, "CompetitorB", 110m, true),
            new(Guid.NewGuid(), _tenantId, sku, "CompetitorC", 120m, true),
            new(Guid.NewGuid(), _tenantId, sku, "CompetitorD", 130m, true),
            new(Guid.NewGuid(), _tenantId, sku, "CompetitorE", 140m, true)
        ];

        await _scraperMesh.RecordObservationsBatchAsync(_tenantId, observations);

        var metrics = await _scraperMesh.GetMarketMetricsAsync(_tenantId, sku);

        metrics.TotalCompetitorsObserved.Should().Be(5);
        metrics.InStockCount.Should().Be(5);
        metrics.OutOfStockCount.Should().Be(0);
        metrics.LowestPrice.Should().Be(100m);
        metrics.HighestPrice.Should().Be(140m);
        metrics.MedianPrice.Should().Be(120m);
        metrics.AveragePrice.Should().Be(120m);
        metrics.LowestInStockPrice.Should().Be(100m);
        metrics.HighestInStockPrice.Should().Be(140m);

        // Price Spread = ((140 - 100) / 120) * 100 = 33.33%
        metrics.PriceSpreadPercentage.Should().Be(33.33m);
        metrics.HasStockOutCompetitor.Should().BeFalse();
        metrics.AreAllCompetitorsOutOfStock.Should().BeFalse();
    }

    [Fact]
    public async Task CompetitorScraperMesh_DetectStockOuts_ShouldIdentifyWhenCompetitorOutOfStock()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "TEST-SKU-STOCKOUT";

        List<CompetitorPriceObservation> observations =
        [
            new(Guid.NewGuid(), _tenantId, sku, "CompetitorInStock", 150m, true),
            new(Guid.NewGuid(), _tenantId, sku, "CompetitorOutOfStock", 120m, false) // out of stock
        ];

        await _scraperMesh.RecordObservationsBatchAsync(_tenantId, observations);

        var metrics = await _scraperMesh.GetMarketMetricsAsync(_tenantId, sku);

        metrics.InStockCount.Should().Be(1);
        metrics.OutOfStockCount.Should().Be(1);
        metrics.HasStockOutCompetitor.Should().BeTrue();
        metrics.LowestPrice.Should().Be(120m); // Overall lowest is 120
        metrics.LowestInStockPrice.Should().Be(150m); // Lowest IN STOCK is 150
        metrics.StockOutOpportunityDetected.Should().BeTrue(); // Lowest competitor is out of stock!
    }

    [Fact]
    public async Task CompetitorScraperMesh_WhenAllCompetitorsOutOfStock_ShouldFlagAllOutOfStock()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "TEST-SKU-ALL-OOS";

        List<CompetitorPriceObservation> observations =
        [
            new(Guid.NewGuid(), _tenantId, sku, "Comp1", 200m, false),
            new(Guid.NewGuid(), _tenantId, sku, "Comp2", 210m, false)
        ];

        await _scraperMesh.RecordObservationsBatchAsync(_tenantId, observations);

        var metrics = await _scraperMesh.GetMarketMetricsAsync(_tenantId, sku);

        metrics.InStockCount.Should().Be(0);
        metrics.OutOfStockCount.Should().Be(2);
        metrics.AreAllCompetitorsOutOfStock.Should().BeTrue();
        metrics.StockOutOpportunityDetected.Should().BeTrue();
        metrics.LowestInStockPrice.Should().BeNull();
    }

    [Fact]
    public async Task CompetitorScraperMesh_SimulateScrapeTarget_RotatesProxiesAndHandlesTransientRetry()
    {
        _scraperMesh.ResetState(_tenantId);

        var request = new ScrapeTargetRequest(
            CompetitorName: "JumiaDZ",
            Sku: "PHONE-GALAXY-A54",
            TargetUrl: "https://jumia.dz/phones/galaxy-a54",
            SimulatedPrice: 48500m,
            InStock: true,
            PromotionalPrice: 47000m,
            ShippingFee: 600m,
            Currency: "DZD",
            SimulateTransientFailure: true);

        var result = await _scraperMesh.SimulateScrapeTargetAsync(_tenantId, request);

        result.Success.Should().BeTrue();
        result.RetriesAttempted.Should().Be(1); // Retried after transient 429 backoff
        result.Observation.Should().NotBeNull();
        result.Observation!.EffectivePrice.Should().Be(47600m); // 47000 + 600 shipping
        result.Observation.ProxyNodeUsed.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region 5. Margin Guardrail Validator Tests

    [Fact]
    public void MarginGuardrail_CalculateFloorPrice_ShouldAccountForCogsShippingGatewayAndMinMargin()
    {
        // COGS: $50
        // Shipping: $10
        // Gateway: 2.5% fee
        // MinMarginAmount: $10
        // MinMarginPercentage: 12%
        var profile = new MarginCostProfile(
            Cogs: 50m,
            EstimatedShippingCost: 10m,
            PaymentGatewayFixedFee: 0m,
            PaymentGatewayFeePercentage: 0.025m,
            MinMarginAmount: 10m,
            MinMarginPercentage: 0.12m);

        decimal floor = _guardrailValidator.CalculateFloorPrice(profile);

        // Direct costs: 60
        // Floor from amount: (60 + 10) / (1 - 0.025) = 70 / 0.975 = 71.79
        // Floor from percentage: 60 / (1 - 0.025 - 0.12) = 60 / 0.855 = 70.18
        // Effective floor: max(71.79, 70.18) = 71.79
        floor.Should().Be(71.79m);
    }

    [Fact]
    public void MarginGuardrail_Validate_WhenPriceAboveFloor_ShouldPassValidation()
    {
        var profile = new MarginCostProfile(
            Cogs: 40m,
            EstimatedShippingCost: 5m,
            PaymentGatewayFeePercentage: 0.02m,
            MinMarginPercentage: 0.15m);

        decimal floor = _guardrailValidator.CalculateFloorPrice(profile);
        decimal proposedPrice = floor + 20m; // Well above floor

        var result = _guardrailValidator.Validate(proposedPrice, profile);

        result.IsValid.Should().BeTrue();
        result.Alerts.Should().BeEmpty();
        result.EstimatedNetMargin.Should().BeGreaterThan(0m);
        result.EstimatedMarginPercentage.Should().BeGreaterThan(15.0m);
    }

    [Fact]
    public void MarginGuardrail_Validate_WhenPriceBelowFloor_ShouldFlagBelowMarginFloorAlert()
    {
        var profile = new MarginCostProfile(
            Cogs: 100m,
            EstimatedShippingCost: 15m,
            MinMarginAmount: 20m,
            MinMarginPercentage: 0.15m);

        decimal floor = _guardrailValidator.CalculateFloorPrice(profile);
        decimal proposedBelowFloor = floor - 10m;

        var result = _guardrailValidator.Validate(proposedBelowFloor, profile);

        result.IsValid.Should().BeFalse();
        result.Alerts.Should().Contain(a => a.AlertCode == "BELOW_MARGIN_FLOOR");
        result.RecommendedClampedPrice.Should().Be(floor);
    }

    [Fact]
    public void MarginGuardrail_Validate_WhenPriceBelowCogs_ShouldFlagCriticalBelowCogsAlert()
    {
        var profile = new MarginCostProfile(
            Cogs: 80m,
            EstimatedShippingCost: 10m);

        decimal proposedPrice = 60m; // Less than COGS of 80

        var result = _guardrailValidator.Validate(proposedPrice, profile);

        result.IsValid.Should().BeFalse();
        result.Alerts.Should().Contain(a => a.AlertCode == "BELOW_COGS");
        result.Alerts.Should().Contain(a => a.AlertCode == "NEGATIVE_CONTRIBUTION_MARGIN");
    }

    [Fact]
    public void MarginGuardrail_Validate_WhenPredatoryExtremeDiscountFromMsrp_ShouldFlagAlert()
    {
        var profile = new MarginCostProfile(
            Cogs: 20m,
            ReferenceMsrp: 200m,
            MaxDiscountFromMsrpPercentage: 0.50m); // Max 50% discount allowed

        decimal deeplyDiscountedPrice = 70m; // 65% discount from MSRP

        var result = _guardrailValidator.Validate(deeplyDiscountedPrice, profile);

        result.IsValid.Should().BeFalse();
        result.Alerts.Should().Contain(a => a.AlertCode == "PREDATORY_UNDERCUT_RISK");
    }

    [Fact]
    public void MarginGuardrail_EnforceGuardrail_WhenPriceBreachesFloor_ShouldThrowException()
    {
        var profile = new MarginCostProfile(Cogs: 50m, MinMarginAmount: 10m);
        decimal belowPrice = 30m;

        var act = () => _guardrailValidator.EnforceGuardrail(belowPrice, profile);

        act.Should().Throw<MarginGuardrailViolationException>()
            .Which.ValidationResult.IsValid.Should().BeFalse();
    }

    #endregion

    #region 6. Dynamic BuyBox Repricer Service Tests

    [Fact]
    public async Task Repricer_AggressiveUndercut_ShouldUndercutLowestInStockCompetitorByDelta()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "LAPTOP-THINKPAD-X1";

        // Setup competitor signals
        await _scraperMesh.RecordObservationsBatchAsync(_tenantId,
        [
            new(Guid.NewGuid(), _tenantId, sku, "StoreAlpha", 1200m, true),
            new(Guid.NewGuid(), _tenantId, sku, "StoreBeta", 1150m, true), // lowest in-stock: 1150
            new(Guid.NewGuid(), _tenantId, sku, "StoreGamma", 1180m, true)
        ]);

        var repricer = new DynamicBuyBoxRepricerService(_scraperMesh, _elasticityEngine, _guardrailValidator);

        var request = new RepricingRequest(
            TenantId: _tenantId,
            Sku: sku,
            CurrentPrice: 1250m,
            Strategy: RepricingStrategy.AggressiveUndercut,
            CostProfile: new MarginCostProfile(Cogs: 800m, EstimatedShippingCost: 20m),
            UndercutDelta: 0.05m);

        var recommendation = await repricer.RepriceAsync(request);

        // 1150 - 0.05 = 1149.95
        recommendation.RecommendedPrice.Should().Be(1149.95m);
        recommendation.StrategyApplied.Should().Be(RepricingStrategy.AggressiveUndercut);
        recommendation.StrategyJustification.Should().Contain("Aggressive undercut");
        recommendation.GuardrailValidation.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Repricer_AggressiveUndercut_WhenUndercutWouldBreachFloor_ShouldClampToMarginFloor()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "TABLET-IPAD-AIR";

        // Lowest competitor is selling at 500 (below our profitable floor)
        await _scraperMesh.RecordObservationAsync(_tenantId, new(Guid.NewGuid(), _tenantId, sku, "DumpingVendor", 500m, true));

        var repricer = new DynamicBuyBoxRepricerService(_scraperMesh, _elasticityEngine, _guardrailValidator);

        // Cost structure requires floor of ~$600
        var profile = new MarginCostProfile(Cogs: 550m, EstimatedShippingCost: 20m, MinMarginAmount: 30m);
        decimal floorPrice = _guardrailValidator.CalculateFloorPrice(profile);

        var request = new RepricingRequest(
            TenantId: _tenantId,
            Sku: sku,
            CurrentPrice: 650m,
            Strategy: RepricingStrategy.AggressiveUndercut,
            CostProfile: profile,
            UndercutDelta: 1.00m);

        var recommendation = await repricer.RepriceAsync(request);

        // Would attempt 499, but must be clamped to floorPrice
        recommendation.RecommendedPrice.Should().Be(floorPrice);
        recommendation.StrategyJustification.Should().Contain("Floor Guardrail Enforced");
    }

    [Fact]
    public async Task Repricer_AggressiveUndercut_WhenLowestCompetitorOutOfStock_ShouldIgnoreOutOfStockPrice()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "HEADPHONES-WH1000XM5";

        // OOS competitor at 300, in-stock competitor at 380
        await _scraperMesh.RecordObservationsBatchAsync(_tenantId,
        [
            new(Guid.NewGuid(), _tenantId, sku, "OutOfStockSeller", 300m, false), // OOS!
            new(Guid.NewGuid(), _tenantId, sku, "RealInStockSeller", 380m, true)
        ]);

        var repricer = new DynamicBuyBoxRepricerService(_scraperMesh, _elasticityEngine, _guardrailValidator);

        var request = new RepricingRequest(
            TenantId: _tenantId,
            Sku: sku,
            CurrentPrice: 400m,
            Strategy: RepricingStrategy.AggressiveUndercut,
            CostProfile: new MarginCostProfile(Cogs: 220m),
            UndercutDelta: 0.10m);

        var recommendation = await repricer.RepriceAsync(request);

        // Must undercut in-stock competitor at 380 => 379.90, ignoring the 300 phantom price
        recommendation.RecommendedPrice.Should().Be(379.90m);
        recommendation.MarketLowestPrice.Should().Be(300m);
        recommendation.MarketLowestInStockPrice.Should().Be(380m);
    }

    [Fact]
    public async Task Repricer_AggressiveUndercut_WhenAllCompetitorsOutOfStock_ShouldApplyStockOutPremium()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "GPU-RTX-5090";

        // All competitors OOS
        await _scraperMesh.RecordObservationsBatchAsync(_tenantId,
        [
            new(Guid.NewGuid(), _tenantId, sku, "StoreA", 2000m, false),
            new(Guid.NewGuid(), _tenantId, sku, "StoreB", 2100m, false)
        ]);

        var repricer = new DynamicBuyBoxRepricerService(_scraperMesh, _elasticityEngine, _guardrailValidator);

        var request = new RepricingRequest(
            TenantId: _tenantId,
            Sku: sku,
            CurrentPrice: 2000m,
            Strategy: RepricingStrategy.AggressiveUndercut,
            CostProfile: new MarginCostProfile(Cogs: 1400m));

        var recommendation = await repricer.RepriceAsync(request);

        recommendation.StockOutOpportunity.Should().BeTrue();
        // Price should be raised (+8% premium: 2000 * 1.08 = 2160)
        recommendation.RecommendedPrice.Should().BeGreaterThan(2000m);
        recommendation.StrategyJustification.Should().Contain("Stock-out opportunity detected");
    }

    [Fact]
    public async Task Repricer_MatchMarketMedian_ShouldAlignWithMarketMedianPrice()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "CAMERA-SONY-A7IV";

        await _scraperMesh.RecordObservationsBatchAsync(_tenantId,
        [
            new(Guid.NewGuid(), _tenantId, sku, "Dealer1", 2400m, true),
            new(Guid.NewGuid(), _tenantId, sku, "Dealer2", 2500m, true),
            new(Guid.NewGuid(), _tenantId, sku, "Dealer3", 2600m, true)
        ]);

        var repricer = new DynamicBuyBoxRepricerService(_scraperMesh, _elasticityEngine, _guardrailValidator);

        var request = new RepricingRequest(
            TenantId: _tenantId,
            Sku: sku,
            CurrentPrice: 2800m,
            Strategy: RepricingStrategy.MatchMarketMedian,
            CostProfile: new MarginCostProfile(Cogs: 1800m));

        var recommendation = await repricer.RepriceAsync(request);

        // Median of 2400, 2500, 2600 is 2500
        recommendation.RecommendedPrice.Should().Be(2500m);
        recommendation.StrategyApplied.Should().Be(RepricingStrategy.MatchMarketMedian);
        recommendation.StrategyJustification.Should().Contain("Match Market Median");
    }

    [Fact]
    public async Task Repricer_MarginMaximizer_ShouldFindElasticityOptimalPrice()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "SMARTWATCH-ULTRA";

        var repricer = new DynamicBuyBoxRepricerService(_scraperMesh, _elasticityEngine, _guardrailValidator);

        var request = new RepricingRequest(
            TenantId: _tenantId,
            Sku: sku,
            CurrentPrice: 400m,
            Strategy: RepricingStrategy.MarginMaximizer,
            CostProfile: new MarginCostProfile(Cogs: 220m),
            BaselineDemand: 150m,
            EstimatedElasticity: -1.4m);

        var recommendation = await repricer.RepriceAsync(request);

        recommendation.StrategyApplied.Should().Be(RepricingStrategy.MarginMaximizer);
        recommendation.RecommendedPrice.Should().BeGreaterThan(220m);
        recommendation.ExpectedDemand.Should().BeGreaterThan(0m);
        recommendation.ExpectedUnitMargin.Should().BeGreaterThan(0m);
        recommendation.StrategyJustification.Should().Contain("Margin Maximizer");
    }

    [Fact]
    public async Task Repricer_WithDaysOfSupplyAdjustment_ShouldAdjustPriceUpwardForCriticallyLowStock()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "LIMITED-EDITION-SNEAKER";

        await _scraperMesh.RecordObservationAsync(_tenantId, new(Guid.NewGuid(), _tenantId, sku, "CompShop", 200m, true));

        var repricer = new DynamicBuyBoxRepricerService(_scraperMesh, _elasticityEngine, _guardrailValidator);

        var request = new RepricingRequest(
            TenantId: _tenantId,
            Sku: sku,
            CurrentPrice: 200m,
            Strategy: RepricingStrategy.AggressiveUndercut,
            CostProfile: new MarginCostProfile(Cogs: 100m),
            UndercutDelta: 0.05m,
            DaysOfSupply: 1.0m); // < 7 days critically low inventory

        var recommendation = await repricer.RepriceAsync(request);

        // Base undercut would be 199.95, but scarcity factor (~1.22x) increases price
        recommendation.RecommendedPrice.Should().BeGreaterThan(210m);
        recommendation.StrategyJustification.Should().Contain("scarcity markup");
    }

    [Fact]
    public async Task Repricer_EndToEndRecommendation_CalculatesMarginLiftAndGeneratesJustification()
    {
        _scraperMesh.ResetState(_tenantId);
        string sku = "COFFEE-MACHINE-DELUXE";

        await _scraperMesh.RecordObservationsBatchAsync(_tenantId,
        [
            new(Guid.NewGuid(), _tenantId, sku, "RetailerA", 520m, true),
            new(Guid.NewGuid(), _tenantId, sku, "RetailerB", 540m, true),
            new(Guid.NewGuid(), _tenantId, sku, "RetailerC", 580m, true)
        ]);

        var repricer = new DynamicBuyBoxRepricerService(_scraperMesh, _elasticityEngine, _guardrailValidator);

        var request = new RepricingRequest(
            TenantId: _tenantId,
            Sku: sku,
            CurrentPrice: 500m,
            Strategy: RepricingStrategy.AggressiveUndercut,
            CostProfile: new MarginCostProfile(Cogs: 300m, EstimatedShippingCost: 15m),
            UndercutDelta: 0.10m);

        var recommendation = await repricer.RepriceAsync(request);

        recommendation.OriginalPrice.Should().Be(500m);
        // Undercut 520 => 519.90
        recommendation.RecommendedPrice.Should().Be(519.90m);
        recommendation.ExpectedUnitMargin.Should().BeApproximately(519.90m - 315m, 1.0m);
        recommendation.MarginChangePercentage.Should().BeGreaterThan(0m); // Margin grew because price rose from 500 to 519.90!
        recommendation.PriceSpreadPercentage.Should().BeApproximately(((580m - 520m) / 540m) * 100m, 1.0m);
    }

    #endregion

    #region 7. Service Collection DI & Additional Edge Cases

    [Fact]
    public void AddDynamicElasticityPricingServices_RegistersAllRequiredDependencies()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddDynamicElasticityPricingServices();

        services.Should().Contain(d => d.ServiceType == typeof(IPriceElasticityEngine) && d.ImplementationType == typeof(PriceElasticityEngine));
        services.Should().Contain(d => d.ServiceType == typeof(ICompetitorScraperMeshService) && d.ImplementationType == typeof(CompetitorScraperMeshService));
        services.Should().Contain(d => d.ServiceType == typeof(IMarginGuardrailValidator) && d.ImplementationType == typeof(MarginGuardrailValidator));
        services.Should().Contain(d => d.ServiceType == typeof(IDynamicBuyBoxRepricerService) && d.ImplementationType == typeof(DynamicBuyBoxRepricerService));
    }

    [Fact]
    public void CalculateArcElasticity_WithPriceDropAndQuantitySurge_CalculatesElasticityCorrectly()
    {
        // Price decreases from 100 to 80 (-20%)
        // Quantity jumps from 100 to 160 (+60%)
        // Numerator: (160 - 100) / (100 + 160) = 60 / 260 = 0.230769
        // Denominator: (80 - 100) / (100 + 80) = -20 / 180 = -0.111111
        // Elasticity = 0.230769 / -0.111111 = -2.0769
        var result = _elasticityEngine.CalculateArcElasticity(100m, 100m, 80m, 160m);

        result.Elasticity.Should().BeApproximately(-2.0769m, 0.001m);
        result.Category.Should().Be(ElasticityCategory.Elastic);
        result.PriceDeltaPercent.Should().Be(-20.0m);
        result.QuantityDeltaPercent.Should().Be(60.0m);
    }

    [Fact]
    public void CompetitorPriceObservation_EffectivePrice_CalculatesPromotionsAndShippingCorrectly()
    {
        var obs = new CompetitorPriceObservation(
            ObservationId: Guid.NewGuid(),
            TenantId: _tenantId,
            Sku: "GADGET-XYZ",
            CompetitorName: "ShopOnline",
            ObservedPrice: 5000m,
            InStock: true,
            PromotionalPrice: 4200m,
            ShippingFee: 400m);

        obs.EffectivePrice.Should().Be(4600m);
    }

    [Fact]
    public void CompetitorScraperMesh_GetActiveProxyPool_ReturnsConfiguredNodes()
    {
        var pool = _scraperMesh.GetActiveProxyPool();

        pool.Should().NotBeEmpty();
        pool.Should().Contain(p => p.Region == "Algiers");
        pool.Should().Contain(p => p.Region == "Paris");
    }

    [Fact]
    public void MarginGuardrail_WithGatewayFeePercentage_CalculatesAdequateFloor()
    {
        // Direct costs: 100, Gateway fee: 5% (0.05), Min margin amount: 20
        // Floor should satisfy: Floor * (1 - 0.05) - 100 >= 20 => Floor * 0.95 >= 120 => Floor >= 126.32
        var profile = new MarginCostProfile(
            Cogs: 80m,
            EstimatedShippingCost: 20m,
            PaymentGatewayFeePercentage: 0.05m,
            MinMarginAmount: 20m,
            MinMarginPercentage: 0m);

        decimal floor = _guardrailValidator.CalculateFloorPrice(profile);
        floor.Should().Be(126.32m);

        // At floor price 126.32:
        // Gateway takes: 126.32 * 0.05 = 6.316 -> 6.32
        // Net received: 126.32 - 6.32 = 120.00
        // Net profit after 100 direct cost: 20.00!
        var validation = _guardrailValidator.Validate(floor, profile);
        validation.IsValid.Should().BeTrue();
    }

    #endregion
}
