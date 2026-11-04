using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SGrowing.Api.Modules.Advertising.GeoIncrementality;
using Xunit;

namespace SGrowing.Tests.Advertising;

public sealed class GeoIncrementalityTests
{
    [Fact]
    public void GetOverview_ReturnsInitialExperimentsAndCells()
    {
        var engine = new GeoIncrementalityEngine();
        var overview = engine.GetOverview();

        Assert.NotNull(overview);
        Assert.True(overview.ActiveExperimentsCount >= 1);
        Assert.True(overview.TotalDmaRegionsCalibrated >= 2);
        Assert.True(overview.MeanTrueIncrementalRoas > 1.0);
        Assert.NotEmpty(overview.Experiments);
        Assert.NotEmpty(overview.CalibratedCells);
        Assert.Contains(overview.CalibratedCells, c => c.CellId == "CELL-DMA-DFW" && c.MarketRole == "TEST_TREATMENT");
        Assert.Contains(overview.CalibratedCells, c => c.CellId == "CELL-DMA-CHI" && c.MarketRole == "SYNTHETIC_CONTROL");
    }

    [Fact]
    public void LaunchExperiment_CreatesHoldoutDesign()
    {
        var engine = new GeoIncrementalityEngine();
        var request = new LaunchGeoExperimentRequest(
            "Streaming CTV Holiday Incrementality",
            80000.00m,
            4,
            new[] { "DFW", "ATL", "PHX" }
        );

        var exp = engine.LaunchExperiment(request);

        Assert.NotNull(exp);
        Assert.StartsWith("GEO-EXP-", exp.ExperimentId);
        Assert.Equal(80000.00m, exp.TotalAdSpendUsd);
        Assert.Equal("RUNNING_HOLD_OUT", exp.Status);
        Assert.Equal(3.75, exp.TrueIncrementalRoasMultiplier);
        Assert.Equal(300000.0, exp.MeasuredIncrementalRevenueUsd); // 80k * 3.75

        var overview = engine.GetOverview();
        Assert.Contains(overview.Experiments, e => e.ExperimentId == exp.ExperimentId);
    }

    [Fact]
    public void EvaluateLift_CalculatesPureCausalRoas()
    {
        var engine = new GeoIncrementalityEngine();
        var request = new EvaluateLiftRequest(
            "GEO-EXP-2026-01",
            850000.00m, // Treatment group revenue
            250000.00m  // Synthetic control counterfactual
        );

        var exp = engine.EvaluateLift(request);

        Assert.NotNull(exp);
        Assert.Equal(600000.0, exp.MeasuredIncrementalRevenueUsd);
        Assert.Equal(4.0, exp.TrueIncrementalRoasMultiplier);
        Assert.Equal("COMPLETED_SIGNIFICANT", exp.Status);
    }

    [Fact]
    public void EvaluateLift_ThrowsWhenExperimentNotFound()
    {
        var engine = new GeoIncrementalityEngine();
        var request = new EvaluateLiftRequest("NON-EXISTENT-EXP", 100000m, 50000m);

        Assert.Throws<KeyNotFoundException>(() => engine.EvaluateLift(request));
    }

    [Fact]
    public void EvaluateLift_CalculatesLiftForNewlyLaunchedExperiment()
    {
        var engine = new GeoIncrementalityEngine();
        var launched = engine.LaunchExperiment(new LaunchGeoExperimentRequest(
            "Midwest Digital Audio Incrementality",
            50000.00m,
            6,
            new[] { "MSP", "DET", "CLE" }
        ));

        var evaluated = engine.EvaluateLift(new EvaluateLiftRequest(
            launched.ExperimentId,
            450000.00m,
            200000.00m
        ));

        Assert.Equal(250000.0, evaluated.MeasuredIncrementalRevenueUsd);
        Assert.Equal(5.0, evaluated.TrueIncrementalRoasMultiplier); // 250k / 50k
        Assert.Equal("COMPLETED_SIGNIFICANT", evaluated.Status);
    }

    [Fact]
    public void EvaluateLift_Should_Handle_Zero_Or_Negative_Lift()
    {
        var engine = new GeoIncrementalityEngine();
        var launched = engine.LaunchExperiment(new LaunchGeoExperimentRequest(
            "Underperforming Channel Test",
            100000.00m,
            2,
            new[] { "BOS" }
        ));

        // Treatment generated less than synthetic control (negative lift)
        var evaluated = engine.EvaluateLift(new EvaluateLiftRequest(
            launched.ExperimentId,
            120000.00m,
            150000.00m
        ));

        Assert.Equal(-30000.0, evaluated.MeasuredIncrementalRevenueUsd);
        Assert.Equal(-0.3, evaluated.TrueIncrementalRoasMultiplier);
    }

    [Fact]
    public void GetOverview_ReflectsMultipleLaunchedExperiments_AndAggregatedMetrics()
    {
        var engine = new GeoIncrementalityEngine();
        var initialOverview = engine.GetOverview();

        var exp1 = engine.LaunchExperiment(new LaunchGeoExperimentRequest(
            "Pacific Northwest CTV Lift",
            60000m,
            3,
            new[] { "SEA", "PDX" }
        ));

        var exp2 = engine.LaunchExperiment(new LaunchGeoExperimentRequest(
            "Northeast Podcast Lift",
            40000m,
            4,
            new[] { "NYC", "PHL" }
        ));

        var updatedOverview = engine.GetOverview();

        updatedOverview.ActiveExperimentsCount.Should().Be(initialOverview.ActiveExperimentsCount + 2);
        updatedOverview.Experiments.Should().Contain(e => e.ExperimentId == exp1.ExperimentId);
        updatedOverview.Experiments.Should().Contain(e => e.ExperimentId == exp2.ExperimentId);
        updatedOverview.TotalIncrementalRevenueCapturedUsd.Should().BeGreaterThan(initialOverview.TotalIncrementalRevenueCapturedUsd);
    }

    [Fact]
    public void LaunchExperiment_CalculatesDateWindowAccurately()
    {
        var engine = new GeoIncrementalityEngine();
        var beforeLaunch = DateTime.UtcNow.AddSeconds(-2);
        const int testWeeks = 5;

        var exp = engine.LaunchExperiment(new LaunchGeoExperimentRequest(
            "Mountain Region Multi-Cell Test",
            75000m,
            testWeeks,
            new[] { "DEN", "SLC" }
        ));
        var afterLaunch = DateTime.UtcNow.AddSeconds(2);

        exp.StartDate.Should().BeOnOrAfter(beforeLaunch).And.BeOnOrBefore(afterLaunch);
        var expectedEnd = exp.StartDate.AddDays(testWeeks * 7);
        exp.EndDate.Should().BeCloseTo(expectedEnd, TimeSpan.FromSeconds(2));
        exp.PValueStatisticalSignificance.Should().BeLessThan(0.05); // Statistically significant threshold
    }

    [Fact]
    public void EvaluateLift_ZeroAdSpend_AvoidsDivideByZero_AndUsesDefaultRoas()
    {
        var engine = new GeoIncrementalityEngine();
        var exp = engine.LaunchExperiment(new LaunchGeoExperimentRequest(
            "Organic Halo Comparison",
            0m,
            2,
            new[] { "MIA" }
        ));

        var evaluated = engine.EvaluateLift(new EvaluateLiftRequest(
            exp.ExperimentId,
            100000m,
            40000m
        ));

        evaluated.MeasuredIncrementalRevenueUsd.Should().Be(60000.0);
        evaluated.TrueIncrementalRoasMultiplier.Should().Be(3.5); // Fallback default when ad spend is zero
        evaluated.Status.Should().Be("COMPLETED_SIGNIFICANT");
    }

    [Fact]
    public void EvaluateLift_ZeroNetLift_ProducesZeroRoas()
    {
        var engine = new GeoIncrementalityEngine();
        var exp = engine.LaunchExperiment(new LaunchGeoExperimentRequest(
            "Equal Distribution Baseline",
            50000m,
            4,
            new[] { "AUS" }
        ));

        var evaluated = engine.EvaluateLift(new EvaluateLiftRequest(
            exp.ExperimentId,
            250000m,
            250000m
        ));

        evaluated.MeasuredIncrementalRevenueUsd.Should().Be(0.0);
        evaluated.TrueIncrementalRoasMultiplier.Should().Be(0.0);
        evaluated.Status.Should().Be("COMPLETED_SIGNIFICANT");
    }

    [Fact]
    public void EvaluateLift_MultipleEvaluations_UpdatesExperimentStateIdempotently()
    {
        var engine = new GeoIncrementalityEngine();
        var exp = engine.LaunchExperiment(new LaunchGeoExperimentRequest(
            "Mid-Flight Re-evaluation Test",
            100000m,
            4,
            new[] { "SFO" }
        ));

        // First mid-flight evaluation
        var eval1 = engine.EvaluateLift(new EvaluateLiftRequest(exp.ExperimentId, 300000m, 100000m));
        eval1.MeasuredIncrementalRevenueUsd.Should().Be(200000.0);
        eval1.TrueIncrementalRoasMultiplier.Should().Be(2.0);

        // Second final evaluation with revised counterfactual
        var eval2 = engine.EvaluateLift(new EvaluateLiftRequest(exp.ExperimentId, 450000m, 100000m));
        eval2.MeasuredIncrementalRevenueUsd.Should().Be(350000.0);
        eval2.TrueIncrementalRoasMultiplier.Should().Be(3.5);

        var overview = engine.GetOverview();
        var inOverview = overview.Experiments.First(e => e.ExperimentId == exp.ExperimentId);
        inOverview.MeasuredIncrementalRevenueUsd.Should().Be(350000.0);
        inOverview.TrueIncrementalRoasMultiplier.Should().Be(3.5);
    }

    [Fact]
    public void GetOverview_CalibratedCells_HaveValidConfidenceIntervalsAndRoles()
    {
        var engine = new GeoIncrementalityEngine();
        var overview = engine.GetOverview();

        overview.CalibratedCells.Should().NotBeEmpty();
        foreach (var cell in overview.CalibratedCells)
        {
            cell.CellId.Should().StartWith("CELL-");
            cell.DmaRegionName.Should().NotBeNullOrWhiteSpace();
            cell.BaselineWeeklyRevenueUsd.Should().BeGreaterThan(0m);
            cell.SyntheticWeightCoefficient.Should().BeInRange(0.0, 1.0);
            cell.MarketRole.Should().Match(role => role == "TEST_TREATMENT" || role == "SYNTHETIC_CONTROL");
        }
    }
}
