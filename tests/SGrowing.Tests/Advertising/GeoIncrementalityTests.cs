using System;
using System.Collections.Generic;
using System.Linq;
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
}
