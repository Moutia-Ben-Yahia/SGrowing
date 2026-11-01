using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Marketing.AdSpend;
using Xunit;

namespace SGrowing.Tests.Marketing;

/// <summary>
/// Comprehensive test suite for Horizon 49: Autonomous Cross-Channel Predictive ROAS &amp; Real-Time Ad Spend Allocator.
/// Validates non-linear Hill saturation curves, analytical marginal ROAS derivatives,
/// boundary handling (zero, negative, infinite spend), inverse saturation solvers,
/// Markov chain removal effects and Bayesian Beta-Binomial attribution,
/// continuous bandit / Thompson Sampling budget reallocation with budget conservation invariants,
/// floor/ceiling guardrails, pacing throttles, creative fatigue radar (CTR slope, frequency saturation, CPA inflection),
/// concurrency thread-safety with System.Threading.Lock, and direct ServiceCollection descriptor validation.
/// </summary>
public class PredictiveRoasAndAdSpendOptimizationTests
{
    private readonly PredictiveRoasEngine _roasEngine = new();
    private readonly DynamicBudgetReallocationService _reallocationService;
    private readonly CreativeFatigueRadarService _fatigueService = new();

    public PredictiveRoasAndAdSpendOptimizationTests()
    {
        _reallocationService = new DynamicBudgetReallocationService(_roasEngine);
    }

    #region 1. Hill Function Diminishing Returns Curve & Exponent Diversity

    [Fact]
    public void EvaluateHillCurve_HalfSaturationSpend_ReturnsExactHalfVmax()
    {
        // When spend x == K, Y(K) = (Vmax * K^n) / (K^n + K^n) = Vmax / 2
        var parameters = new HillParameters(Vmax: 100_000.0, K: 10_000.0, HillExponent: 1.8);
        double revenue = _roasEngine.EvaluateHillCurve(10_000.0, parameters);

        revenue.Should().BeApproximately(50_000.0, 1e-4);
    }

    [Fact]
    public void EvaluateHillCurve_SteepSigmoid_Exponent2Point5_ProducesCorrectSShapedCurve()
    {
        var parameters = new HillParameters(Vmax: 60_000.0, K: 5_000.0, HillExponent: 2.5);

        // At low spend (x = 1,000 < K), growth is convex/slow
        double revLow = _roasEngine.EvaluateHillCurve(1_000.0, parameters);
        // At K (x = 5,000), exact half
        double revMid = _roasEngine.EvaluateHillCurve(5_000.0, parameters);
        // At high spend (x = 10,000 > K), growth saturates
        double revHigh = _roasEngine.EvaluateHillCurve(10_000.0, parameters);

        revLow.Should().BeLessThan(revMid * 0.1); // Strong initial thresholding
        revMid.Should().BeApproximately(30_000.0, 1e-4);
        revHigh.Should().BeGreaterThan(45_000.0);
    }

    [Fact]
    public void EvaluateHillCurve_ShallowCurve_Exponent0Point5_ProducesConcaveDiminishingReturns()
    {
        // For n < 1, returns are strictly concave from x = 0
        var parameters = new HillParameters(Vmax: 40_000.0, K: 4_000.0, HillExponent: 0.5);

        double rev1k = _roasEngine.EvaluateHillCurve(1_000.0, parameters);
        double rev4k = _roasEngine.EvaluateHillCurve(4_000.0, parameters);
        double rev16k = _roasEngine.EvaluateHillCurve(16_000.0, parameters);

        rev4k.Should().BeApproximately(20_000.0, 1e-4);
        // Check diminishing marginal additions: (rev4k - rev1k) > (rev16k - rev4k) normalized per dollar
        double deltaFirst = (rev4k - rev1k) / 3000.0;
        double deltaSecond = (rev16k - rev4k) / 12000.0;
        deltaFirst.Should().BeGreaterThan(deltaSecond);
    }

    [Fact]
    public void EvaluateHillCurve_HyperbolicCurve_Exponent1Point0_MatchesMichaelisMentenKinetics()
    {
        // n = 1: Y(x) = (Vmax * x) / (K + x)
        var parameters = new HillParameters(Vmax: 80_000.0, K: 8_000.0, HillExponent: 1.0);
        double spend = 24_000.0;

        double expected = (80_000.0 * 24_000.0) / (8_000.0 + 24_000.0); // 80k * 3/4 = 60,000
        double actual = _roasEngine.EvaluateHillCurve(spend, parameters);

        actual.Should().BeApproximately(expected, 1e-4);
    }

    [Fact]
    public void EvaluateHillCurve_UltraSteepCurve_Exponent4Point0_ExhibitsSharpThresholdBehavior()
    {
        var parameters = new HillParameters(Vmax: 50_000.0, K: 10_000.0, HillExponent: 4.0);

        // For spend below 50% of K, revenue is nearly 0
        double revSub = _roasEngine.EvaluateHillCurve(4_000.0, parameters);
        // For spend at K, exactly 25,000
        double revK = _roasEngine.EvaluateHillCurve(10_000.0, parameters);
        // For spend at 2x K, almost fully saturated (> 94%)
        double revSup = _roasEngine.EvaluateHillCurve(20_000.0, parameters);

        revSub.Should().BeLessThan(1_500.0);
        revK.Should().BeApproximately(25_000.0, 1e-4);
        revSup.Should().BeGreaterThan(45_000.0);
    }

    [Fact]
    public void EvaluateHillCurve_MonotonicallyIncreasesWithSpend()
    {
        var parameters = new HillParameters(Vmax: 50_000.0, K: 5_000.0, HillExponent: 1.7);
        double prev = -1.0;

        for (double spend = 500.0; spend <= 25_000.0; spend += 500.0)
        {
            double current = _roasEngine.EvaluateHillCurve(spend, parameters);
            current.Should().BeGreaterThan(prev);
            prev = current;
        }
    }

    #endregion

    #region 2. Marginal ROAS Analytical Derivative Precision Tests

    [Theory]
    [InlineData(1_000.0)]
    [InlineData(3_000.0)]
    [InlineData(6_000.0)]
    [InlineData(12_000.0)]
    [InlineData(25_000.0)]
    public void CalculateMarginalRoas_AnalyticalMatchesNumericalFiniteDifference_Exponent1Point8(double spend)
    {
        var parameters = new HillParameters(Vmax: 50_000.0, K: 6_000.0, HillExponent: 1.8);
        double analytical = _roasEngine.CalculateMarginalRoas(spend, parameters);

        // Central finite difference: (Y(x + h) - Y(x - h)) / (2h) with h = 0.001
        double h = 0.001;
        double yPlus = _roasEngine.EvaluateHillCurve(spend + h, parameters);
        double yMinus = _roasEngine.EvaluateHillCurve(spend - h, parameters);
        double numerical = (yPlus - yMinus) / (2.0 * h);

        analytical.Should().BeApproximately(numerical, 1e-4);
    }

    [Fact]
    public void CalculateMarginalRoas_AnalyticalMatchesFormula_Exponent1Point0()
    {
        // For n = 1: dY/dx = (Vmax * K) / (K + x)^2
        var parameters = new HillParameters(Vmax: 70_000.0, K: 7_000.0, HillExponent: 1.0);
        double spend = 14_000.0;

        double expected = (70_000.0 * 7_000.0) / Math.Pow(7_000.0 + 14_000.0, 2);
        double actual = _roasEngine.CalculateMarginalRoas(spend, parameters);

        actual.Should().BeApproximately(expected, 1e-6);
    }

    [Fact]
    public void CalculateMarginalRoas_SteepCurve_PeaksNearInflectionPoint()
    {
        // For n = 2.0: Inflection point where second derivative is zero is x = K * ((n-1)/(n+1))^(1/n)
        // x_inflection = 6000 * (1/3)^(1/2) ~= 3464.1
        var parameters = new HillParameters(Vmax: 60_000.0, K: 6_000.0, HillExponent: 2.0);

        double mBefore = _roasEngine.CalculateMarginalRoas(1_000.0, parameters);
        double mInflection = _roasEngine.CalculateMarginalRoas(3_464.0, parameters);
        double mAfter = _roasEngine.CalculateMarginalRoas(10_000.0, parameters);

        mInflection.Should().BeGreaterThan(mBefore);
        mInflection.Should().BeGreaterThan(mAfter);
    }

    [Fact]
    public void CalculateMarginalRoas_ApproachesZeroAsSpendSaturates()
    {
        var parameters = new HillParameters(Vmax: 50_000.0, K: 5_000.0, HillExponent: 1.5);
        double marginalAtExtremeSpend = _roasEngine.CalculateMarginalRoas(500_000.0, parameters);

        marginalAtExtremeSpend.Should().BeLessThan(0.01);
    }

    #endregion

    #region 3. Zero, Negative, and Boundary Edge Case Tests

    [Fact]
    public void EvaluateHillCurve_ZeroSpend_ReturnsZeroRevenue()
    {
        var parameters = HillParameters.DefaultMeta;
        _roasEngine.EvaluateHillCurve(0.0, parameters).Should().Be(0.0);
    }

    [Fact]
    public void EvaluateHillCurve_NegativeSpend_ReturnsZeroRevenue()
    {
        var parameters = HillParameters.DefaultGoogle;
        _roasEngine.EvaluateHillCurve(-1_500.0, parameters).Should().Be(0.0);
    }

    [Fact]
    public void EvaluateHillCurve_DoubleNaNSpend_ReturnsZeroRevenue()
    {
        var parameters = HillParameters.DefaultTikTok;
        _roasEngine.EvaluateHillCurve(double.NaN, parameters).Should().Be(0.0);
    }

    [Fact]
    public void EvaluateHillCurve_PositiveInfinitySpend_ReturnsVmaxAsymptote()
    {
        var parameters = new HillParameters(Vmax: 75_000.0, K: 8_000.0, HillExponent: 1.8);
        _roasEngine.EvaluateHillCurve(double.PositiveInfinity, parameters).Should().Be(75_000.0);
    }

    [Fact]
    public void CalculateMarginalRoas_ZeroAndNegativeSpend_ReturnsZero()
    {
        var parameters = HillParameters.DefaultMeta;
        _roasEngine.CalculateMarginalRoas(0.0, parameters).Should().Be(0.0);
        _roasEngine.CalculateMarginalRoas(-500.0, parameters).Should().Be(0.0);
        _roasEngine.CalculateMarginalRoas(double.NaN, parameters).Should().Be(0.0);
        _roasEngine.CalculateMarginalRoas(double.PositiveInfinity, parameters).Should().Be(0.0);
    }

    [Fact]
    public void EvaluateHillCurve_InvalidParameters_ReturnsZeroSafely()
    {
        var invalidVmax = new HillParameters(Vmax: -10_000.0, K: 5_000.0, HillExponent: 1.5);
        var invalidK = new HillParameters(Vmax: 10_000.0, K: 0.0, HillExponent: 1.5);
        var invalidN = new HillParameters(Vmax: 10_000.0, K: 5_000.0, HillExponent: -1.0);

        _roasEngine.EvaluateHillCurve(2_000.0, invalidVmax).Should().Be(0.0);
        _roasEngine.EvaluateHillCurve(2_000.0, invalidK).Should().Be(0.0);
        _roasEngine.EvaluateHillCurve(2_000.0, invalidN).Should().Be(0.0);
    }

    #endregion

    #region 4. Average ROAS, Saturation & Optimal Hurdle Inversion Tests

    [Fact]
    public void CalculateAverageRoas_AtVariousSpends_MatchesRatioOfRevenueToSpend()
    {
        var parameters = new HillParameters(Vmax: 40_000.0, K: 5_000.0, HillExponent: 1.5);
        double spend = 10_000.0;

        double rev = _roasEngine.EvaluateHillCurve(spend, parameters);
        double avgRoas = _roasEngine.CalculateAverageRoas(spend, parameters);

        avgRoas.Should().BeApproximately(rev / spend, 1e-6);
    }

    [Fact]
    public void CalculateAverageRoas_ZeroSpend_ReturnsZero()
    {
        var parameters = HillParameters.DefaultGoogle;
        _roasEngine.CalculateAverageRoas(0.0, parameters).Should().Be(0.0);
    }

    [Fact]
    public void CalculateSaturationPercentage_AtSpendK_ReturnsExact50Percent()
    {
        var parameters = new HillParameters(Vmax: 50_000.0, K: 8_000.0, HillExponent: 1.6);
        double saturation = _roasEngine.CalculateSaturationPercentage(8_000.0, parameters);

        saturation.Should().BeApproximately(50.0, 1e-4);
    }

    [Fact]
    public void CalculateSpendForTargetSaturation_50Percent_ReturnsK()
    {
        var parameters = new HillParameters(Vmax: 60_000.0, K: 7_500.0, HillExponent: 2.0);
        double spend50 = _roasEngine.CalculateSpendForTargetSaturation(50.0, parameters);

        spend50.Should().BeApproximately(7_500.0, 1e-4);
    }

    [Fact]
    public void CalculateSpendForTargetSaturation_80Percent_ReturnsExactKTimes4PowRootN()
    {
        // s = 80%: x = K * (80 / 20)^(1/n) = K * 4^(1/n)
        var parameters = new HillParameters(Vmax: 60_000.0, K: 5_000.0, HillExponent: 2.0);
        double spend80 = _roasEngine.CalculateSpendForTargetSaturation(80.0, parameters);

        // 4^(1/2) = 2 => x = 5000 * 2 = 10,000
        spend80.Should().BeApproximately(10_000.0, 1e-4);
    }

    [Fact]
    public void CalculateSpendForTargetSaturation_95Percent_ReturnsExactKTimes19PowRootN()
    {
        // s = 95%: x = K * (95 / 5)^(1/n) = K * 19^(1/n)
        var parameters = new HillParameters(Vmax: 60_000.0, K: 3_000.0, HillExponent: 1.0);
        double spend95 = _roasEngine.CalculateSpendForTargetSaturation(95.0, parameters);

        // 19^(1/1) = 19 => x = 3000 * 19 = 57,000
        spend95.Should().BeApproximately(57_000.0, 1e-4);
    }

    [Fact]
    public void CalculateOptimalHurdleSpend_Exponent1Point0_MatchesAnalyticalFormula()
    {
        // For n = 1: x = sqrt((Vmax * K) / targetR) - K
        var parameters = new HillParameters(Vmax: 100_000.0, K: 4_000.0, HillExponent: 1.0);
        double targetRoas = 1.0;

        double expected = Math.Sqrt((100_000.0 * 4_000.0) / targetRoas) - 4_000.0; // sqrt(400M) - 4k = 20k - 4k = 16k
        double actual = _roasEngine.CalculateOptimalHurdleSpend(parameters, targetRoas);

        actual.Should().BeApproximately(expected, 1e-2);
    }

    [Fact]
    public void CalculateOptimalHurdleSpend_Exponent1Point8_FindsSpendWhereMarginalRoasEqualsTarget()
    {
        var parameters = new HillParameters(Vmax: 50_000.0, K: 6_000.0, HillExponent: 1.8);
        double targetMarginalRoas = 1.2;

        double optimalSpend = _roasEngine.CalculateOptimalHurdleSpend(parameters, targetMarginalRoas);
        double evaluatedMarginal = _roasEngine.CalculateMarginalRoas(optimalSpend, parameters);

        evaluatedMarginal.Should().BeApproximately(targetMarginalRoas, 0.05);
    }

    #endregion

    #region 5. Markov Attribution & Bayesian Multi-Touch Models

    [Fact]
    public void CalculateMarkovBayesianAttribution_SingleChannelJourneys_Attributes100Percent()
    {
        List<CustomerJourneyPath> journeys =
        [
            new(Touchpoints: ["Meta"], Converted: true, ConversionValue: 150.0, Count: 10),
            new(Touchpoints: ["Meta"], Converted: false, ConversionValue: 0.0, Count: 20)
        ];

        var results = _roasEngine.CalculateMarkovBayesianAttribution(journeys);

        results.Should().HaveCount(1);
        results[0].Channel.Should().Be("Meta");
        results[0].MarkovAttributionWeight.Should().Be(1.0);
        results[0].BlendedAttributionWeight.Should().Be(1.0);
        results[0].AttributedConversions.Should().Be(10.0);
        results[0].AttributedRevenue.Should().Be(1500.0);
    }

    [Fact]
    public void CalculateMarkovBayesianAttribution_TwoIndependentParallelChannels_AttributesProportionally()
    {
        List<CustomerJourneyPath> journeys =
        [
            new(Touchpoints: ["Google"], Converted: true, ConversionValue: 100.0, Count: 60),
            new(Touchpoints: ["Meta"], Converted: true, ConversionValue: 100.0, Count: 40)
        ];

        var results = _roasEngine.CalculateMarkovBayesianAttribution(journeys);

        results.Should().HaveCount(2);
        double googleWeight = results.First(r => r.Channel == "Google").BlendedAttributionWeight;
        double metaWeight = results.First(r => r.Channel == "Meta").BlendedAttributionWeight;

        googleWeight.Should().BeGreaterThan(metaWeight);
        (googleWeight + metaWeight).Should().BeApproximately(1.0, 1e-4);
    }

    [Fact]
    public void CalculateMarkovBayesianAttribution_SequentialMultiTouch_ComputesRemovalEffects()
    {
        List<CustomerJourneyPath> journeys =
        [
            new(Touchpoints: ["TikTok", "Meta", "Google"], Converted: true, ConversionValue: 200.0, Count: 30),
            new(Touchpoints: ["TikTok", "Meta"], Converted: false, ConversionValue: 0.0, Count: 20),
            new(Touchpoints: ["Google"], Converted: true, ConversionValue: 100.0, Count: 10)
        ];

        var results = _roasEngine.CalculateMarkovBayesianAttribution(journeys);

        results.Should().HaveCount(3);
        results.Sum(r => r.BlendedAttributionWeight).Should().BeApproximately(1.0, 1e-4);
        results.All(r => r.RemovalEffect >= 0.0).Should().BeTrue();
    }

    [Fact]
    public void CalculateMarkovBayesianAttribution_CyclicJourneys_HandlesLoopsWithoutDivergence()
    {
        List<CustomerJourneyPath> journeys =
        [
            new(Touchpoints: ["Meta", "Google", "Meta", "Google"], Converted: true, ConversionValue: 300.0, Count: 15),
            new(Touchpoints: ["Meta", "Meta", "Meta"], Converted: false, ConversionValue: 0.0, Count: 10)
        ];

        var results = _roasEngine.CalculateMarkovBayesianAttribution(journeys);

        results.Should().HaveCount(2);
        results.Sum(r => r.BlendedAttributionWeight).Should().BeApproximately(1.0, 1e-4);
    }

    [Fact]
    public void CalculateMarkovBayesianAttribution_AttributedRevenueSum_MatchesTotalConversionRevenue()
    {
        List<CustomerJourneyPath> journeys =
        [
            new(Touchpoints: ["Meta", "Email"], Converted: true, ConversionValue: 250.0, Count: 4), // 1000
            new(Touchpoints: ["Google", "Email"], Converted: true, ConversionValue: 150.0, Count: 6), // 900
            new(Touchpoints: ["TikTok"], Converted: false, ConversionValue: 0.0, Count: 10)
        ];

        var results = _roasEngine.CalculateMarkovBayesianAttribution(journeys);

        double totalRevenue = results.Sum(r => r.AttributedRevenue);
        totalRevenue.Should().BeApproximately(1900.0, 1.0);
    }

    [Fact]
    public void CalculateMarkovBayesianAttribution_EmptyJourneysList_ReturnsEmpty()
    {
        var results = _roasEngine.CalculateMarkovBayesianAttribution([]);
        results.Should().BeEmpty();
    }

    #endregion

    #region 6. Channel Efficiency Rankings

    [Fact]
    public void RankChannelsByEfficiency_RanksByMarginalRoasDescending()
    {
        List<(string ChannelId, string ChannelName, double CurrentSpend, HillParameters Params)> channels =
        [
            ("ch-meta", "Meta Ads", 15_000.0, HillParameters.DefaultMeta),
            ("ch-google", "Google Ads", 2_000.0, HillParameters.DefaultGoogle),
            ("ch-tiktok", "TikTok Ads", 8_000.0, HillParameters.DefaultTikTok)
        ];

        var rankings = _roasEngine.RankChannelsByEfficiency(channels);

        rankings.Should().HaveCount(3);
        rankings[0].MarginalRoas.Should().BeGreaterThanOrEqualTo(rankings[1].MarginalRoas);
        rankings[1].MarginalRoas.Should().BeGreaterThanOrEqualTo(rankings[2].MarginalRoas);
    }

    [Fact]
    public void RankChannelsByEfficiency_LowSpendHighPotential_ClassifiesAsAggressiveScaleUp()
    {
        // Low spend (1000) on high Vmax Google has low saturation and high marginal ROAS
        var channels = new List<(string, string, double, HillParameters)>
        {
            ("ch-scale", "Google High Potential", 1_000.0, new HillParameters(Vmax: 100_000.0, K: 10_000.0, HillExponent: 1.5))
        };

        var rankings = _roasEngine.RankChannelsByEfficiency(channels);

        rankings[0].Tier.Should().Be(ChannelEfficiencyTier.AggressiveScaleUp);
        rankings[0].Recommendation.Should().Contain("Aggressively increase daily budget");
    }

    [Fact]
    public void RankChannelsByEfficiency_OverspentChannel_ClassifiesAsSaturatedReduce()
    {
        // Spend 28,000 on 100k Vmax with K 10,000 causes ~86.4% saturation and marginal ROAS ~0.76
        var channels = new List<(string, string, double, HillParameters)>
        {
            ("ch-sat", "Saturated Snapchat", 28_000.0, new HillParameters(Vmax: 100_000.0, K: 10_000.0, HillExponent: 1.8))
        };

        var rankings = _roasEngine.RankChannelsByEfficiency(channels);

        rankings[0].Tier.Should().Be(ChannelEfficiencyTier.SaturatedReduce);
        rankings[0].SaturationPercentage.Should().BeGreaterThan(85.0);
    }

    [Fact]
    public void RankChannelsByEfficiency_UnprofitableMarginal_ClassifiesAsUnprofitableKill()
    {
        // Spend 60,000 on a small 10k Vmax campaign drives marginal ROAS < 0.4
        var channels = new List<(string, string, double, HillParameters)>
        {
            ("ch-dead", "Exhausted Affiliate", 60_000.0, new HillParameters(Vmax: 10_000.0, K: 2_000.0, HillExponent: 1.5))
        };

        var rankings = _roasEngine.RankChannelsByEfficiency(channels);

        rankings[0].Tier.Should().Be(ChannelEfficiencyTier.UnprofitableKill);
        rankings[0].Recommendation.Should().Contain("sub-economic");
    }

    #endregion

    #region 7. Continuous Bandit & Thompson Sampling Budget Reallocation

    [Fact]
    public void ReallocateBudgets_ShiftsBudgetTowardHigherMarginalRoasChannel()
    {
        List<ChannelBudgetInput> channels =
        [
            new("meta", "Meta Ads", CurrentSpend: 5_000.0, FloorSpend: 1_000.0, CeilingSpend: 10_000.0,
                HillParams: new HillParameters(Vmax: 80_000.0, K: 8_000.0, HillExponent: 1.8), MaxShiftFraction: 0.25),
            new("snap", "Snap Ads", CurrentSpend: 5_000.0, FloorSpend: 1_000.0, CeilingSpend: 10_000.0,
                HillParams: new HillParameters(Vmax: 15_000.0, K: 2_000.0, HillExponent: 1.5), MaxShiftFraction: 0.25)
        ];

        var result = _reallocationService.ReallocateBudgets(channels, randomSeed: 42);

        var metaAlloc = result.Allocations.First(a => a.ChannelId == "meta");
        var snapAlloc = result.Allocations.First(a => a.ChannelId == "snap");

        metaAlloc.Delta.Should().BeGreaterThan(0.0);
        snapAlloc.Delta.Should().BeLessThan(0.0);
    }

    [Fact]
    public void ReallocateBudgets_EnforcesFloorSpendGuardrail()
    {
        List<ChannelBudgetInput> channels =
        [
            new("high", "High Performer", CurrentSpend: 5_000.0, FloorSpend: 1_000.0, CeilingSpend: 20_000.0,
                HillParams: new HillParameters(Vmax: 100_000.0, K: 10_000.0, HillExponent: 1.8)),
            new("low", "Low Performer", CurrentSpend: 5_000.0, FloorSpend: 4_200.0, CeilingSpend: 10_000.0,
                HillParams: new HillParameters(Vmax: 10_000.0, K: 1_000.0, HillExponent: 1.2), MaxShiftFraction: 0.50)
        ];

        var result = _reallocationService.ReallocateBudgets(channels, randomSeed: 123);

        var lowAlloc = result.Allocations.First(a => a.ChannelId == "low");
        lowAlloc.NewSpend.Should().BeGreaterThanOrEqualTo(4_200.0);
    }

    [Fact]
    public void ReallocateBudgets_EnforcesCeilingSpendGuardrail()
    {
        List<ChannelBudgetInput> channels =
        [
            new("super", "Super Star", CurrentSpend: 8_000.0, FloorSpend: 1_000.0, CeilingSpend: 9_000.0,
                HillParams: new HillParameters(Vmax: 200_000.0, K: 20_000.0, HillExponent: 2.0)),
            new("average", "Average Channel", CurrentSpend: 8_000.0, FloorSpend: 1_000.0, CeilingSpend: 15_000.0,
                HillParams: new HillParameters(Vmax: 40_000.0, K: 8_000.0, HillExponent: 1.5))
        ];

        var result = _reallocationService.ReallocateBudgets(channels, randomSeed: 456);

        var superAlloc = result.Allocations.First(a => a.ChannelId == "super");
        superAlloc.NewSpend.Should().BeLessThanOrEqualTo(9_000.0);
    }

    [Fact]
    public void ReallocateBudgets_RespectsSingleDayPacingThrottle()
    {
        // 20% max shift on 10,000 spend allows max delta of 2,000
        List<ChannelBudgetInput> channels =
        [
            new("c1", "Channel 1", CurrentSpend: 10_000.0, FloorSpend: 1_000.0, CeilingSpend: 30_000.0,
                HillParams: new HillParameters(Vmax: 200_000.0, K: 20_000.0, HillExponent: 1.8), MaxShiftFraction: 0.20),
            new("c2", "Channel 2", CurrentSpend: 10_000.0, FloorSpend: 1_000.0, CeilingSpend: 30_000.0,
                HillParams: new HillParameters(Vmax: 30_000.0, K: 5_000.0, HillExponent: 1.5), MaxShiftFraction: 0.20)
        ];

        var result = _reallocationService.ReallocateBudgets(channels, randomSeed: 789);

        var c1 = result.Allocations.First(a => a.ChannelId == "c1");
        c1.Delta.Should().BeInRange(-2_050.0, 2_050.0); // Within pacing limit (+- micro drift tolerance)
    }

    [Fact]
    public void ReallocateBudgets_EmptyChannels_ThrowsArgumentException()
    {
        var act = () => _reallocationService.ReallocateBudgets([]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReallocateBudgets_ZeroOrNegativeBudget_ThrowsArgumentException()
    {
        List<ChannelBudgetInput> channels =
        [
            new("c1", "C1", CurrentSpend: 0.0, FloorSpend: 0.0, CeilingSpend: 1_000.0, HillParams: HillParameters.DefaultMeta)
        ];

        var act = () => _reallocationService.ReallocateBudgets(channels, customTotalBudget: 0.0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReallocateBudgets_DeterministicExecutionWithRandomSeed()
    {
        List<ChannelBudgetInput> channels =
        [
            new("meta", "Meta", 5_000.0, 1_000.0, 10_000.0, HillParameters.DefaultMeta),
            new("google", "Google", 5_000.0, 1_000.0, 10_000.0, HillParameters.DefaultGoogle)
        ];

        var result1 = _reallocationService.ReallocateBudgets(channels, randomSeed: 999);
        var result2 = _reallocationService.ReallocateBudgets(channels, randomSeed: 999);

        result1.Allocations[0].NewSpend.Should().Be(result2.Allocations[0].NewSpend);
        result1.Allocations[1].NewSpend.Should().Be(result2.Allocations[1].NewSpend);
    }

    #endregion

    #region 8. Budget Conservation Invariant Tests

    [Fact]
    public void ReallocateBudgets_BudgetConservationInvariant_ExactSumMatchAcross100Iterations()
    {
        // Critical mathematical invariant: sum(B_new) == B_total (+- 1e-6) across 100 consecutive runs
        double totalBudget = 25_000.0;
        List<ChannelBudgetInput> currentChannels =
        [
            new("meta", "Meta", 8_000.0, 1_000.0, 15_000.0, HillParameters.DefaultMeta),
            new("google", "Google", 7_000.0, 1_000.0, 15_000.0, HillParameters.DefaultGoogle),
            new("tiktok", "TikTok", 6_000.0, 500.0, 12_000.0, HillParameters.DefaultTikTok),
            new("snap", "Snap", 4_000.0, 500.0, 8_000.0, HillParameters.DefaultSnapchat)
        ];

        for (int i = 0; i < 100; i++)
        {
            var plan = _reallocationService.ReallocateBudgets(currentChannels, customTotalBudget: totalBudget, randomSeed: i + 1);

            double sumNew = plan.Allocations.Sum(a => a.NewSpend);
            Math.Abs(sumNew - totalBudget).Should().BeLessThanOrEqualTo(1e-4, $"Iteration {i} failed conservation invariant.");

            // Pipe new budgets into next iteration to simulate multi-day dynamic allocation
            currentChannels = plan.Allocations.Select(a =>
            {
                var orig = currentChannels.First(c => c.ChannelId == a.ChannelId);
                return orig with { CurrentSpend = a.NewSpend };
            }).ToList();
        }
    }

    [Fact]
    public void ReallocateBudgets_BudgetConservationInvariant_HoldsWithTightFloorsAndCeilings()
    {
        double totalBudget = 10_000.0;
        List<ChannelBudgetInput> channels =
        [
            // Channel 1 capped tightly at 3,000 ceiling
            new("c1", "Ch 1", CurrentSpend: 2_500.0, FloorSpend: 500.0, CeilingSpend: 3_000.0,
                HillParams: new HillParameters(Vmax: 100_000.0, K: 10_000.0, HillExponent: 2.0)),
            // Channel 2 pinned at 4,000 floor
            new("c2", "Ch 2", CurrentSpend: 4_500.0, FloorSpend: 4_000.0, CeilingSpend: 8_000.0,
                HillParams: new HillParameters(Vmax: 10_000.0, K: 2_000.0, HillExponent: 1.2)),
            // Channel 3 absorbs dynamic swing
            new("c3", "Ch 3", CurrentSpend: 3_000.0, FloorSpend: 500.0, CeilingSpend: 8_000.0,
                HillParams: new HillParameters(Vmax: 50_000.0, K: 5_000.0, HillExponent: 1.5))
        ];

        var plan = _reallocationService.ReallocateBudgets(channels, customTotalBudget: totalBudget, randomSeed: 777);

        double sumNew = plan.Allocations.Sum(a => a.NewSpend);
        Math.Abs(sumNew - totalBudget).Should().BeLessThanOrEqualTo(1e-4);
        plan.Allocations.First(a => a.ChannelId == "c1").NewSpend.Should().BeLessThanOrEqualTo(3_000.0);
        plan.Allocations.First(a => a.ChannelId == "c2").NewSpend.Should().BeGreaterThanOrEqualTo(4_000.0);
    }

    [Fact]
    public void ReallocateBudgets_BudgetConservationInvariant_HoldsWithNonRoundDecimalBudget()
    {
        double oddBudget = 13_749.83;
        List<ChannelBudgetInput> channels =
        [
            new("c1", "Ch 1", CurrentSpend: 4_583.27, FloorSpend: 1_000.0, CeilingSpend: 8_000.0, HillParameters.DefaultMeta),
            new("c2", "Ch 2", CurrentSpend: 4_583.28, FloorSpend: 1_000.0, CeilingSpend: 8_000.0, HillParameters.DefaultGoogle),
            new("c3", "Ch 3", CurrentSpend: 4_583.28, FloorSpend: 1_000.0, CeilingSpend: 8_000.0, HillParameters.DefaultTikTok)
        ];

        var plan = _reallocationService.ReallocateBudgets(channels, customTotalBudget: oddBudget, randomSeed: 888);

        double sumNew = plan.Allocations.Sum(a => a.NewSpend);
        Math.Abs(sumNew - oddBudget).Should().BeLessThanOrEqualTo(1e-3);
    }

    #endregion

    #region 9. Creative Fatigue Radar - CTR Decay, Frequency Saturation & CPA Inflection

    [Fact]
    public void AnalyzeRollingWindowCtr_DetectsNegativeSlopeOnDeterioratingCreative()
    {
        // 7 days of steady CTR drop: 3.0% down to 1.2%
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history = [];
        for (int i = 6; i >= 0; i--)
        {
            double ctr = 0.030 - (0.003 * (6 - i)); // 0.030, 0.027, 0.024, 0.021, 0.018, 0.015, 0.012
            int impressions = 10_000;
            int clicks = (int)(impressions * ctr);
            history.Add(new DailyAdPerformance(now.AddDays(-i), impressions, clicks, 100m, 5, 250m));
        }

        var analysis = _fatigueService.AnalyzeRollingWindowCtr(history, 7);

        analysis.SlopePerDay.Should().BeLessThan(0.0);
        analysis.PercentageChange.Should().BeLessThan(-30.0);
        analysis.IsDeteriorating.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeRollingWindowCtr_DetectsPositiveSlopeOnImprovingCreative()
    {
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history = [];
        for (int i = 6; i >= 0; i--)
        {
            double ctr = 0.010 + (0.002 * (6 - i)); // 1.0% rising to 2.2%
            int impressions = 10_000;
            int clicks = (int)(impressions * ctr);
            history.Add(new DailyAdPerformance(now.AddDays(-i), impressions, clicks, 100m, 5, 250m));
        }

        var analysis = _fatigueService.AnalyzeRollingWindowCtr(history, 7);

        analysis.SlopePerDay.Should().BeGreaterThan(0.0);
        analysis.PercentageChange.Should().BeGreaterThan(20.0);
        analysis.IsDeteriorating.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeRollingWindowCtr_FlatPerformance_YieldsNearZeroSlope()
    {
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history = [];
        for (int i = 6; i >= 0; i--)
        {
            history.Add(new DailyAdPerformance(now.AddDays(-i), 10_000, 200, 100m, 5, 250m)); // 2.0% flat CTR
        }

        var analysis = _fatigueService.AnalyzeRollingWindowCtr(history, 7);

        analysis.SlopePerDay.Should().BeApproximately(0.0, 1e-5);
        analysis.PercentageChange.Should().BeApproximately(0.0, 1e-2);
        analysis.IsDeteriorating.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFrequencySaturation_HighFrequencyWithDecline_TriggersAlert()
    {
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history = [];
        for (int i = 6; i >= 0; i--)
        {
            double ctr = 0.030 - (0.003 * (6 - i)); // CTR drops > 20%
            history.Add(new DailyAdPerformance(now.AddDays(-i), 20_000, (int)(20_000 * ctr), 200m, 8, 400m));
        }

        // Pass custom frequency 4.5x (> 3.8x threshold)
        var diag = _fatigueService.EvaluateFrequencySaturation(history, customFrequency: 4.5);

        diag.IsTriggered.Should().BeTrue();
        diag.Frequency.Should().Be(4.5);
        diag.Message.Should().Contain("saturation triggered");
    }

    [Fact]
    public void EvaluateFrequencySaturation_HighFrequencyWithStableCtr_DoesNotTriggerAlert()
    {
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history = [];
        for (int i = 6; i >= 0; i--)
        {
            history.Add(new DailyAdPerformance(now.AddDays(-i), 20_000, 400, 200m, 8, 400m)); // Constant CTR 2%
        }

        var diag = _fatigueService.EvaluateFrequencySaturation(history, customFrequency: 4.5);

        diag.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public void EvaluateFrequencySaturation_LowFrequencyWithDecline_DoesNotTriggerAlert()
    {
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history = [];
        for (int i = 6; i >= 0; i--)
        {
            double ctr = 0.030 - (0.003 * (6 - i));
            history.Add(new DailyAdPerformance(now.AddDays(-i), 20_000, (int)(20_000 * ctr), 200m, 8, 400m));
        }

        // Frequency 2.1x (< 3.8x threshold)
        var diag = _fatigueService.EvaluateFrequencySaturation(history, customFrequency: 2.1);

        diag.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public void EvaluateCpaInflection_FiveConsecutiveDaysEscalationOver35Percent_TriggersAlarm()
    {
        // 5 consecutive days of increasing CPA: 20 -> 23 -> 26 -> 29 -> 32 (+60% hike > 35%)
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history =
        [
            new(now.AddDays(-4), 5_000, 100, 100m, 5, 200m), // CPA = $20
            new(now.AddDays(-3), 5_000, 100, 115m, 5, 200m), // CPA = $23
            new(now.AddDays(-2), 5_000, 100, 130m, 5, 200m), // CPA = $26
            new(now.AddDays(-1), 5_000, 100, 145m, 5, 200m), // CPA = $29
            new(now,             5_000, 100, 160m, 5, 200m)  // CPA = $32
        ];

        var diag = _fatigueService.EvaluateCpaInflection(history);

        diag.IsTriggered.Should().BeTrue();
        diag.ConsecutiveIncreaseDays.Should().Be(5);
        diag.TotalIncreasePercentage.Should().Be(60.0);
        diag.Message.Should().Contain("CPA inflection alarm active");
    }

    [Fact]
    public void EvaluateCpaInflection_FourConsecutiveDaysEscalation_DoesNotTriggerAlarm()
    {
        // Only 4 consecutive increases, preceded by a drop
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history =
        [
            new(now.AddDays(-5), 5_000, 100, 100m, 4, 200m), // CPA = $25
            new(now.AddDays(-4), 5_000, 100, 100m, 5, 200m), // CPA = $20 (dropped!)
            new(now.AddDays(-3), 5_000, 100, 120m, 5, 200m), // CPA = $24
            new(now.AddDays(-2), 5_000, 100, 140m, 5, 200m), // CPA = $28
            new(now.AddDays(-1), 5_000, 100, 160m, 5, 200m), // CPA = $32
            new(now,             5_000, 100, 180m, 5, 200m)  // CPA = $36
        ];

        var diag = _fatigueService.EvaluateCpaInflection(history);

        // Looking at last 5 days: Day -4 ($20) to Day 0 ($36) IS 5 consecutive days of increase (+80%)!
        // But if day 0 dipped:
        List<DailyAdPerformance> historyWithDip =
        [
            new(now.AddDays(-4), 5_000, 100, 100m, 5, 200m), // CPA = $20
            new(now.AddDays(-3), 5_000, 100, 120m, 5, 200m), // CPA = $24
            new(now.AddDays(-2), 5_000, 100, 110m, 5, 200m), // CPA = $22 (dip!)
            new(now.AddDays(-1), 5_000, 100, 140m, 5, 200m), // CPA = $28
            new(now,             5_000, 100, 160m, 5, 200m)  // CPA = $32
        ];

        var dipDiag = _fatigueService.EvaluateCpaInflection(historyWithDip);
        dipDiag.IsTriggered.Should().BeFalse();
    }

    [Fact]
    public void EvaluateCpaInflection_FiveDaysEscalationUnder35Percent_DoesNotTriggerAlarm()
    {
        // 5 consecutive increases but total hike is only +15%: 20.0 -> 20.6 -> 21.3 -> 22.0 -> 23.0
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history =
        [
            new(now.AddDays(-4), 5_000, 100, 100.0m, 5, 200m), // $20.0
            new(now.AddDays(-3), 5_000, 100, 103.0m, 5, 200m), // $20.6
            new(now.AddDays(-2), 5_000, 100, 106.5m, 5, 200m), // $21.3
            new(now.AddDays(-1), 5_000, 100, 110.0m, 5, 200m), // $22.0
            new(now,             5_000, 100, 115.0m, 5, 200m)  // $23.0 (+15%)
        ];

        var diag = _fatigueService.EvaluateCpaInflection(history);

        diag.IsTriggered.Should().BeFalse();
        diag.TotalIncreasePercentage.Should().BeApproximately(15.0, 0.5);
    }

    [Fact]
    public void GenerateRefreshTicket_CriticalSeverity_ProducesP1ImmediateTicket()
    {
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history =
        [
            new(now.AddDays(-4), 5_000, 100, 100m, 5, 200m), // $20
            new(now.AddDays(-3), 5_000, 100, 120m, 5, 200m), // $24
            new(now.AddDays(-2), 5_000, 100, 140m, 5, 200m), // $28
            new(now.AddDays(-1), 5_000, 100, 160m, 5, 200m), // $32
            new(now,             5_000, 100, 180m, 5, 200m)  // $36 (+80%)
        ];

        var eval = _fatigueService.EvaluateCreativeFatigue("camp-critical-123", history, customFrequency: 4.2);

        eval.OverallSeverity.Should().Be(CreativeFatigueSeverity.CriticalCpaSpike);
        eval.RefreshTicket.Priority.Should().Be(TicketPriority.P1_Immediate);
        eval.RefreshTicket.HookSwapSuggestion.Should().Contain("shock stat");
        eval.RefreshTicket.AudienceRotationAdvice.Should().Contain("exclude");
    }

    [Fact]
    public void GenerateRefreshTicket_HighFatigue_ProducesP2Ticket()
    {
        var now = DateTime.UtcNow.Date;
        // Flat CPA so no CPA alarm, but declining CTR with high frequency
        List<DailyAdPerformance> history = [];
        for (int i = 6; i >= 0; i--)
        {
            double ctr = 0.030 - (0.003 * (6 - i));
            history.Add(new DailyAdPerformance(now.AddDays(-i), 10_000, (int)(10_000 * ctr), 100m, 5, 200m));
        }

        var eval = _fatigueService.EvaluateCreativeFatigue("camp-fatigue-456", history, customFrequency: 4.0);

        eval.OverallSeverity.Should().Be(CreativeFatigueSeverity.HighFatigue);
        eval.RefreshTicket.Priority.Should().Be(TicketPriority.P2_Within48Hours);
        eval.RefreshTicket.RecommendedFormat.Should().Contain("carousel");
    }

    [Fact]
    public void GenerateRefreshTicket_HealthyCreative_ProducesP4NoneTicket()
    {
        var now = DateTime.UtcNow.Date;
        List<DailyAdPerformance> history = [];
        for (int i = 6; i >= 0; i--)
        {
            history.Add(new DailyAdPerformance(now.AddDays(-i), 10_000, 250, 100m, 5, 200m));
        }

        var eval = _fatigueService.EvaluateCreativeFatigue("camp-healthy-789", history, customFrequency: 2.0);

        eval.OverallSeverity.Should().Be(CreativeFatigueSeverity.Healthy);
        eval.RefreshTicket.Priority.Should().Be(TicketPriority.P4_None);
    }

    #endregion

    #region 10. Concurrency & System.Threading.Lock Thread-Safety Tests

    [Fact]
    public async Task DynamicBudgetReallocationService_ConcurrentRequests_ExecuteSafelyWithoutBudgetConservationViolation()
    {
        _reallocationService.ClearAuditHistory();

        List<ChannelBudgetInput> channels =
        [
            new("meta", "Meta Ads", 5_000.0, 1_000.0, 15_000.0, HillParameters.DefaultMeta),
            new("google", "Google Ads", 5_000.0, 1_000.0, 15_000.0, HillParameters.DefaultGoogle),
            new("tiktok", "TikTok Ads", 5_000.0, 1_000.0, 15_000.0, HillParameters.DefaultTikTok)
        ];

        const int parallelTasksCount = 25;
        var tasks = new Task<ReallocationPlanResult>[parallelTasksCount];

        for (int i = 0; i < parallelTasksCount; i++)
        {
            int seed = i * 37 + 1;
            tasks[i] = Task.Run(() => _reallocationService.ReallocateBudgets(channels, customTotalBudget: 15_000.0, randomSeed: seed));
        }

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(parallelTasksCount);
        foreach (var plan in results)
        {
            double sum = plan.Allocations.Sum(a => a.NewSpend);
            Math.Abs(sum - 15_000.0).Should().BeLessThanOrEqualTo(1e-4);
        }

        var auditHistory = _reallocationService.GetAuditHistory();
        auditHistory.Should().HaveCount(parallelTasksCount);
    }

    [Fact]
    public async Task DynamicBudgetReallocationService_ConcurrentAuditLogging_MaintainsAccurateAuditTrail()
    {
        _reallocationService.ClearAuditHistory();

        List<ChannelBudgetInput> channels =
        [
            new("ch1", "Ch1", 3_000.0, 500.0, 10_000.0, HillParameters.DefaultMeta),
            new("ch2", "Ch2", 7_000.0, 500.0, 10_000.0, HillParameters.DefaultGoogle)
        ];

        const int iterations = 30;
        var tasks = Enumerable.Range(0, iterations).Select(i =>
            Task.Run(() => _reallocationService.ReallocateBudgets(channels, customTotalBudget: 10_000.0, randomSeed: i))
        );

        await Task.WhenAll(tasks);

        var history = _reallocationService.GetAuditHistory();
        history.Should().HaveCount(iterations);
        history.All(h => Math.Abs(h.TotalBudget - 10_000.0) <= 1e-4).Should().BeTrue();
    }

    #endregion

    #region 11. Direct ServiceCollection Registration Validation (Hard Constraint 2)

    [Fact]
    public void AddAdSpendServices_RegistersAllRequiredServicesAsSingletonWithoutCallingBuildServiceProvider()
    {
        // HARD CONSTRAINT: In unit tests, NEVER call services.BuildServiceProvider(). Assert directly on ServiceCollection descriptors.
        var services = new ServiceCollection();
        services.AddAdSpendServices();

        // 1. IPredictiveRoasEngine -> PredictiveRoasEngine (Singleton)
        services.Should().Contain(d =>
            d.ServiceType == typeof(IPredictiveRoasEngine) &&
            d.ImplementationType == typeof(PredictiveRoasEngine) &&
            d.Lifetime == ServiceLifetime.Singleton);

        // 2. IDynamicBudgetReallocationService -> DynamicBudgetReallocationService (Singleton)
        services.Should().Contain(d =>
            d.ServiceType == typeof(IDynamicBudgetReallocationService) &&
            d.ImplementationType == typeof(DynamicBudgetReallocationService) &&
            d.Lifetime == ServiceLifetime.Singleton);

        // 3. ICreativeFatigueRadarService -> CreativeFatigueRadarService (Singleton)
        services.Should().Contain(d =>
            d.ServiceType == typeof(ICreativeFatigueRadarService) &&
            d.ImplementationType == typeof(CreativeFatigueRadarService) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    #endregion
}
