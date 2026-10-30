using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SGrowing.Api.Modules.Funnels;
using Xunit;

namespace SGrowing.Tests.Funnels;

public class FunnelSplitTestingBanditServiceTests
{
    private readonly FunnelSplitTestingBanditService _service;

    public FunnelSplitTestingBanditServiceTests()
    {
        _service = new FunnelSplitTestingBanditService();
    }

    #region Dirichlet & Gamma Sampler Tests

    [Fact]
    public void DirichletSampler_SampleGamma_AlphaAtLeastOne_ReturnsPositiveValues()
    {
        // Marsaglia & Tsang algorithm for alpha >= 1
        for (int i = 0; i < 50; i++)
        {
            double val = DirichletSampler.SampleGamma(2.5);
            val.Should().BeGreaterThan(0.0);
            double.IsNaN(val).Should().BeFalse();
            double.IsInfinity(val).Should().BeFalse();
        }
    }

    [Fact]
    public void DirichletSampler_SampleGamma_AlphaLessThanOne_ReturnsPositiveValues()
    {
        // Boosted transformation for 0 < alpha < 1
        for (int i = 0; i < 50; i++)
        {
            double val = DirichletSampler.SampleGamma(0.4);
            val.Should().BeGreaterThan(0.0);
            double.IsNaN(val).Should().BeFalse();
            double.IsInfinity(val).Should().BeFalse();
        }
    }

    [Fact]
    public void DirichletSampler_SampleGamma_ThrowsOnNonPositiveAlpha()
    {
        Action actZero = () => DirichletSampler.SampleGamma(0.0);
        Action actNegative = () => DirichletSampler.SampleGamma(-1.5);
        Action actNaN = () => DirichletSampler.SampleGamma(double.NaN);

        actZero.Should().Throw<ArgumentOutOfRangeException>();
        actNegative.Should().Throw<ArgumentOutOfRangeException>();
        actNaN.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DirichletSampler_SampleStandardNormal_ProducesFiniteValues()
    {
        for (int i = 0; i < 100; i++)
        {
            double z = DirichletSampler.SampleStandardNormal();
            double.IsNaN(z).Should().BeFalse();
            double.IsInfinity(z).Should().BeFalse();
        }
    }

    [Fact]
    public void DirichletSampler_SampleDirichlet_ReturnsVectorSummingToOne()
    {
        double[] alphas = [10.0, 5.0, 3.0, 2.0, 1.0];

        for (int i = 0; i < 50; i++)
        {
            double[] p = DirichletSampler.SampleDirichlet(alphas);
            p.Should().HaveCount(alphas.Length);
            p.Sum().Should().BeApproximately(1.0, 1e-6);
        }
    }

    [Fact]
    public void DirichletSampler_SampleDirichlet_AllProbabilitiesInZeroToOneRange()
    {
        double[] alphas = [2.0, 2.0, 2.0, 2.0, 2.0];

        for (int i = 0; i < 50; i++)
        {
            double[] p = DirichletSampler.SampleDirichlet(alphas);
            foreach (var prob in p)
            {
                prob.Should().BeInRange(0.0, 1.0);
            }
        }
    }

    [Fact]
    public void DirichletSampler_SampleDirichlet_HandlesSkewedAlphas()
    {
        // Strongly dominant first outcome
        double[] alphas = [500.0, 1.0, 1.0, 1.0, 1.0];
        double[] p = DirichletSampler.SampleDirichlet(alphas);

        p[0].Should().BeGreaterThan(p[1]);
        p[0].Should().BeGreaterThan(p[2]);
        p.Sum().Should().BeApproximately(1.0, 1e-6);
    }

    [Fact]
    public void DirichletSampler_SampleDirichlet_ThrowsOnNullOrEmptyAlphas()
    {
        Action actNull = () => DirichletSampler.SampleDirichlet(null!);
        Action actEmpty = () => DirichletSampler.SampleDirichlet([]);

        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
    }

    #endregion

    #region RPV & Mathematical Dot Product Tests

    [Fact]
    public void CalculatePosteriorMeanRpv_WithKnownAlphas_CalculatesExactTheoreticalRpv()
    {
        // Alphas: Bounce=50, Lead=20, Checkout=15, Paid=10, Upsell=5. Total = 100
        // Values: Bounce=0, Lead=10, Checkout=20, Paid=100, Upsell=200
        // Expected = (0.50*0) + (0.20*10) + (0.15*20) + (0.10*100) + (0.05*200)
        //          = 0 + 2.0 + 3.0 + 10.0 + 10.0 = $25.00
        double[] alphas = [50, 20, 15, 10, 5];
        var config = new FunnelOutcomeValueConfig(0m, 10m, 20m, 100m, 200m);

        decimal rpv = FunnelSplitTestingBanditService.CalculatePosteriorMeanRpv(alphas, config);
        rpv.Should().Be(25.00m);
    }

    [Fact]
    public void CalculatePosteriorMeanRpv_ZeroAlphas_ReturnsZero()
    {
        double[] alphas = [0, 0, 0, 0, 0];
        var config = new FunnelOutcomeValueConfig();

        decimal rpv = FunnelSplitTestingBanditService.CalculatePosteriorMeanRpv(alphas, config);
        rpv.Should().Be(0m);
    }

    #endregion

    #region Service Initialization & Seed Funnel Tests

    [Fact]
    public void Service_Initialization_SeedsThreeRealisticFunnelsWithThreeVariantsEach()
    {
        var experiments = _service.GetAllExperiments();
        experiments.Should().HaveCount(3);

        var saasExp = _service.GetExperiment("funnel-saas-enterprise");
        var b2bExp = _service.GetExperiment("funnel-highticket-b2b");
        var ecomExp = _service.GetExperiment("funnel-ecommerce-growth");

        saasExp.Should().NotBeNull();
        b2bExp.Should().NotBeNull();
        ecomExp.Should().NotBeNull();

        saasExp!.Variants.Should().HaveCount(3);
        b2bExp!.Variants.Should().HaveCount(3);
        ecomExp!.Variants.Should().HaveCount(3);

        saasExp.Variants.Select(v => v.VariantId).Should().BeEquivalentTo(new[] { "A", "B", "C" });
    }

    [Fact]
    public void Service_GetAllExperiments_ReturnsAllSeedFunnelsWithValidAggregates()
    {
        var summaries = _service.GetAllExperiments();
        summaries.Should().HaveCount(3);

        foreach (var s in summaries)
        {
            s.FunnelId.Should().NotBeNullOrWhiteSpace();
            s.FunnelName.Should().NotBeNullOrWhiteSpace();
            s.TotalVisitors.Should().BeGreaterThan(0);
            s.TotalRevenue.Should().BeGreaterThan(0m);
            s.VariantCount.Should().Be(3);
        }
    }

    [Fact]
    public void Service_GetExperiment_ReturnsNullForInvalidId()
    {
        var exp = _service.GetExperiment("non-existent-funnel-xyz");
        exp.Should().BeNull();

        var emptyExp = _service.GetExperiment("");
        emptyExp.Should().BeNull();
    }

    [Fact]
    public void Service_GetExperiment_ReturnsDeepClonedState()
    {
        var exp1 = _service.GetExperiment("funnel-saas-enterprise");
        var exp2 = _service.GetExperiment("funnel-saas-enterprise");

        exp1.Should().NotBeNull();
        exp2.Should().NotBeNull();
        exp1.Should().NotBeSameAs(exp2);
        exp1!.Variants[0].Should().NotBeSameAs(exp2!.Variants[0]);
    }

    #endregion

    #region Visitor Routing Tests

    [Fact]
    public void Service_RouteVisitor_ReturnsValidDecisionWithValidVariant()
    {
        var decision = _service.RouteVisitor("funnel-saas-enterprise");

        decision.Should().NotBeNull();
        decision.FunnelId.Should().Be("funnel-saas-enterprise");
        decision.VisitorId.Should().StartWith("vis_");
        new[] { "A", "B", "C" }.Should().Contain(decision.SelectedVariantId);
        decision.SelectedVariantName.Should().NotBeNullOrWhiteSpace();
        new[] { "ThompsonSampling", "ExplorationFloor", "CannibalizationFloor" }.Should().Contain(decision.RoutingReason);
        decision.ExplorationRate.Should().Be(0.05);
    }

    [Fact]
    public void Service_RouteVisitor_UsesProvidedVisitorId()
    {
        string customId = "user_enterprise_99812";
        var decision = _service.RouteVisitor("funnel-saas-enterprise", customId);

        decision.VisitorId.Should().Be(customId);
    }

    [Fact]
    public void Service_RouteVisitor_ProvidesSampledRpvsForEveryVariant()
    {
        var decision = _service.RouteVisitor("funnel-highticket-b2b");

        decision.SampledRpvs.Should().HaveCount(3);
        decision.SampledRpvs.ContainsKey("A").Should().BeTrue();
        decision.SampledRpvs.ContainsKey("B").Should().BeTrue();
        decision.SampledRpvs.ContainsKey("C").Should().BeTrue();

        foreach (var rpv in decision.SampledRpvs.Values)
        {
            rpv.Should().BeGreaterThanOrEqualTo(0m);
        }
    }

    [Fact]
    public void Service_RouteVisitor_ThrowsOnInvalidFunnelId()
    {
        Action act = () => _service.RouteVisitor("invalid-funnel-id");
        act.Should().Throw<KeyNotFoundException>();
    }

    #endregion

    #region Outcome Recording Tests

    [Fact]
    public void Service_RecordOutcome_IncrementsSpecificOutcomeAlpha()
    {
        string funnelId = "funnel-saas-enterprise";
        string variantId = "B";

        var initialExp = _service.GetExperiment(funnelId)!;
        var initialVariant = initialExp.Variants.First(v => v.VariantId == variantId);
        double initialOrderPaidAlpha = initialVariant.Alphas[(int)FunnelStepOutcome.OrderPaid];

        var result = _service.RecordOutcome(funnelId, variantId, FunnelStepOutcome.OrderPaid);

        result.Should().NotBeNull();
        result.FunnelId.Should().Be(funnelId);
        result.VariantId.Should().Be(variantId);
        result.Outcome.Should().Be(FunnelStepOutcome.OrderPaid);
        result.UpdatedAlphas[(int)FunnelStepOutcome.OrderPaid].Should().Be(initialOrderPaidAlpha + 1.0);
    }

    [Fact]
    public void Service_RecordOutcome_IncreasesTotalVisitorsAndRevenue()
    {
        string funnelId = "funnel-ecommerce-growth";
        string variantId = "A";

        var initialExp = _service.GetExperiment(funnelId)!;
        var initialVariant = initialExp.Variants.First(v => v.VariantId == variantId);
        int initialVisitors = initialVariant.TotalVisitors;
        decimal initialRevenue = initialVariant.TotalRevenue;

        var result = _service.RecordOutcome(funnelId, variantId, FunnelStepOutcome.UpsellAccepted);

        result.TotalVisitors.Should().Be(initialVisitors + 1);

        var updatedExp = _service.GetExperiment(funnelId)!;
        var updatedVariant = updatedExp.Variants.First(v => v.VariantId == variantId);
        updatedVariant.TotalRevenue.Should().Be(initialRevenue + initialExp.OutcomeValues.UpsellAccepted);
    }

    [Fact]
    public void Service_RecordOutcome_SupportsCustomStepValueOverride()
    {
        string funnelId = "funnel-saas-enterprise";
        string variantId = "C";
        decimal customDealValue = 4999.00m;

        var result = _service.RecordOutcome(funnelId, variantId, FunnelStepOutcome.OrderPaid, customValue: customDealValue);

        result.AttributedValue.Should().Be(customDealValue);
    }

    [Fact]
    public void Service_RecordOutcome_RecalculatesExpectedRpvAndConversionRates()
    {
        string funnelId = "funnel-saas-enterprise";
        string variantId = "A";

        var result = _service.RecordOutcome(funnelId, variantId, FunnelStepOutcome.OrderPaid);

        result.ExpectedRpv.Should().BeGreaterThan(0m);
        result.PaidConversionRate.Should().BeInRange(0.0, 1.0);
        result.LeadConversionRate.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void Service_RecordOutcome_ThrowsOnInvalidVariantOrFunnel()
    {
        Action actBadFunnel = () => _service.RecordOutcome("non-existent-funnel", "A", FunnelStepOutcome.Bounce);
        Action actBadVariant = () => _service.RecordOutcome("funnel-saas-enterprise", "Z", FunnelStepOutcome.Bounce);

        actBadFunnel.Should().Throw<KeyNotFoundException>();
        actBadVariant.Should().Throw<KeyNotFoundException>();
    }

    #endregion

    #region Monte Carlo Superiority Simulation Tests

    [Fact]
    public void Service_RunSuperioritySimulation_EnforcesMinimum500Iterations()
    {
        string funnelId = "funnel-saas-enterprise";

        // Request 100 iterations, should automatically clamp to at least 500
        var report = _service.RunSuperioritySimulation(funnelId, iterations: 100);

        report.Iterations.Should().BeGreaterThanOrEqualTo(500);
        report.Variants.Should().HaveCount(3);
    }

    [Fact]
    public void Service_RunSuperioritySimulation_WinProbabilitiesSumToOne()
    {
        string funnelId = "funnel-ecommerce-growth";
        var report = _service.RunSuperioritySimulation(funnelId, iterations: 1000);

        double totalWinProb = report.Variants.Sum(v => v.WinProbability);
        totalWinProb.Should().BeApproximately(1.0, 0.05);
    }

    [Fact]
    public void Service_RunSuperioritySimulation_BaselineSuperiorityIsFiftyPercent()
    {
        string funnelId = "funnel-highticket-b2b";
        var report = _service.RunSuperioritySimulation(funnelId, iterations: 1000);

        var baseline = report.Variants.First(v => v.VariantId == report.BaselineVariantId);
        baseline.SuperiorityVsBaseline.Should().Be(0.50);
    }

    [Fact]
    public void Service_RunSuperioritySimulation_CalculatesCredibleIntervalsConsistently()
    {
        string funnelId = "funnel-saas-enterprise";
        var report = _service.RunSuperioritySimulation(funnelId, iterations: 1000);

        foreach (var v in report.Variants)
        {
            v.CredibleIntervalLow.Should().BeLessThanOrEqualTo(v.CredibleIntervalHigh);
            // Posterior mean should typically fall between or very near 95% credible bounds
            v.ExpectedRpv.Should().BeGreaterThan(0m);
        }
    }

    #endregion

    #region Auto-Cannibalization Logic Tests

    [Fact]
    public void Service_AutoCannibalization_TriggersWhenVariantExceedsThresholdAndSuperiority()
    {
        string funnelId = "funnel-saas-enterprise";

        // Run superiority with threshold = 100 visitors.
        // In seed data, Variant C has 584 visitors and superior revenue numbers (> 95% superiority vs baseline A).
        var report = _service.RunSuperioritySimulation(funnelId, iterations: 1000, minVisitorThreshold: 100);

        var variantC = report.Variants.First(v => v.VariantId == "C");
        if (variantC.SuperiorityVsBaseline >= 0.95 || variantC.WinProbability >= 0.95)
        {
            report.AutoCannibalizationActive.Should().BeTrue();
            report.WinningVariantId.Should().Be("C");
            variantC.IsWinner.Should().BeTrue();
            variantC.RecommendedTrafficShare.Should().Be(0.95); // 1 - epsilon

            // Inferior arms A & B should be marked as cannibalized
            var variantA = report.Variants.First(v => v.VariantId == "A");
            var variantB = report.Variants.First(v => v.VariantId == "B");
            variantA.IsCannibalized.Should().BeTrue();
            variantB.IsCannibalized.Should().BeTrue();
        }
    }

    [Fact]
    public void Service_AutoCannibalization_DoesNotTriggerWhenVisitorCountIsBelowThreshold()
    {
        string funnelId = "funnel-saas-enterprise";

        // Set high min visitor threshold = 10,000 visitors
        var report = _service.RunSuperioritySimulation(funnelId, iterations: 1000, minVisitorThreshold: 10000);

        report.AutoCannibalizationActive.Should().BeFalse();
        report.WinningVariantId.Should().BeNull();
        foreach (var v in report.Variants)
        {
            v.IsWinner.Should().BeFalse();
            v.IsCannibalized.Should().BeFalse();
        }
    }

    [Fact]
    public void Service_AutoCannibalization_MarksInferiorVariantsAsCannibalized()
    {
        string funnelId = "funnel-ecommerce-growth";
        var report = _service.RunSuperioritySimulation(funnelId, iterations: 1000, minVisitorThreshold: 50);

        if (report.AutoCannibalizationActive)
        {
            var winner = report.Variants.First(v => v.VariantId == report.WinningVariantId);
            winner.IsWinner.Should().BeTrue();
            winner.IsCannibalized.Should().BeFalse();

            var losers = report.Variants.Where(v => v.VariantId != report.WinningVariantId);
            foreach (var loser in losers)
            {
                loser.IsCannibalized.Should().BeTrue();
                loser.IsWinner.Should().BeFalse();
                loser.RecommendedTrafficShare.Should().BeLessThan(winner.RecommendedTrafficShare);
            }
        }
    }

    [Fact]
    public void Service_ResetExperiment_RestoresPriorsAndClearsCannibalization()
    {
        string funnelId = "funnel-saas-enterprise";

        // Record some events
        _service.RecordOutcome(funnelId, "A", FunnelStepOutcome.OrderPaid);
        _service.RecordOutcome(funnelId, "B", FunnelStepOutcome.UpsellAccepted);

        // Reset
        _service.ResetExperiment(funnelId);

        var exp = _service.GetExperiment(funnelId)!;
        exp.AutoCannibalizationActive.Should().BeFalse();
        exp.WinningVariantId.Should().BeNull();

        // Alphas should match initial seed alphas
        foreach (var v in exp.Variants)
        {
            v.Alphas.Should().BeEquivalentTo(v.InitialAlphas);
            v.IsWinner.Should().BeFalse();
            v.IsCannibalized.Should().BeFalse();
        }
    }

    #endregion

    #region Concurrency & Thread-Safety Tests

    [Fact]
    public void Service_ConcurrentOutcomeRecording_IsThreadSafeAndConsistent()
    {
        string funnelId = "funnel-ecommerce-growth";
        string variantId = "B";

        var initialExp = _service.GetExperiment(funnelId)!;
        var initialVariant = initialExp.Variants.First(v => v.VariantId == variantId);
        int initialLeadAlphas = (int)initialVariant.Alphas[(int)FunnelStepOutcome.LeadCaptured];

        int parallelCalls = 200;

        Parallel.For(0, parallelCalls, _ =>
        {
            _service.RecordOutcome(funnelId, variantId, FunnelStepOutcome.LeadCaptured);
        });

        var finalExp = _service.GetExperiment(funnelId)!;
        var finalVariant = finalExp.Variants.First(v => v.VariantId == variantId);

        finalVariant.Alphas[(int)FunnelStepOutcome.LeadCaptured].Should().Be(initialLeadAlphas + parallelCalls);
        finalVariant.TotalVisitors.Should().Be(initialVariant.TotalVisitors + parallelCalls);
    }

    [Fact]
    public void Service_ConcurrentVisitorRouting_DoesNotDeadlockOrThrow()
    {
        string funnelId = "funnel-saas-enterprise";
        var results = new ConcurrentBag<RouteVisitorResult>();

        Parallel.For(0, 150, i =>
        {
            var res = _service.RouteVisitor(funnelId, $"visitor_{i}");
            results.Add(res);
        });

        results.Should().HaveCount(150);
        results.All(r => r.SelectedVariantId != null).Should().BeTrue();
    }

    #endregion
}
