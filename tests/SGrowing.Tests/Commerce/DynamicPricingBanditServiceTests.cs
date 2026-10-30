using SGrowing.Api.Modules.Commerce;
using Xunit;

namespace SGrowing.Tests.Commerce;

public class DynamicPricingBanditServiceTests
{
    private readonly DynamicPricingBanditService _service = new();

    #region 1. Beta Distribution Sampler Tests

    [Fact]
    public void BetaDistributionSampler_ProducesValuesBetweenZeroAndOne()
    {
        for (int i = 0; i < 50; i++)
        {
            double sample = BetaDistributionSampler.Sample(2.0, 5.0);
            Assert.InRange(sample, 0.0, 1.0);
        }
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(-1.0, 5.0)]
    [InlineData(2.0, 0.0)]
    [InlineData(2.0, -3.0)]
    public void BetaDistributionSampler_ThrowsOnNonPositiveParameters(double alpha, double beta)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BetaDistributionSampler.Sample(alpha, beta));
    }

    [Fact]
    public void BetaDistributionSampler_MeanApproximatesTheoreticalExpectation()
    {
        double alpha = 20.0;
        double beta = 80.0;
        double expectedMean = alpha / (alpha + beta); // 0.20

        double sum = 0.0;
        int trials = 500;
        for (int i = 0; i < trials; i++)
        {
            sum += BetaDistributionSampler.Sample(alpha, beta);
        }

        double empiricalMean = sum / trials;
        // Allow a reasonable statistical tolerance for 500 trials
        Assert.InRange(empiricalMean, expectedMean - 0.05, expectedMean + 0.05);
    }

    #endregion

    #region 2. Multi-Armed Bandit & Thompson Sampling Tests

    [Fact]
    public async Task GetExperimentsAsync_ReturnsAllFourSeedSkus()
    {
        var experiments = await _service.GetExperimentsAsync();

        Assert.Equal(4, experiments.Count);
        Assert.Contains(experiments, e => e.Sku == "SKU-SAAS-ENTERPRISE-ADDON");
        Assert.Contains(experiments, e => e.Sku == "SKU-AUDIO-EARBUDS-PRO");
        Assert.Contains(experiments, e => e.Sku == "SKU-DESK-ERGO-MOTORIZED");
        Assert.Contains(experiments, e => e.Sku == "SKU-CLOUD-STORAGE-PREMIUM");
    }

    [Fact]
    public async Task GetExperimentBySkuAsync_ReturnsCorrectSkuDetails()
    {
        var exp = await _service.GetExperimentBySkuAsync("SKU-SAAS-ENTERPRISE-ADDON");

        Assert.NotNull(exp);
        Assert.Equal("Enterprise AI Copilot SaaS Addon", exp.ProductName);
        Assert.Equal(4, exp.Arms.Count);
        Assert.Equal(15.00m, exp.BaseCostCogs);
        Assert.Equal(0.60m, exp.MinimumMarginHurdle);
        Assert.Equal(24.00m, exp.HardMarginFloor); // 15 * 1.60 + 0 = 24.00
    }

    [Fact]
    public async Task SelectArmAsync_ReturnsValidArmSelectionResult()
    {
        var result = await _service.SelectArmAsync("SKU-SAAS-ENTERPRISE-ADDON");

        Assert.NotNull(result);
        Assert.Equal("SKU-SAAS-ENTERPRISE-ADDON", result.Sku);
        Assert.Contains(result.SelectedArmId, new[] { "ARM-29", "ARM-39", "ARM-49", "ARM-69" });
        Assert.True(result.ProposedPrice >= 29.00m);
        Assert.True(result.FinalPrice >= result.FloorPrice);
        Assert.Equal(4, result.AllSampledThetas.Count);
        Assert.InRange(result.SampledTheta, 0.0, 1.0);
        Assert.True(result.SampledExpectedRevenue > 0m);
    }

    [Fact]
    public async Task SelectArmAsync_ThrowsKeyNotFoundException_ForUnknownSku()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.SelectArmAsync("NON-EXISTENT-SKU"));
    }

    [Fact]
    public async Task ThompsonSampling_FavorsHighExpectedValueArm()
    {
        // Reset SKU prior with an extreme dominant arm
        await _service.ResetSkuPriorAsync("SKU-CLOUD-STORAGE-PREMIUM", alphaPrior: 1.0, betaPrior: 100.0);
        // Make ARM-12 heavily dominant: 100 conversions out of 101 trials
        for (int i = 0; i < 100; i++)
        {
            await _service.RecordConversionAsync("SKU-CLOUD-STORAGE-PREMIUM", "ARM-12");
        }

        int arm12Selections = 0;
        int trials = 50;
        for (int i = 0; i < trials; i++)
        {
            var sel = await _service.SelectArmAsync("SKU-CLOUD-STORAGE-PREMIUM");
            if (sel.SelectedArmId == "ARM-12") arm12Selections++;
        }

        // Arm 12 should be chosen in majority of trials
        Assert.True(arm12Selections >= 45, $"Expected ARM-12 to dominate, but got {arm12Selections}/{trials}");
    }

    #endregion

    #region 3. Impression and Conversion Recording Tests

    [Fact]
    public async Task RecordImpressionAsync_IncrementsImpressionsAndBeta()
    {
        var before = await _service.GetExperimentBySkuAsync("SKU-SAAS-ENTERPRISE-ADDON");
        var armBefore = before!.Arms.First(a => a.ArmId == "ARM-29");
        long initialImpressions = armBefore.Impressions;
        double initialBeta = armBefore.Beta;

        var after = await _service.RecordImpressionAsync("SKU-SAAS-ENTERPRISE-ADDON", "ARM-29");
        var armAfter = after.Arms.First(a => a.ArmId == "ARM-29");

        Assert.Equal(initialImpressions + 1, armAfter.Impressions);
        Assert.Equal(initialBeta + 1.0, armAfter.Beta);
    }

    [Fact]
    public async Task RecordConversionAsync_IncrementsConversionsAlphaAndAdjustsBeta()
    {
        var before = await _service.GetExperimentBySkuAsync("SKU-SAAS-ENTERPRISE-ADDON");
        var armBefore = before!.Arms.First(a => a.ArmId == "ARM-49");
        long initialConversions = armBefore.Conversions;
        double initialAlpha = armBefore.Alpha;
        double initialBeta = armBefore.Beta;
        decimal initialRev = armBefore.TotalRevenue;

        var after = await _service.RecordConversionAsync("SKU-SAAS-ENTERPRISE-ADDON", "ARM-49", revenue: 49.00m);
        var armAfter = after.Arms.First(a => a.ArmId == "ARM-49");

        Assert.Equal(initialConversions + 1, armAfter.Conversions);
        Assert.Equal(initialAlpha + 1.0, armAfter.Alpha);
        Assert.Equal(Math.Max(1.0, initialBeta - 1.0), armAfter.Beta);
        Assert.Equal(initialRev + 49.00m, armAfter.TotalRevenue);
    }

    [Fact]
    public async Task RecordImpression_ThrowsOnInvalidArm()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.RecordImpressionAsync("SKU-SAAS-ENTERPRISE-ADDON", "ARM-INVALID"));
    }

    [Fact]
    public async Task RecordConversion_ThrowsOnInvalidSku()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.RecordConversionAsync("INVALID-SKU", "ARM-29"));
    }

    #endregion

    #region 4. Price Elasticity of Demand Tests

    [Fact]
    public async Task CalculateElasticity_InelasticDemand_RecommendsIncreasePrice()
    {
        // Price increased 20% ($100 to $120), Quantity drops 5% (1000 to 950)
        // Arc elasticity: ((950 - 1000) / 975) / ((120 - 100) / 110) = (-0.05128) / (0.1818) = -0.282
        var report = await _service.CalculateElasticityAsync(
            sku: "SKU-SAAS-ENTERPRISE-ADDON",
            price0: 100m,
            price1: 120m,
            quantity0: 1000m,
            quantity1: 950m);

        Assert.Equal("Inelastic", report.ElasticityClassification);
        Assert.Equal("IncreasePrice", report.RecommendedStrategy);
        Assert.True(Math.Abs(report.ElasticityCoefficient) < 0.8);
        Assert.Contains("inelastic", report.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CalculateElasticity_UnitaryDemand_RecommendsHoldPrice()
    {
        // Price increased 10% ($100 to $110), Quantity drops 10% (1000 to 900)
        // Arc elasticity approx -1.0
        var report = await _service.CalculateElasticityAsync(
            sku: "SKU-AUDIO-EARBUDS-PRO",
            price0: 100m,
            price1: 110m,
            quantity0: 1000m,
            quantity1: 900m);

        Assert.Equal("Unitary", report.ElasticityClassification);
        Assert.Equal("HoldPrice", report.RecommendedStrategy);
        Assert.InRange(Math.Abs(report.ElasticityCoefficient), 0.8, 1.2);
    }

    [Fact]
    public async Task CalculateElasticity_ElasticDemand_RecommendsDiscountToDriveVolume()
    {
        // Price dropped 10% ($100 to $90), Quantity increased 35% (1000 to 1350)
        var report = await _service.CalculateElasticityAsync(
            sku: "SKU-DESK-ERGO-MOTORIZED",
            price0: 100m,
            price1: 90m,
            quantity0: 1000m,
            quantity1: 1350m);

        Assert.Equal("Elastic", report.ElasticityClassification);
        Assert.Equal("DiscountToDriveVolume", report.RecommendedStrategy);
        Assert.True(Math.Abs(report.ElasticityCoefficient) > 1.2);
        Assert.Contains("elastic", report.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CalculateElasticity_ZeroPriceChange_HandlesGracefullyWithoutDivisionByZero()
    {
        var report = await _service.CalculateElasticityAsync(
            sku: "SKU-SAAS-ENTERPRISE-ADDON",
            price0: 50m,
            price1: 50m,
            quantity0: 100m,
            quantity1: 100m);

        Assert.Equal(0.0, report.ElasticityCoefficient);
        Assert.Equal("Inelastic", report.ElasticityClassification);
        Assert.Equal("HoldPrice", report.RecommendedStrategy);
    }

    [Fact]
    public async Task CalculateElasticity_ZeroQuantityOrPriceMidpoints_ReturnsZeroSafely()
    {
        var report = await _service.CalculateElasticityAsync(
            sku: "SKU-SAAS-ENTERPRISE-ADDON",
            price0: 0m,
            price1: 0m,
            quantity0: 0m,
            quantity1: 0m);

        Assert.Equal(0.0, report.ElasticityCoefficient);
    }

    #endregion

    #region 5. Hard Margin Floor Guardrail Tests

    [Fact]
    public async Task HardMarginFloor_CalculatesAccuratelyForProduct()
    {
        // Ergonomic Desk: BaseCost = 195, Shipping = 35, Hurdle = 20%
        // Floor = 195 * 1.20 + 35 = 234 + 35 = $269.00
        var exp = await _service.GetExperimentBySkuAsync("SKU-DESK-ERGO-MOTORIZED");

        Assert.NotNull(exp);
        Assert.Equal(269.00m, exp.HardMarginFloor);
    }

    [Fact]
    public async Task EvaluatePriceGuardrail_CandidateAboveFloor_ReturnsCompliant()
    {
        var check = await _service.EvaluatePriceGuardrailAsync("SKU-DESK-ERGO-MOTORIZED", candidatePrice: 350.00m);

        Assert.False(check.FloorClampApplied);
        Assert.Equal(350.00m, check.EffectivePrice);
        Assert.Equal(269.00m, check.FloorPrice);
        Assert.Contains("Compliant", check.GuardrailStatus);
    }

    [Fact]
    public async Task EvaluatePriceGuardrail_CandidateBelowFloor_ClampsToFloor()
    {
        var check = await _service.EvaluatePriceGuardrailAsync("SKU-DESK-ERGO-MOTORIZED", candidatePrice: 200.00m);

        Assert.True(check.FloorClampApplied);
        Assert.Equal(269.00m, check.EffectivePrice);
        Assert.Equal(269.00m, check.FloorPrice);
        Assert.Contains("clamped", check.GuardrailStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluatePriceGuardrail_CandidateEqualToFloor_ReturnsCompliant()
    {
        var check = await _service.EvaluatePriceGuardrailAsync("SKU-DESK-ERGO-MOTORIZED", candidatePrice: 269.00m);

        Assert.False(check.FloorClampApplied);
        Assert.Equal(269.00m, check.EffectivePrice);
    }

    #endregion

    #region 6. Automated Inventory Liquidation Markdown Tests

    [Fact]
    public async Task GetLiquidationSchedule_HealthyInventory_DoesNotTriggerLiquidation()
    {
        // SaaS Addon: Stock = 1200, Velocity = 25/day => DOS = 48 days (<= 60)
        var sched = await _service.GetLiquidationScheduleForSkuAsync("SKU-SAAS-ENTERPRISE-ADDON");

        Assert.False(sched.LiquidationTriggered);
        Assert.Equal("Healthy", sched.LiquidationTier);
        Assert.Equal(0m, sched.RecommendedMarkdownPct);
        Assert.Equal(sched.StandardPrice, sched.FinalPrice);
        Assert.False(sched.MarginFloorClamped);
    }

    [Fact]
    public async Task GetLiquidationSchedule_SlowMoving60Days_CalculatesProportionalMarkdown()
    {
        // Wireless Earbuds: Stock = 850, Velocity = 8.5/day => DOS = 100 days
        // Overage = (100 - 60) / 60 = 40/60 = 0.6667
        // MarkdownStep = 0.12 => Markdown = 0.6667 * 0.12 = 0.08 (8%)
        // StandardPrice = 79.99 => Discounted = 79.99 * 0.92 = 73.59
        // Hard Floor = 44.50 => 73.59 > 44.50 (Compliant, not clamped)
        var sched = await _service.GetLiquidationScheduleForSkuAsync("SKU-AUDIO-EARBUDS-PRO");

        Assert.True(sched.LiquidationTriggered);
        Assert.Equal("SlowMoving_60Days", sched.LiquidationTier);
        Assert.Equal(100.0m, sched.DaysOfSupply);
        Assert.Equal(0.08m, sched.RecommendedMarkdownPct);
        Assert.Equal(73.59m, sched.FinalPrice);
        Assert.False(sched.MarginFloorClamped);
    }

    [Fact]
    public async Task GetLiquidationSchedule_CriticalOverage_ClampsToHardMarginFloor()
    {
        // Ergonomic Desk: Stock = 420, Velocity = 2.1/day => DOS = 200 days (> 180 Critical)
        // Overage = (200 - 60) / 60 = 140 / 60 = 2.333
        // MarkdownStep = 0.15 => Raw = 2.333 * 0.15 = 0.35 (35%), max is 0.45
        // StandardPrice = 449.00 => RawDiscounted = 449 * (1 - 0.35) = 291.85
        // HardMarginFloor = 269.00 => Raw (291.85) is above floor (269.00)
        // Now let's test extreme overage: update stock to 2000, DOS = 2000 / 2.1 = 952 days!
        // Raw discount reaches max 45% => 449 * 0.55 = 246.95, which is BELOW 269.00 floor!
        await _service.UpdateInventoryTelemetryAsync("SKU-DESK-ERGO-MOTORIZED", currentStock: 2000, dailyRunRate: 2.1m);

        var sched = await _service.GetLiquidationScheduleForSkuAsync("SKU-DESK-ERGO-MOTORIZED");

        Assert.True(sched.LiquidationTriggered);
        Assert.Equal("Critical_180DaysPlus", sched.LiquidationTier);
        Assert.True(sched.MarginFloorClamped);
        Assert.Equal(269.00m, sched.FinalPrice); // Clamped to hard margin floor!
        Assert.True(sched.RawDiscountedPrice < 269.00m);
    }

    [Fact]
    public async Task GetLiquidationSchedule_ZeroVelocity_TreatsAsInfiniteSupplyGracefully()
    {
        await _service.UpdateInventoryTelemetryAsync("SKU-AUDIO-EARBUDS-PRO", currentStock: 500, dailyRunRate: 0m);

        var sched = await _service.GetLiquidationScheduleForSkuAsync("SKU-AUDIO-EARBUDS-PRO");

        Assert.Equal(9999m, sched.DaysOfSupply);
        Assert.True(sched.LiquidationTriggered);
        Assert.Equal("Critical_180DaysPlus", sched.LiquidationTier);
    }

    [Fact]
    public async Task GetLiquidationSchedule_ZeroStock_ReturnsZeroDosAndNoLiquidation()
    {
        await _service.UpdateInventoryTelemetryAsync("SKU-AUDIO-EARBUDS-PRO", currentStock: 0, dailyRunRate: 10m);

        var sched = await _service.GetLiquidationScheduleForSkuAsync("SKU-AUDIO-EARBUDS-PRO");

        Assert.Equal(0m, sched.DaysOfSupply);
        Assert.False(sched.LiquidationTriggered);
        Assert.Equal("Healthy", sched.LiquidationTier);
    }

    #endregion

    #region 7. Concurrency & State Reset Tests

    [Fact]
    public async Task ResetSkuPrior_RestoresCleanBaselinePrior()
    {
        var updated = await _service.ResetSkuPriorAsync("SKU-SAAS-ENTERPRISE-ADDON", alphaPrior: 1.0, betaPrior: 1.0);

        foreach (var arm in updated.Arms)
        {
            Assert.Equal(1.0, arm.Alpha);
            Assert.Equal(1.0, arm.Beta);
            Assert.Equal(0, arm.Impressions);
            Assert.Equal(0, arm.Conversions);
            Assert.Equal(0m, arm.TotalRevenue);
        }
    }

    [Fact]
    public async Task ConcurrentImpressionsAndConversions_AreThreadSafeAndConsistent()
    {
        const string sku = "SKU-CLOUD-STORAGE-PREMIUM";
        await _service.ResetSkuPriorAsync(sku, 1.0, 1.0);

        const int iterations = 100;
        var tasks = new List<Task>();

        for (int i = 0; i < iterations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await _service.RecordImpressionAsync(sku, "ARM-7");
                await _service.RecordConversionAsync(sku, "ARM-7", 7.99m);
            }));
        }

        await Task.WhenAll(tasks);

        var exp = await _service.GetExperimentBySkuAsync(sku);
        var arm = exp!.Arms.First(a => a.ArmId == "ARM-7");

        Assert.Equal(iterations, arm.Impressions);
        Assert.Equal(iterations, arm.Conversions);
        Assert.Equal(iterations * 7.99m, arm.TotalRevenue);
        Assert.Equal(1.0 + iterations, arm.Alpha);
    }

    #endregion
}
